namespace HearthstoneCardSearchTool.Web;

public sealed record OptionDto(string Value, string Label);

public sealed record BootstrapResponse(
    string AppName,
    int TotalCards,
    bool HasAnyImages,
    int MaxDisplay,
    IReadOnlyList<OptionDto> Modes,
    IReadOnlyList<OptionDto> Costs,
    IReadOnlyList<OptionDto> Classes,
    IReadOnlyList<OptionDto> Rarities,
    IReadOnlyList<OptionDto> CardTypes,
    IReadOnlyList<OptionDto> WildSets,
    IReadOnlyList<OptionDto> StandardSets,
    IReadOnlyList<OptionDto> Races,
    IReadOnlyList<OptionDto> Schools,
    IReadOnlyList<OptionDto> CollectibleOptions,
    IReadOnlyList<OptionDto> KeywordOptions);

public sealed record SearchRequest(
    string? Query,
    string? Mode,
    string? Cost,
    string? Class,
    string? Set,
    string? Rarity,
    string? CardType,
    string? Race,
    string? School,
    string? Collectible,
    string? Keyword,
    int? Limit);

public sealed record SearchResponse(
    int TotalCards,
    int DisplayedCount,
    int MaxDisplay,
    string SearchMode,
    IReadOnlyList<CardSummaryDto> Items);

public sealed record CardSummaryDto(
    string CardId,
    int DbfId,
    string NameZh,
    string NameEn,
    string TextZh,
    string Subtitle,
    bool HasImage,
    string? ImageUrl);

public sealed record CardDetailDto(
    string CardId,
    int DbfId,
    string Name,
    string Text,
    bool HasImage,
    string? ImageUrl,
    bool IsEnchantment,
    IReadOnlyList<RelatedCardDto> ParentCards,
    IReadOnlyList<RelatedCardDto> RelatedCards,
    IReadOnlyList<RelatedCardDto> EnchantmentCards,
    IReadOnlyList<CardTagDto> Tags);

public sealed record RelatedCardDto(
    string CardId,
    int DbfId,
    string Name,
    string Reason,
    bool HasImage,
    string? ImageUrl);

public sealed record CardTagDto(
    string Key,
    string DisplayName,
    string Value,
    string? EnumId,
    string? TargetCardId,
    int? TargetDbfId);

public sealed class RepositoryState
{
    public required string ResourceRoot { get; init; }
    public required string ImageRoot { get; init; }
    public required HearthstoneCardSearchTool.Core.CardRepository Repository { get; init; }
}
