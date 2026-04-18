using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace HearthstoneCardSearchTool.Core;

public sealed class CardRepository
{
    private static readonly Regex HtmlTagRegex = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex SuffixRegex = new("^(.+\\d)([A-Za-z_].*)$", RegexOptions.Compiled);
    private static readonly HashSet<string> HiddenTags =
    [
        "CARDNAME",
        "CARDTEXT",
        "FLAVORTEXT",
        "ARTISTNAME",
        "TARGETING_ARROW_TEXT",
    ];

    private readonly IReadOnlyList<CardRecord> cards;
    private readonly Dictionary<string, CardRecord> byCardId;
    private readonly Dictionary<int, CardRecord> byDbfId;

    private CardRepository(
        IReadOnlyList<CardRecord> cards,
        Dictionary<string, CardRecord> byCardId,
        Dictionary<int, CardRecord> byDbfId,
        BootstrapData bootstrap,
        bool hasAnyImages)
    {
        this.cards = cards;
        this.byCardId = byCardId;
        this.byDbfId = byDbfId;
        Bootstrap = bootstrap;
        HasAnyImages = hasAnyImages;
    }

    public BootstrapData Bootstrap { get; }
    public bool HasAnyImages { get; }

    public static CardRepository Load(string resourceRoot)
    {
        var xmlPath = Path.Combine(resourceRoot, "CardDefs.xml");
        var imageRoot = Path.Combine(resourceRoot, "cardpng");

        CardDataMaps.Initialize(resourceRoot);

        if (!File.Exists(xmlPath))
        {
            throw new FileNotFoundException("未找到 CardDefs.xml。", xmlPath);
        }

        var imageIndex = BuildImageIndex(imageRoot);
        var document = XDocument.Load(xmlPath);
        var cards = new List<CardRecord>();
        var classLabels = new Dictionary<string, string>(StringComparer.Ordinal);
        var rarityLabels = new Dictionary<string, string>(StringComparer.Ordinal);
        var cardTypeLabels = new Dictionary<string, string>(StringComparer.Ordinal);
        var raceLabels = new Dictionary<string, string>(StringComparer.Ordinal);
        var schoolLabels = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var entity in document.Descendants("Entity"))
        {
            var cardId = entity.Attribute("CardID")?.Value;
            var dbfRaw = entity.Attribute("ID")?.Value;
            if (string.IsNullOrWhiteSpace(cardId) || !int.TryParse(dbfRaw, out var dbfId))
            {
                continue;
            }

            var nameZh = string.Empty;
            var nameEn = string.Empty;
            var textZh = string.Empty;
            var tags = new List<CardTagRecord>();
            var tagMap = new Dictionary<string, string>(StringComparer.Ordinal);
            var enumValues = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var tag in entity.Elements("Tag"))
            {
                var key = tag.Attribute("name")?.Value;
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                var enumId = tag.Attribute("enumID")?.Value;
                var value = ReadTagValue(tag, key, ref nameZh, ref nameEn, ref textZh);

                if (!string.IsNullOrWhiteSpace(value))
                {
                    tagMap[key] = value;
                    if (!string.IsNullOrWhiteSpace(enumId))
                    {
                        enumValues[enumId] = value;
                    }
                }

                tags.Add(new CardTagRecord(key, value, enumId));
            }

            if (string.IsNullOrWhiteSpace(nameZh))
            {
                nameZh = string.IsNullOrWhiteSpace(nameEn) ? cardId : nameEn;
            }

            if (ShouldSkipCard(tagMap))
            {
                continue;
            }

            RememberLabel(classLabels, tagMap, "CLASS", CardDataMaps.ClassMap);
            RememberLabel(rarityLabels, tagMap, "RARITY", CardDataMaps.RarityMap);
            RememberLabel(cardTypeLabels, tagMap, "CARDTYPE", CardDataMaps.CardTypeMap);
            RememberLabel(raceLabels, tagMap, "CARDRACE", CardDataMaps.RaceMap);
            RememberLabel(schoolLabels, tagMap, "SPELL_SCHOOL", CardDataMaps.SchoolMap);

            imageIndex.TryGetValue(cardId.ToLowerInvariant(), out var imagePath);

