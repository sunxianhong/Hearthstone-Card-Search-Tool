using HearthstoneCardSearchTool.Core;

namespace HearthstoneCardSearchTool.Tests;

public sealed class RepositoryTests
{
    private static readonly Lazy<CardRepository> Repository = new(LoadRepository);

    [Fact]
    public void LoadsCardsAndFilters()
    {
        var bootstrap = Repository.Value.Bootstrap;

        Assert.True(bootstrap.TotalCards > 1000);
        Assert.NotEmpty(bootstrap.Classes);
        Assert.NotEmpty(bootstrap.CardTypes);
        Assert.NotEmpty(bootstrap.Sets);
    }

    [Fact]
    public void SearchReturnsCardsForPlaceholderMode()
    {
        var placeholderResults = Repository.Value.Search(
            string.Empty,
            SearchFilters.Empty,
            200);

        Assert.NotEmpty(placeholderResults);
        Assert.Contains(placeholderResults, item => string.IsNullOrWhiteSpace(item.ImagePath));

        var resourceRoot = ResourceLocator.LocateResourceRoot(AppContext.BaseDirectory, Directory.GetCurrentDirectory());
        var imageCardId = Path.GetFileNameWithoutExtension(
            Directory.EnumerateFiles(Path.Combine(resourceRoot, "cardpng"), "*.png", SearchOption.AllDirectories).First());

        var imageResults = Repository.Value.Search(imageCardId, SearchFilters.Empty, 20);
        Assert.Contains(imageResults, item => !string.IsNullOrWhiteSpace(item.ImagePath));
    }

    [Fact]
    public void TagSearchAndDetailWork()
    {
        var results = Repository.Value.Search(
            "HEALTH:5",
            new SearchFilters
            {
                CardType = "4",
            },
            20);

        Assert.NotEmpty(results);

        var detail = Repository.Value.GetDetail(results[0].CardId);
        Assert.NotNull(detail);
        Assert.NotEmpty(detail!.Tags);
    }

    [Fact]
    public void EnumSearchWorks()
    {
        var results = Repository.Value.Search(
            "45:5",
            new SearchFilters
            {
                CardType = "4",
            },
            20);

        Assert.NotEmpty(results);
    }

    [Fact]
    public void LoadsLegacyTagDictionaryFromHtml()
    {
        var _ = Repository.Value;

        Assert.Equal("覆盖水印为特定扩展包", CardDataMaps.MapTagLabel("WATERMARK_OVERRIDE_CARD_SET"));
        Assert.Equal("大地的裂变", CardDataMaps.SetMap["1980"]);
        Assert.Equal("疯狂的暗月马戏团", CardDataMaps.SetMap["1466"]);
    }

    [Fact]
    public void BootstrapSetOptionsFollowXmlData()
    {
        var sets = Repository.Value.Bootstrap.Sets;

        Assert.Contains(sets, item => item.Value == "1980" && item.Label == "大地的裂变");
        Assert.DoesNotContain(sets, item => item.Value == "22");
        Assert.DoesNotContain(sets, item => item.Value == "7");
        Assert.DoesNotContain(sets, item => item.Value == "8");
    }

    [Fact]
    public void EnchantmentTypeDisplaysLikeLegacyHtml()
    {
        Assert.Equal("附魔 (6)", CardDataMaps.MapTagValue("CARDTYPE", "6"));
    }

    private static CardRepository LoadRepository()
    {
        var resourceRoot = ResourceLocator.LocateResourceRoot(AppContext.BaseDirectory, Directory.GetCurrentDirectory());
        return CardRepository.Load(resourceRoot);
    }
}
