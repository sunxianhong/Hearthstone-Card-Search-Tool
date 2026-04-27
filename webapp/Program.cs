using HearthstoneCardSearchTool.Core;
using HearthstoneCardSearchTool.Web;

const int DefaultMaxDisplay = 300;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<CardDataMapConfigStore>();
builder.Services.AddSingleton(sp =>
{
    var resourceRoot = ResolveResourceRoot(builder.Configuration, builder.Environment);
    var cardDataMapConfigStore = sp.GetRequiredService<CardDataMapConfigStore>();
    var cardDataMapOverrides = cardDataMapConfigStore.LoadAsync().GetAwaiter().GetResult();
    CardDataMaps.ApplyOverrides(cardDataMapOverrides);

    return new RepositoryState
    {
        ResourceRoot = resourceRoot,
        ImageRoot = Path.GetFullPath(Path.Combine(resourceRoot, "cardpng")),
        Repository = CardRepository.Load(resourceRoot),
    };
});
builder.Services.AddSingleton<FilterBarConfigStore>();

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
            BuildMappedOptions(
                CardDataMaps.ClassMap,
                state.Repository.Bootstrap.Classes.Select(static item => item.Value),
                state.Repository.Bootstrap.Classes),
            BuildMappedOptions(
                CardDataMaps.RarityMap,
                state.Repository.Bootstrap.Rarities.Select(static item => item.Value),
                state.Repository.Bootstrap.Rarities),
            BuildMappedOptions(
                CardDataMaps.CardTypeMap,
                state.Repository.Bootstrap.CardTypes.Select(static item => item.Value),
                state.Repository.Bootstrap.CardTypes),
            BuildPresentOptions(CardDataMaps.GetAllSets()),
            BuildPresentOptions(CardDataMaps.GetAllSets()),
            BuildMappedOptions(
                CardDataMaps.RaceMap,
                state.Repository.Bootstrap.Races.Select(static item => item.Value),
                state.Repository.Bootstrap.Races),
            BuildMappedOptions(
                CardDataMaps.SchoolMap,
                state.Repository.Bootstrap.Schools.Select(static item => item.Value),
                state.Repository.Bootstrap.Schools),
            BuildCollectibleOptions(),
            BuildKeywordOptions()));
});

app.MapGet("/api/card-data-maps", async (CardDataMapConfigStore store, CancellationToken cancellationToken) =>
{
    var config = await store.LoadAsync(cancellationToken);
    CardDataMaps.ApplyOverrides(config);
    return Results.Ok(BuildCardDataMapConfigResponse(config));
});

app.MapPut("/api/card-data-maps", async (CardDataMapOverrideConfig config, CardDataMapConfigStore store, CancellationToken cancellationToken) =>
{
    var saved = await store.SaveAsync(config, cancellationToken);
    CardDataMaps.ApplyOverrides(saved);
    return Results.Ok(BuildCardDataMapConfigResponse(saved));
});

app.MapGet("/api/filter-bar-config", async (RepositoryState state, FilterBarConfigStore store, CancellationToken cancellationToken) =>
{
    var config = await store.LoadAsync(() => BuildDefaultFilterBarConfig(state), cancellationToken);
    return Results.Ok(config);
});

app.MapGet("/api/filter-bar-config/default", (RepositoryState state) =>
{
    return Results.Ok(BuildDefaultFilterBarConfig(state));
});

app.MapPut("/api/filter-bar-config", async (FilterBarConfig config, RepositoryState state, FilterBarConfigStore store, CancellationToken cancellationToken) =>
{
    var saved = await store.SaveAsync(config, () => BuildDefaultFilterBarConfig(state), cancellationToken);
    return Results.Ok(saved);
});

