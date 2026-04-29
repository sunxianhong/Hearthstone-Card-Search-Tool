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

    private enum CustomRelatedRuleKind
    {
        Add,
        Inherit,
    }

    private sealed record CustomRelatedRule(
        CustomRelatedRuleKind Kind,
        IReadOnlyList<string> Sources,
        IReadOnlyList<string> Targets);

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
        var enchantmentImagePath = ResolveEnchantmentImagePath(resourceRoot);

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
            if (IsEnchantmentCard(tagMap) && enchantmentImagePath is not null)
            {
                imagePath = enchantmentImagePath;
            }

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
                Sets = CardDataMaps.GetAllSets(),
                Races = MapToOptions(raceLabels),
                Schools = MapToOptions(schoolLabels),
            },
            imageIndex.Count > 0 || enchantmentImagePath is not null);
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
                parentCards.Add(new RelatedCardLink(parent.CardId, parent.DbfId, parent.NameZh, $"ID: {parent.DbfId}", parent.ImagePath));
            }
        }

        foreach (var forward in card.ForwardRelated.GroupBy(static item => item.DbfId).Select(static group => group.First()))
        {
            if (!byDbfId.TryGetValue(forward.DbfId, out var target))
            {
                continue;
            }

            var row = new RelatedCardLink(target.CardId, target.DbfId, target.NameZh, $"{forward.Reason} (ID: {target.DbfId})", target.ImagePath);
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
        if (tagMap.TryGetValue(key, out var code) && !string.IsNullOrWhiteSpace(code))
        {
            labels[code] = dictionary.TryGetValue(code, out var label)
                ? label
                : code;
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

    private static bool IsEnchantmentCard(IReadOnlyDictionary<string, string> tagMap)
    {
        return tagMap.TryGetValue("CARDTYPE", out var value) && value == "6";
    }

    private static string? ResolveEnchantmentImagePath(string resourceRoot)
    {
        var resourceImagePath = Path.GetFullPath(Path.Combine(resourceRoot, "enchantment.png"));
        if (File.Exists(resourceImagePath))
        {
            return resourceImagePath;
        }

        var appImagePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "enchantment.png"));
        return File.Exists(appImagePath)
            ? appImagePath
            : null;
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

        if (!MatchesMode(cardSet, filters))
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

    private static bool MatchesMode(string? actualSet, SearchFilters filters)
    {
        if (string.IsNullOrWhiteSpace(filters.Mode))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(actualSet)
            && filters.ModeSetValues is not null
            && filters.ModeSetValues.Contains(actualSet);
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

                AddForwardRelated(card, target, CardDataMaps.MapTagLabel(pair.Key));
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

            AddForwardRelated(parent, card, "同名后缀衍生");
        }

        ApplyCustomRelatedCardMappings(cards, byDbfId);
    }

    private static void ApplyCustomRelatedCardMappings(
        IReadOnlyList<CardRecord> cards,
        IReadOnlyDictionary<int, CardRecord> byDbfId)
    {
        var rules = ParseCustomRelatedRules();
        if (rules.Count == 0)
        {
            return;
        }

        var byCardId = new Dictionary<string, CardRecord>(StringComparer.OrdinalIgnoreCase);
        foreach (var card in cards)
        {
            byCardId.TryAdd(card.CardId, card);
        }

        var byNameZh = BuildNameIndex(cards, static card => card.NameZh, StringComparer.Ordinal);
        var byNameEn = BuildNameIndex(cards, static card => card.NameEn, StringComparer.OrdinalIgnoreCase);

        foreach (var rule in rules.Where(static rule => rule.Kind == CustomRelatedRuleKind.Add))
        {
            var sources = ResolveCards(rule.Sources, byCardId, byDbfId, byNameZh, byNameEn);
            var targets = ResolveCards(rule.Targets, byCardId, byDbfId, byNameZh, byNameEn);

            foreach (var source in sources)
            {
                foreach (var target in targets)
                {
                    AddForwardRelated(source, target, "自定义相关牌");
                }
            }
        }

        var inheritRules = rules.Where(static rule => rule.Kind == CustomRelatedRuleKind.Inherit).ToList();
        for (var iteration = 0; iteration <= inheritRules.Count; iteration++)
        {
            var addedAny = false;

            foreach (var rule in inheritRules)
            {
                var donors = ResolveCards(rule.Sources, byCardId, byDbfId, byNameZh, byNameEn);
                var receivers = ResolveCards(rule.Targets, byCardId, byDbfId, byNameZh, byNameEn);

                foreach (var receiver in receivers)
                {
                    foreach (var donor in donors)
                    {
                        if (receiver.DbfId == donor.DbfId)
                        {
                            continue;
                        }

                        foreach (var forward in donor.ForwardRelated.ToList())
                        {
                            if (!byDbfId.TryGetValue(forward.DbfId, out var target))
                            {
                                continue;
                            }

                            if (target.IsEnchantment)
                            {
                                continue;
                            }

                            var reason = $"继承自 {GetDisplayName(donor)}: {forward.Reason}";
                            addedAny |= AddForwardRelated(receiver, target, reason);
                        }
                    }
                }
            }

            if (!addedAny)
            {
                break;
            }
        }
    }

    private static List<CustomRelatedRule> ParseCustomRelatedRules()
    {
        var rules = new List<CustomRelatedRule>();

        foreach (var pair in CardDataMaps.RelatedCardMap)
        {
            if (!TryReadCustomRelatedRuleKey(pair.Key, out var kind, out var sourceText))
            {
                continue;
            }

            var sources = SplitCardIdentifiers(sourceText);
            var targets = SplitCardIdentifiers(pair.Value);
            if (sources.Count == 0 || targets.Count == 0)
            {
                continue;
            }

            rules.Add(new CustomRelatedRule(kind, sources, targets));
        }

        return rules;
    }

    private static bool TryReadCustomRelatedRuleKey(
        string key,
        out CustomRelatedRuleKind kind,
        out string sourceText)
    {
        var trimmed = key.Trim();
        if (trimmed.EndsWith("=>", StringComparison.Ordinal))
        {
            kind = CustomRelatedRuleKind.Add;
            sourceText = trimmed[..^2].Trim();
            return !string.IsNullOrWhiteSpace(sourceText);
        }

        if (trimmed.EndsWith("<=", StringComparison.Ordinal))
        {
            kind = CustomRelatedRuleKind.Inherit;
            sourceText = trimmed[..^2].Trim();
            return !string.IsNullOrWhiteSpace(sourceText);
        }

        kind = CustomRelatedRuleKind.Add;
        sourceText = string.Empty;
        return false;
    }

    private static IReadOnlyList<string> SplitCardIdentifiers(string text)
    {
        return text
            .Split([',', '，'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .ToList();
    }

    private static Dictionary<string, List<CardRecord>> BuildNameIndex(
        IEnumerable<CardRecord> cards,
        Func<CardRecord, string> selector,
        StringComparer comparer)
    {
        var index = new Dictionary<string, List<CardRecord>>(comparer);

        foreach (var card in cards)
        {
            var name = selector(card).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (!index.TryGetValue(name, out var matches))
            {
                matches = [];
                index[name] = matches;
            }

            matches.Add(card);
        }

        return index;
    }

    private static IReadOnlyList<CardRecord> ResolveCards(
        IReadOnlyList<string> identifiers,
        IReadOnlyDictionary<string, CardRecord> byCardId,
        IReadOnlyDictionary<int, CardRecord> byDbfId,
        IReadOnlyDictionary<string, List<CardRecord>> byNameZh,
        IReadOnlyDictionary<string, List<CardRecord>> byNameEn)
    {
        var resolved = new List<CardRecord>();
        var seen = new HashSet<int>();

        foreach (var identifier in identifiers)
        {
            foreach (var card in ResolveCard(identifier, byCardId, byDbfId, byNameZh, byNameEn))
            {
                if (seen.Add(card.DbfId))
                {
                    resolved.Add(card);
                }
            }
        }

        return resolved;
    }

    private static IReadOnlyList<CardRecord> ResolveCard(
        string identifier,
        IReadOnlyDictionary<string, CardRecord> byCardId,
        IReadOnlyDictionary<int, CardRecord> byDbfId,
        IReadOnlyDictionary<string, List<CardRecord>> byNameZh,
        IReadOnlyDictionary<string, List<CardRecord>> byNameEn)
    {
        var key = identifier.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            return [];
        }

        if (byCardId.TryGetValue(key, out var cardById))
        {
            return [cardById];
        }

        if (int.TryParse(key, out var dbfId) && byDbfId.TryGetValue(dbfId, out var cardByDbfId))
        {
            return [cardByDbfId];
        }

        if (byNameZh.TryGetValue(key, out var cardsByZhName))
        {
            return cardsByZhName;
        }

        if (byNameEn.TryGetValue(key, out var cardsByEnName))
        {
            return cardsByEnName;
        }

        return [];
    }

    private static bool AddForwardRelated(CardRecord source, CardRecord target, string reason)
    {
        if (source.DbfId == target.DbfId)
        {
            return false;
        }

        var added = false;
        if (source.ForwardRelated.All(link => link.DbfId != target.DbfId))
        {
            source.ForwardRelated.Add(new ForwardRelatedRecord(target.DbfId, reason));
            added = true;
        }

        if (!target.ReverseRelated.Contains(source.DbfId))
        {
            target.ReverseRelated.Add(source.DbfId);
        }

        return added;
    }

    private static string GetDisplayName(CardRecord card)
    {
        return string.IsNullOrWhiteSpace(card.NameZh)
            ? card.CardId
            : card.NameZh;
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
