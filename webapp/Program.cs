using HearthstoneCardSearchTool.Core;
using HearthstoneCardSearchTool.Web;

const int DefaultMaxDisplay = 300;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(_ =>
{
    var resourceRoot = ResolveResourceRoot(builder.Configuration, builder.Environment);
    return new RepositoryState
    {
        ResourceRoot = resourceRoot,
        ImageRoot = Path.GetFullPath(Path.Combine(resourceRoot, "cardpng")),
        Repository = CardRepository.Load(resourceRoot),
    };
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/health", (RepositoryState state) =>
{
    return Results.Ok(new
    {
        status = "ok",
        totalCards = state.Repository.Bootstrap.TotalCards,
        hasAnyImages = state.Repository.HasAnyImages,
    });
});

app.MapGet("/api/bootstrap", (RepositoryState state) =>
{
    return Results.Ok(
        new BootstrapResponse(
            "炉石卡牌检索器",
            state.Repository.Bootstrap.TotalCards,
            state.Repository.HasAnyImages,
            DefaultMaxDisplay,
            BuildModeOptions(),
            BuildCostOptions(),
            BuildMappedOptions(CardDataMaps.ClassMap, state.Repository.Bootstrap.Classes.Select(static item => item.Value)),
            BuildMappedOptions(CardDataMaps.RarityMap, state.Repository.Bootstrap.Rarities.Select(static item => item.Value)),
            BuildMappedOptions(CardDataMaps.CardTypeMap, state.Repository.Bootstrap.CardTypes.Select(static item => item.Value)),
            BuildPresentOptions(CardDataMaps.GetSetsForMode("wild")),
            BuildPresentOptions(CardDataMaps.GetSetsForMode("standard")),
            BuildPresentOptions(state.Repository.Bootstrap.Races),
            BuildPresentOptions(state.Repository.Bootstrap.Schools),
            BuildCollectibleOptions(),
            BuildKeywordOptions()));
});

app.MapGet("/api/cards", ([AsParameters] SearchRequest request, RepositoryState state) =>
{
    var filters = new SearchFilters
    {
        Mode = NormalizeMode(request.Mode),
        Cost = NullIfWhiteSpace(request.Cost),
        Class = NullIfWhiteSpace(request.Class),
        Set = NullIfWhiteSpace(request.Set),
        Rarity = NullIfWhiteSpace(request.Rarity),
        CardType = NullIfWhiteSpace(request.CardType),
        Race = NullIfWhiteSpace(request.Race),
        School = NullIfWhiteSpace(request.School),
        Collectible = NullIfWhiteSpace(request.Collectible),
        Keyword = NullIfWhiteSpace(request.Keyword),
    };

    var limit = Math.Clamp(request.Limit ?? DefaultMaxDisplay, 1, DefaultMaxDisplay);
    var results = state.Repository.Search(request.Query ?? string.Empty, filters, limit);

    return Results.Ok(
        new SearchResponse(
            state.Repository.Bootstrap.TotalCards,
            results.Count,
            limit,
            DescribeSearchMode(request.Query),
            results.Select(MapCardSummary).ToList()));
});

app.MapGet("/api/cards/{cardId}", (string cardId, RepositoryState state) =>
{
    var detail = state.Repository.GetDetail(cardId);
    if (detail is null)
    {
        return Results.NotFound();
    }

    return Results.Ok(
        new CardDetailDto(
            detail.CardId,
            detail.DbfId,
            detail.Name,
            detail.Text,
            HasImage(detail.ImagePath),
            BuildImageUrl(detail.CardId, detail.ImagePath),
            detail.IsEnchantment,
            detail.ParentCards.Select(MapRelatedCard).ToList(),
            detail.RelatedCards.Select(MapRelatedCard).ToList(),
            detail.EnchantmentCards.Select(MapRelatedCard).ToList(),
            detail.Tags.Select(tag => new CardTagDto(tag.Key, tag.DisplayName, tag.Value, tag.EnumId, tag.TargetCardId, tag.TargetDbfId)).ToList()));
});

app.MapGet("/api/cards/{cardId}/image", (string cardId, RepositoryState state) =>
{
    var detail = state.Repository.GetDetail(cardId);
    if (detail is null || !HasImage(detail.ImagePath))
    {
        return Results.NotFound();
    }

    var imagePath = Path.GetFullPath(detail.ImagePath!);
    if (!imagePath.StartsWith(state.ImageRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(imagePath))
    {
        return Results.NotFound();
    }

    return Results.File(imagePath, "image/png", enableRangeProcessing: true);
});

app.MapFallbackToFile("index.html");

app.Run();

static string ResolveResourceRoot(IConfiguration configuration, IWebHostEnvironment environment)
{
    var configuredRoot = configuration["CARD_RESOURCE_ROOT"]
        ?? configuration["CardResourceRoot"]
        ?? configuration["ResourceRoot"];

    if (!string.IsNullOrWhiteSpace(configuredRoot))
    {
        var absolutePath = Path.IsPathRooted(configuredRoot)
            ? configuredRoot
            : Path.GetFullPath(Path.Combine(environment.ContentRootPath, configuredRoot));

        return ResourceLocator.LocateResourceRoot(absolutePath);
    }

    return ResourceLocator.LocateResourceRoot(
        environment.ContentRootPath,
        AppContext.BaseDirectory,
        Directory.GetCurrentDirectory());
}

static string NormalizeMode(string? mode)
{
    return string.Equals(mode, "standard", StringComparison.OrdinalIgnoreCase)
        ? "standard"
        : "wild";
}

static string? NullIfWhiteSpace(string? value)
{
    return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

static IReadOnlyList<OptionDto> BuildModeOptions()
{
    return
    [
        new OptionDto("wild", "狂野"),
        new OptionDto("standard", "标准"),
    ];
}

static IReadOnlyList<OptionDto> BuildCostOptions()
{
    var items = new List<OptionDto>
    {
        new(string.Empty, "法力值"),
    };

    for (var cost = 0; cost <= 9; cost++)
    {
        items.Add(new OptionDto(cost.ToString(), cost.ToString()));
    }

    items.Add(new OptionDto("10", "10+"));
    return items;
}

static IReadOnlyList<OptionDto> BuildCollectibleOptions()
{
    return
    [
        new OptionDto(string.Empty, "是否可收藏"),
        new OptionDto("1", "可收藏"),
        new OptionDto("0", "不可收藏"),
    ];
}

static IReadOnlyList<OptionDto> BuildKeywordOptions()
{
    return
    [
        new OptionDto(string.Empty, "关键词"),
        new OptionDto("BATTLECRY", "战吼"),
        new OptionDto("TAUNT", "嘲讽"),
        new OptionDto("DIVINE_SHIELD", "圣盾"),
        new OptionDto("DEATHRATTLE", "亡语"),
        new OptionDto("DISCOVER", "发现"),
        new OptionDto("RUSH", "突袭"),
        new OptionDto("LIFESTEAL", "吸血"),
        new OptionDto("WINDFURY", "风怒"),
        new OptionDto("STEALTH", "潜行"),
    ];
}

static IReadOnlyList<OptionDto> BuildMappedOptions(
    IReadOnlyDictionary<string, string> map,
    IEnumerable<string> presentValues)
{
    var present = new HashSet<string>(presentValues.Where(static value => !string.IsNullOrWhiteSpace(value)), StringComparer.Ordinal);

    return map
        .Where(pair => present.Contains(pair.Key))
        .Select(pair => BuildLabeledOption(pair.Key, pair.Value))
        .OrderBy(static item => SortKey(item.Value))
        .ThenBy(static item => item.Label, StringComparer.Ordinal)
        .ToList();
}

static IReadOnlyList<OptionDto> BuildPresentOptions(IEnumerable<FilterOption> items)
{
    return items
        .Select(static item => BuildLabeledOption(item.Value, item.Label))
        .OrderBy(static item => SortKey(item.Value))
        .ThenBy(static item => item.Label, StringComparer.Ordinal)
        .ToList();
}

static OptionDto BuildLabeledOption(string value, string label)
{
    return new OptionDto(value, $"{label.Trim()} ({value})");
}

static int SortKey(string value)
{
    return int.TryParse(value, out var parsed)
        ? parsed
        : int.MaxValue;
}

static CardSummaryDto MapCardSummary(CardRecord card)
{
    return new CardSummaryDto(
        card.CardId,
        card.DbfId,
        card.NameZh,
        card.NameEn,
        card.TextZh,
        BuildSummary(card),
        HasImage(card.ImagePath),
        BuildImageUrl(card.CardId, card.ImagePath));
}

static RelatedCardDto MapRelatedCard(RelatedCardLink link)
{
    return new RelatedCardDto(
        link.CardId,
        link.DbfId,
        link.Name,
        link.Reason,
        HasImage(link.ImagePath),
        BuildImageUrl(link.CardId, link.ImagePath));
}

static string BuildSummary(CardRecord card)
{
    var parts = new List<string>();

    if (card.Cost.HasValue)
    {
        parts.Add($"法力值 {card.Cost.Value}");
    }

    TryAppendMappedTag(card, parts, "CARDTYPE");
    TryAppendMappedTag(card, parts, "CLASS");
    TryAppendMappedTag(card, parts, "CARD_SET");

    return string.Join(" · ", parts);
}

static void TryAppendMappedTag(CardRecord card, ICollection<string> parts, string key)
{
    if (!card.TagMap.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
    {
        return;
    }

    parts.Add(CardDataMaps.MapTagValue(key, value));
}

static bool HasImage(string? imagePath)
{
    return !string.IsNullOrWhiteSpace(imagePath);
}

static string? BuildImageUrl(string cardId, string? imagePath)
{
    return HasImage(imagePath)
        ? $"/api/cards/{Uri.EscapeDataString(cardId)}/image"
        : null;
}

static string DescribeSearchMode(string? query)
{
    var input = (query ?? string.Empty).Trim();
    if (string.IsNullOrWhiteSpace(input))
    {
        return "全库浏览";
    }

    var parts = input.Split(':', 2);
    if (parts.Length == 2)
    {
        return parts[0].Trim().All(char.IsAsciiDigit)
            ? "EnumID 搜索"
            : "标签搜索";
    }

    return "普通搜索";
}