app.MapGet("/api/cards", async ([AsParameters] SearchRequest request, RepositoryState state, FilterBarConfigStore store, CancellationToken cancellationToken) =>
{
    var normalizedMode = NullIfWhiteSpace(request.Mode) is { } rawMode
        ? NormalizeMode(rawMode)
        : null;

    var filterBarConfig = await store.LoadAsync(() => BuildDefaultFilterBarConfig(state), cancellationToken);
    var filters = new SearchFilters
    {
        Mode = normalizedMode,
        ModeSetValues = ResolveModeSetValues(filterBarConfig, normalizedMode),
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

static IReadOnlySet<string>? ResolveModeSetValues(FilterBarConfig config, string? mode)
{
    if (string.IsNullOrWhiteSpace(mode))
    {
        return null;
    }

    var section = config.Sections.FirstOrDefault(section => section.Key.Equals("set", StringComparison.OrdinalIgnoreCase));
    if (section is null)
    {
        return new HashSet<string>(StringComparer.Ordinal);
    }

    var isStandardMode = string.Equals(mode, "standard", StringComparison.OrdinalIgnoreCase);
    return section.Options
        .Where(option => isStandardMode ? option.VisibleInStandard == true : option.VisibleInWild == true)
        .Select(static option => option.Value)
        .ToHashSet(StringComparer.Ordinal);
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
        new OptionDto(string.Empty, "是否可收集"),
        new OptionDto("1", "可收集"),
        new OptionDto("0", "不可收集"),
    ];
}

static IReadOnlyList<OptionDto> BuildKeywordOptions()
{
    return
    [
        new OptionDto(string.Empty, "关键字"),
        new OptionDto("BATTLECRY", "战吼"),
        new OptionDto("TAUNT", "嘲讽"),
        new OptionDto("DIVINE_SHIELD", "圣盾"),
        new OptionDto("DEATHRATTLE", "亡语"),
        new OptionDto("DISCOVER", "发现"),
        new OptionDto("RUSH", "突袭"),
        new OptionDto("LIFESTEAL", "吸血"),
        new OptionDto("WINDFURY", "风怒"),
        new OptionDto("STEALTH", "潜行"),
        new OptionDto("POISONOUS", "剧毒"),
        new OptionDto("AURA", "光环"),
        new OptionDto("TRIGGER_VISUAL", "特效"),
    ];
}

static IReadOnlyList<OptionDto> BuildMappedOptions(
    IReadOnlyDictionary<string, string> map,
    IEnumerable<string> presentValues,
    IEnumerable<FilterOption>? fallbackOptions = null)
{
    var present = new HashSet<string>(presentValues.Where(static value => !string.IsNullOrWhiteSpace(value)), StringComparer.Ordinal);
    var fallbackLabels = fallbackOptions?
        .ToDictionary(static item => item.Value, static item => item.Label, StringComparer.Ordinal)
        ?? new Dictionary<string, string>(StringComparer.Ordinal);

    return present
        .Select(value =>
        {
            var label = map.TryGetValue(value, out var mappedLabel)
                ? mappedLabel
                : fallbackLabels.TryGetValue(value, out var fallbackLabel)
                    ? fallbackLabel
                    : value;

            return BuildLabeledOption(value, label);
        })
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

static FilterBarConfig BuildDefaultFilterBarConfig(RepositoryState state)
{
    return new FilterBarConfig(
    [
        BuildFilterBarSection("mode", "模式", BuildModeOptions()),
        BuildFilterBarSection("set", "扩展包", BuildPresentOptions(CardDataMaps.GetAllSets()), supportsModeVisibility: true),
        BuildFilterBarSection("cost", "法力值", BuildCostOptions()),
        BuildFilterBarSection(
            "class",
            "职业",
            BuildMappedOptions(
                CardDataMaps.ClassMap,
                state.Repository.Bootstrap.Classes.Select(static item => item.Value),
                state.Repository.Bootstrap.Classes)),
        BuildFilterBarSection(
            "rarity",
            "稀有度",
            BuildMappedOptions(
                CardDataMaps.RarityMap,
                state.Repository.Bootstrap.Rarities.Select(static item => item.Value),
                state.Repository.Bootstrap.Rarities)),
        BuildFilterBarSection(
            "cardType",
            "卡片类型",
            BuildMappedOptions(
                CardDataMaps.CardTypeMap,
                state.Repository.Bootstrap.CardTypes.Select(static item => item.Value),
                state.Repository.Bootstrap.CardTypes)),
        BuildFilterBarSection(
            "race",
            "随从种族",
            BuildMappedOptions(
                CardDataMaps.RaceMap,
                state.Repository.Bootstrap.Races.Select(static item => item.Value),
                state.Repository.Bootstrap.Races)),
        BuildFilterBarSection(
            "school",
            "法术派系",
            BuildMappedOptions(
                CardDataMaps.SchoolMap,
                state.Repository.Bootstrap.Schools.Select(static item => item.Value),
                state.Repository.Bootstrap.Schools)),
        BuildFilterBarSection("keyword", "关键词", BuildKeywordOptions()),
        BuildFilterBarSection("collectible", "是否可收藏", BuildCollectibleOptions()),
    ]);
}

static FilterBarSectionConfig BuildFilterBarSection(
    string key,
    string label,
    IEnumerable<OptionDto> options,
    bool supportsModeVisibility = false)
{
    return new FilterBarSectionConfig(
        key,
        label,
        Enabled: true,
        options
            .Where(static option => !string.IsNullOrWhiteSpace(option.Value))
            .Select(option => new FilterBarOptionConfig(
                option.Value,
                option.Label,
                Visible: true,
                VisibleInWild: supportsModeVisibility,
                VisibleInStandard: supportsModeVisibility))
            .ToList());
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

static CardDataMapConfigResponse BuildCardDataMapConfigResponse(CardDataMapOverrideConfig overrides)
{
    return new CardDataMapConfigResponse(
    [
        BuildCardDataMapLibrary(
            "unknownEnumMap",
            "未知 EnumID 显示名",
            "用于补充卡牌详情里未知 enumID 的中文显示名。只需要填写新增或覆盖的条目。",
            CardDataMaps.DefaultUnknownEnumMap,
            overrides.UnknownEnumMap),
        BuildCardDataMapLibrary(
            "tagLabels",
            "标签中文名",
            "用于给卡牌标签 key 补中文名。适合在游戏更新后补充新的 Tag 名称。",
            CardDataMaps.DefaultTagLabels,
            overrides.TagLabels),
        BuildCardDataMapLibrary(
            "classMap",
            "职业映射",
            "用于职业筛选、详情标签和搜索摘要显示。保存后页面会立即刷新对应中文名。",
            CardDataMaps.DefaultClassMap,
            overrides.ClassMap),
        BuildCardDataMapLibrary(
            "rarityMap",
            "稀有度映射",
            "用于稀有度筛选和详情标签显示。",
            CardDataMaps.DefaultRarityMap,
            overrides.RarityMap),
        BuildCardDataMapLibrary(
            "raceMap",
            "种族映射",
            "用于随从种族筛选和详情标签显示。",
            CardDataMaps.DefaultRaceMap,
            overrides.RaceMap),
        BuildCardDataMapLibrary(
            "schoolMap",
            "法术派系映射",
            "用于法术派系筛选和详情标签显示。",
            CardDataMaps.DefaultSchoolMap,
            overrides.SchoolMap),
        BuildCardDataMapLibrary(
            "setMap",
            "扩展包映射",
            "用于扩展包中文名、扩展包筛选显示名和详情标签显示。新版本补包时优先在这里维护。",
            CardDataMaps.DefaultSetMap,
            overrides.SetMap),
    ]);
}

static CardDataMapLibraryDto BuildCardDataMapLibrary(
    string key,
    string label,
    string description,
    IReadOnlyDictionary<string, string> defaults,
    IReadOnlyDictionary<string, string>? overrides)
{
    var normalizedOverrides = NormalizeMap(overrides);
    var effective = MergeMaps(defaults, normalizedOverrides);

    return new CardDataMapLibraryDto(
        key,
        label,
        description,
        defaults.Count,
        normalizedOverrides.Count,
        effective.Count,
        normalizedOverrides,
        defaults,
        effective);
}

static Dictionary<string, string> NormalizeMap(IReadOnlyDictionary<string, string>? source)
{
    var normalized = new Dictionary<string, string>(StringComparer.Ordinal);
    if (source is null)
    {
        return normalized;
    }

    foreach (var pair in source)
    {
        var key = pair.Key.Trim();
        var value = pair.Value.Trim();
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
        {
            continue;
        }

        normalized[key] = value;
    }

    return normalized;
}

static Dictionary<string, string> MergeMaps(
    IReadOnlyDictionary<string, string> defaults,
    IReadOnlyDictionary<string, string> overrides)
{
    var merged = new Dictionary<string, string>(defaults, StringComparer.Ordinal);
    foreach (var pair in overrides)
    {
        merged[pair.Key] = pair.Value;
    }

    return merged;
}
