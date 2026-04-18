using System.Collections.ObjectModel;

namespace HearthstoneCardSearchTool.Core;

public sealed record FilterOption(string Value, string Label);

public sealed class SearchFilters
{
    public string? Mode { get; init; }
    public string? Cost { get; init; }
    public string? Class { get; init; }
    public string? Rarity { get; init; }
    public string? CardType { get; init; }
    public string? Set { get; init; }
    public string? Collectible { get; init; }
    public string? Race { get; init; }
    public string? School { get; init; }
    public string? Keyword { get; init; }

    public static SearchFilters Empty { get; } = new();
}

public sealed record CardTagRecord(string Key, string Value, string? EnumId);

public sealed record ForwardRelatedRecord(int DbfId, string Reason);

public sealed class CardRecord
{
    public CardRecord(
        string cardId,
        int dbfId,
        string nameZh,
        string nameEn,
        string textZh,
        string? imagePath,
        IReadOnlyList<CardTagRecord> tags,
        IReadOnlyDictionary<string, string> tagMap,
        IReadOnlyDictionary<string, string> enumValues)
    {
        CardId = cardId;
        DbfId = dbfId;
        NameZh = nameZh;
        NameEn = nameEn;
        TextZh = textZh;
        ImagePath = imagePath;
        Tags = tags;
        TagMap = tagMap;
        EnumValues = enumValues;
    }

    public string CardId { get; }
    public int DbfId { get; }
    public string NameZh { get; }
    public string NameEn { get; }
    public string TextZh { get; }
    public string? ImagePath { get; }
    public IReadOnlyList<CardTagRecord> Tags { get; }
    public IReadOnlyDictionary<string, string> TagMap { get; }
    public IReadOnlyDictionary<string, string> EnumValues { get; }
    public Collection<int> ReverseRelated { get; } = new();
    public Collection<ForwardRelatedRecord> ForwardRelated { get; } = new();

    public bool IsEnchantment => TagMap.TryGetValue("CARDTYPE", out var value) && value == "6";

    public int? Cost =>
        TagMap.TryGetValue("COST", out var value) && int.TryParse(value, out var parsed)
            ? parsed
            : null;
}

public sealed record RelatedCardLink(string CardId, int DbfId, string Name, string Reason);

public sealed record CardTagView(
    string Key,
    string DisplayName,
    string Value,
    string? EnumId,
    string? TargetCardId,
    int? TargetDbfId);

public sealed class CardDetailData
{
    public required string CardId { get; init; }
    public required int DbfId { get; init; }
    public required string Name { get; init; }
    public required string Text { get; init; }
    public required string? ImagePath { get; init; }
    public required bool IsEnchantment { get; init; }
    public required IReadOnlyList<RelatedCardLink> ParentCards { get; init; }
    public required IReadOnlyList<RelatedCardLink> RelatedCards { get; init; }
    public required IReadOnlyList<RelatedCardLink> EnchantmentCards { get; init; }
    public required IReadOnlyList<CardTagView> Tags { get; init; }
}

public sealed class BootstrapData
{
    public required int TotalCards { get; init; }
    public required IReadOnlyList<FilterOption> Classes { get; init; }
    public required IReadOnlyList<FilterOption> Rarities { get; init; }
    public required IReadOnlyList<FilterOption> CardTypes { get; init; }
    public required IReadOnlyList<FilterOption> Sets { get; init; }
    public required IReadOnlyList<FilterOption> Races { get; init; }
    public required IReadOnlyList<FilterOption> Schools { get; init; }
}