            cards.Add(
                new CardRecord(
                    cardId,
                    dbfId,
                    nameZh,
                    nameEn,
                    textZh,
                    imagePath,
                    tags,
                    tagMap,
                    enumValues));
        }

        BuildRelationships(cards);
        cards.Sort(CompareCards);

        var byCardId = cards.ToDictionary(static card => card.CardId, StringComparer.Ordinal);
        var byDbfId = cards.ToDictionary(static card => card.DbfId);

        return new CardRepository(
            cards,
            byCardId,
            byDbfId,
            new BootstrapData
            {
                TotalCards = cards.Count,
                Classes = MapToOptions(classLabels),
                Rarities = MapToOptions(rarityLabels),
                CardTypes = MapToOptions(cardTypeLabels),
                Sets = CardDataMaps.GetFilterableSets(),
                Races = MapToOptions(raceLabels),
                Schools = MapToOptions(schoolLabels),
            },
            imageIndex.Count > 0);
    }

    public IReadOnlyList<CardRecord> Search(string query, SearchFilters filters, int limit)
    {
        var normalizedQuery = query.Trim().ToLowerInvariant();
        var results = new List<CardRecord>();

        foreach (var card in cards)
        {
            if (results.Count >= limit)
            {
                break;
            }

            if (!MatchesFilters(card, filters) || !MatchesQuery(card, normalizedQuery))
            {
                continue;
            }

            results.Add(card);
        }

        return results;
    }

    public CardDetailData? GetDetail(string cardId)
    {
        if (!byCardId.TryGetValue(cardId, out var card))
        {
            return null;
        }

        var parentCards = new List<RelatedCardLink>();
        var relatedCards = new List<RelatedCardLink>();
        var enchantmentCards = new List<RelatedCardLink>();

        foreach (var reverseId in card.ReverseRelated.Distinct())
        {
            if (byDbfId.TryGetValue(reverseId, out var parent))
            {
                parentCards.Add(new RelatedCardLink(parent.CardId, parent.DbfId, parent.NameZh, $"ID: {parent.DbfId}"));
            }
        }

        foreach (var forward in card.ForwardRelated.GroupBy(static item => item.DbfId).Select(static group => group.First()))
        {
            if (!byDbfId.TryGetValue(forward.DbfId, out var target))
            {
                continue;
            }

            var row = new RelatedCardLink(target.CardId, target.DbfId, target.NameZh, $"{forward.Reason} (ID: {target.DbfId})");
            if (target.IsEnchantment)
            {
                enchantmentCards.Add(row);
            }
            else
            {
                relatedCards.Add(row);
            }
        }

        var tagRows = card.Tags
            .Where(static tag => !HiddenTags.Contains(tag.Key))
            .OrderBy(static tag => int.TryParse(tag.Key, out _) ? 1 : 0)
            .Select(tag =>
            {
                var targetDbfId = IsRelatedKey(tag.Key) && int.TryParse(tag.Value, out var parsedId) && byDbfId.ContainsKey(parsedId)
                    ? parsedId
                    : (int?)null;

                var targetCardId = targetDbfId.HasValue
                    ? byDbfId[targetDbfId.Value].CardId
                    : null;

                return new CardTagView(
                    tag.Key,
                    CardDataMaps.BuildDisplayTagName(tag.Key, tag.EnumId),
                    CardDataMaps.MapTagValue(tag.Key, tag.Value),
                    tag.EnumId,
                    targetCardId,
                    targetDbfId);
            })
            .ToList();

        return new CardDetailData
        {
            CardId = card.CardId,
            DbfId = card.DbfId,
            Name = card.NameZh,
            Text = card.TextZh,
            ImagePath = card.ImagePath,
            IsEnchantment = card.IsEnchantment,
            ParentCards = parentCards,
            RelatedCards = relatedCards,
            EnchantmentCards = enchantmentCards,
            Tags = tagRows,
        };
    }

    private static string ReadTagValue(XElement tag, string key, ref string nameZh, ref string nameEn, ref string textZh)
    {
        if (string.Equals(tag.Attribute("type")?.Value, "LocString", StringComparison.Ordinal))
        {
            var zh = ChildText(tag, "zhCN");
            var en = ChildText(tag, "enUS");

            if (key == "CARDNAME")
            {
                nameZh = string.IsNullOrWhiteSpace(zh) ? en : zh;
                nameEn = en;
            }

            if (key == "CARDTEXT")
            {
                textZh = CleanCardText(string.IsNullOrWhiteSpace(zh) ? en : zh);
            }

            return string.IsNullOrWhiteSpace(zh) ? en : zh;
        }

        return tag.Attribute("value")?.Value ?? tag.Value.Trim();
    }

    private static void RememberLabel(
        IDictionary<string, string> labels,
        IReadOnlyDictionary<string, string> tagMap,
        string key,
        IReadOnlyDictionary<string, string> dictionary)
    {
        if (tagMap.TryGetValue(key, out var code) && !string.IsNullOrWhiteSpace(code) && dictionary.TryGetValue(code, out var label))
        {
            labels[code] = label;
        }
    }

    private static List<FilterOption> MapToOptions(IDictionary<string, string> labels)
    {
        return labels
            .Select(static item => new FilterOption(item.Key, item.Value))
            .OrderBy(static item => item.Label, StringComparer.Ordinal)
            .ThenBy(static item => item.Value, StringComparer.Ordinal)
            .ToList();
    }

    private static int CompareCards(CardRecord left, CardRecord right)
    {
        var costCompare = Nullable.Compare(left.Cost, right.Cost);
        if (costCompare != 0)
        {
            return costCompare;
        }

        var nameCompare = StringComparer.Ordinal.Compare(left.NameZh, right.NameZh);
        return nameCompare != 0
            ? nameCompare
            : StringComparer.Ordinal.Compare(left.CardId, right.CardId);
    }

    private static Dictionary<string, string> BuildImageIndex(string imageRoot)
    {
        if (!Directory.Exists(imageRoot))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var imageIndex = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in Directory
                     .EnumerateFiles(imageRoot, "*.png", SearchOption.AllDirectories)
                     .Select(static path => Path.GetFullPath(path))
                     .OrderBy(static path => GetImagePathPriority(path))
                     .ThenBy(static path => path, StringComparer.OrdinalIgnoreCase))
        {
            var key = Path.GetFileNameWithoutExtension(path);
            imageIndex.TryAdd(key, path);
        }

        return imageIndex;
    }

    private static int GetImagePathPriority(string path)
    {
        var normalized = path.Replace('/', '\\');
        var priority = 0;

        if (normalized.Contains("\\0000_INVALID_UNKNOWN\\", StringComparison.OrdinalIgnoreCase))
        {
            priority += 100;
        }

        if (normalized.Contains("INVALID", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("UNKNOWN", StringComparison.OrdinalIgnoreCase))
        {
            priority += 10;
        }

        return priority;
    }

    private static string ChildText(XElement parent, string name)
    {
        return parent.Element(name)?.Value.Trim() ?? string.Empty;
    }

    private static string CleanCardText(string text)
    {
        return HtmlTagRegex.Replace(text, string.Empty)
            .Replace("\\n", Environment.NewLine, StringComparison.Ordinal)
            .Replace("$", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Trim();
    }

    private static bool MatchesFilters(CardRecord card, SearchFilters filters)
    {
        if (card.IsEnchantment)
        {
            return false;
        }

        if (!CardDataMaps.IsKnownSet(card.TagMap.TryGetValue("CARD_SET", out var cardSet) ? cardSet : null))
        {
            return false;
        }

        if (!MatchesMode(cardSet, filters.Mode))
        {
            return false;
        }

        if (!MatchesCost(card, filters.Cost))
        {
            return false;
        }

        if (!MatchesExact(card, "CLASS", filters.Class))
        {
            return false;
        }

        if (!MatchesExact(card, "RARITY", filters.Rarity))
        {
            return false;
        }

        if (!MatchesExact(card, "CARDTYPE", filters.CardType))
        {
            return false;
        }

        if (!MatchesExact(card, "CARD_SET", filters.Set))
        {
            return false;
        }

        if (!MatchesExact(card, "CARDRACE", filters.Race))
        {
            return false;
        }

        if (!MatchesExact(card, "SPELL_SCHOOL", filters.School))
        {
            return false;
        }

        if (!MatchesCollectible(card, filters.Collectible))
        {
            return false;
        }

        if (!MatchesKeyword(card, filters.Keyword))
        {
            return false;
        }

        return true;
    }

    private static bool MatchesCost(CardRecord card, string? expected)
    {
        if (string.IsNullOrWhiteSpace(expected))
        {
            return true;
        }

        var actual = card.Cost ?? 0;
        return expected == "10"
            ? actual >= 10
            : int.TryParse(expected, out var parsed) && actual == parsed;
    }

    private static bool MatchesMode(string? actualSet, string? mode)
    {
        return string.IsNullOrWhiteSpace(mode) || CardDataMaps.MatchesMode(mode, actualSet);
    }

    private static bool MatchesExact(CardRecord card, string key, string? expected)
    {
        return string.IsNullOrWhiteSpace(expected)
            || card.TagMap.TryGetValue(key, out var actual) && actual == expected;
    }

    private static bool MatchesCollectible(CardRecord card, string? expected)
    {
        if (string.IsNullOrWhiteSpace(expected))
        {
            return true;
        }

        var actual = card.TagMap.TryGetValue("COLLECTIBLE", out var value) && value == "1" ? "1" : "0";
        return actual == expected;
    }

    private static bool MatchesKeyword(CardRecord card, string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return true;
        }

        return card.TagMap.TryGetValue(keyword, out var value) && value != "0";
    }

    private static bool MatchesQuery(CardRecord card, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        var colonIndex = query.IndexOf(':');
        if (colonIndex > 0 && colonIndex < query.Length - 1)
        {
            var left = query[..colonIndex].Trim().ToUpperInvariant();
            var right = query[(colonIndex + 1)..].Trim().ToLowerInvariant();
            if (left.Length == 0 || right.Length == 0)
            {
                return false;
            }

            if (left.All(char.IsAsciiDigit))
            {
                return card.EnumValues.TryGetValue(left, out var enumValue)
                    && enumValue.Contains(right, StringComparison.OrdinalIgnoreCase);
            }

            return card.TagMap.TryGetValue(left, out var tagValue)
                && tagValue.Contains(right, StringComparison.OrdinalIgnoreCase);
        }

        return (!string.IsNullOrWhiteSpace(card.NameZh) && card.NameZh.Contains(query, StringComparison.OrdinalIgnoreCase))
            || (!string.IsNullOrWhiteSpace(card.NameEn) && card.NameEn.Contains(query, StringComparison.OrdinalIgnoreCase))
            || card.CardId.Contains(query, StringComparison.OrdinalIgnoreCase)
            || card.DbfId.ToString() == query
            || (!string.IsNullOrWhiteSpace(card.TextZh) && card.TextZh.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ShouldSkipCard(IReadOnlyDictionary<string, string> tagMap)
    {
        return !tagMap.ContainsKey("CARD_SET");
    }

    private static void BuildRelationships(IReadOnlyList<CardRecord> cards)
    {
        var byDbfId = cards.ToDictionary(static card => card.DbfId);
        var byCardId = cards.ToDictionary(static card => card.CardId, StringComparer.Ordinal);

        foreach (var card in cards)
        {
            foreach (var pair in card.TagMap)
            {
                if (!IsRelatedKey(pair.Key) || !int.TryParse(pair.Value, out var targetDbfId) || !byDbfId.TryGetValue(targetDbfId, out var target))
                {
                    continue;
                }

                if (!target.ReverseRelated.Contains(card.DbfId))
                {
                    target.ReverseRelated.Add(card.DbfId);
                }

                if (card.ForwardRelated.All(link => link.DbfId != targetDbfId))
                {
                    card.ForwardRelated.Add(new ForwardRelatedRecord(targetDbfId, CardDataMaps.MapTagLabel(pair.Key)));
                }
            }

            var match = SuffixRegex.Match(card.CardId);
            if (!match.Success)
            {
                continue;
            }

            var parentCardId = match.Groups[1].Value;
            if (!byCardId.TryGetValue(parentCardId, out var parent) || parent.CardId == card.CardId)
            {
                continue;
            }

            if (!card.ReverseRelated.Contains(parent.DbfId))
            {
                card.ReverseRelated.Add(parent.DbfId);
            }

            if (parent.ForwardRelated.All(link => link.DbfId != card.DbfId))
            {
                parent.ForwardRelated.Add(new ForwardRelatedRecord(card.DbfId, "同名后缀衍生"));
            }
        }
    }

    public static bool IsRelatedKey(string tagKey)
    {
        return tagKey is "COLLECTION_RELATED_CARD_DATABASE_ID"
            or "HERO_POWER"
            or "DECK_RULE_COUNT_AS_COPY_OF_CARD_ID"
            or "MODIFIED_CARD_ID"
            or "LINKED_ENTITY"
            or "BATTLEGROUNDS_PREMIUM_DBF_ID"
            or "BATTLEGROUNDS_NORMAL_DBF_ID"
            or "DISPLAY_CARD_ON_MOUSEOVER"
            or "BACON_TRIPLE_CANDIDATE"
            or "HERO_POWER_DOUBLE"
            or "REPLACEMENT_ENTITY"
            || tagKey.Contains("RELATED", StringComparison.Ordinal);
    }
}
