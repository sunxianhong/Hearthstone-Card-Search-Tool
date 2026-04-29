using HearthstoneCardSearchTool.Core;
using System.Xml.Linq;

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
        var firstImagePath = Directory.EnumerateFiles(Path.Combine(resourceRoot, "cardpng"), "*.png", SearchOption.AllDirectories).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(firstImagePath))
        {
            return;
        }

        var imageCardId = Path.GetFileNameWithoutExtension(firstImagePath);

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
    public void UsesFallbackMappings()
    {
        var _ = Repository.Value;

        Assert.Equal("覆盖水印为特定扩展包", CardDataMaps.MapTagLabel("WATERMARK_OVERRIDE_CARD_SET"));
        Assert.Equal("大地的裂变", CardDataMaps.SetMap["1980"]);
        Assert.Equal("疯狂的暗月马戏团", CardDataMaps.SetMap["1466"]);
    }

    [Fact]
    public void BootstrapSetOptionsFollowFallbackMap()
    {
        var sets = Repository.Value.Bootstrap.Sets;

        Assert.Equal(CardDataMaps.GetAllSets(), sets);
        Assert.Contains(sets, item => item.Value == "1980" && item.Label == "大地的裂变");
        Assert.Contains(sets, item => item.Value == "1637" && item.Label == "核心");
        Assert.Contains(sets, item => item.Value == "1941" && item.Label == "活动礼物");
        Assert.Contains(sets, item => item.Value == "22");
        Assert.Contains(sets, item => item.Value == "7");
        Assert.Contains(sets, item => item.Value == "8");
    }

    [Fact]
    public void StandardAndWildModeSetListsReturnAllSets()
    {
        var standardSets = CardDataMaps.GetSetsForMode("standard");
        var wildSets = CardDataMaps.GetSetsForMode("wild");

        Assert.Contains(standardSets, item => item.Value == "1637");
        Assert.Contains(standardSets, item => item.Value == "1941");
        Assert.Contains(standardSets, item => item.Value == "1980");
        Assert.Contains(standardSets, item => item.Value == "1466");
        Assert.Contains(wildSets, item => item.Value == "1941");
        Assert.Contains(wildSets, item => item.Value == "22");
    }

    [Fact]
    public void EnchantmentTypeUsesFallbackDisplay()
    {
        Assert.Equal("附魔 (6)", CardDataMaps.MapTagValue("CARDTYPE", "6"));
    }

    [Fact]
    public void KeywordOptionsFollowFallbackMap()
    {
        var keywords = CardDataMaps.GetAllKeywords();

        Assert.Contains(keywords, item => item.Value == "BATTLECRY" && item.Label == "\u6218\u543c");
        Assert.Contains(keywords, item => item.Value == "TRIGGER_VISUAL" && item.Label == "\u7279\u6548");
    }

    [Fact]
    public void EnchantmentCardsUseSharedEnchantmentImage()
    {
        var resourceRoot = ResourceLocator.LocateResourceRoot(AppContext.BaseDirectory, Directory.GetCurrentDirectory());
        var enchantmentImagePath = Path.Combine(resourceRoot, "enchantment.png");
        var xmlPath = Path.Combine(resourceRoot, "CardDefs.xml");
        var enchantmentCardId = XDocument.Load(xmlPath)
            .Descendants("Entity")
            .FirstOrDefault(entity => entity.Elements("Tag").Any(tag =>
                tag.Attribute("name")?.Value == "CARDTYPE"
                && tag.Attribute("value")?.Value == "6"))
            ?.Attribute("CardID")
            ?.Value;
        var detail = enchantmentCardId is null
            ? null
            : Repository.Value.GetDetail(enchantmentCardId);

        Assert.False(string.IsNullOrWhiteSpace(enchantmentCardId));
        Assert.True(File.Exists(enchantmentImagePath));
        Assert.NotNull(detail);
        Assert.Equal(enchantmentImagePath, detail!.ImagePath);
    }

    [Fact]
    public void CustomRelatedMappingsAddAndInheritRelatedCards()
    {
        var resourceRoot = CreateTemporaryCardDataRoot();
        CardDataMaps.ApplyOverrides(
            new CardDataMapOverrideConfig
            {
                RelatedCardMap = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["CARD_A=>"] = "卡牌C,CARD_D,CARD_E",
                    ["CARD_A<="] = "卡牌B",
                },
            });

        try
        {
            var repository = CardRepository.Load(resourceRoot);

            var detailA = repository.GetDetail("CARD_A");
            var detailB = repository.GetDetail("CARD_B");
            var detailC = repository.GetDetail("CARD_C");
            var detailE = repository.GetDetail("CARD_E");

            Assert.NotNull(detailA);
            Assert.NotNull(detailB);
            Assert.NotNull(detailC);
            Assert.NotNull(detailE);

            Assert.Contains(detailA!.RelatedCards, item => item.CardId == "CARD_C");
            Assert.Contains(detailA.RelatedCards, item => item.CardId == "CARD_D");
            Assert.Contains(detailA.EnchantmentCards, item => item.CardId == "CARD_E");
            Assert.Contains(detailB!.RelatedCards, item => item.CardId == "CARD_C");
            Assert.Contains(detailB.RelatedCards, item => item.CardId == "CARD_D");
            Assert.DoesNotContain(detailB.EnchantmentCards, item => item.CardId == "CARD_E");
            Assert.Contains(detailC!.ParentCards, item => item.CardId == "CARD_A");
            Assert.Contains(detailC.ParentCards, item => item.CardId == "CARD_B");
            Assert.Contains(detailE!.ParentCards, item => item.CardId == "CARD_A");
            Assert.DoesNotContain(detailE.ParentCards, item => item.CardId == "CARD_B");
        }
        finally
        {
            CardDataMaps.ResetOverrides();
            Directory.Delete(resourceRoot, recursive: true);
        }
    }

    [Fact]
    public void SourceDefaultMappingsBecomeDefaultRelatedCards()
    {
        var resourceRoot = CreateTemporaryCardDataRoot();
        var configDirectory = Path.Combine(resourceRoot, "config");
        Directory.CreateDirectory(configDirectory);
        File.WriteAllText(
            Path.Combine(configDirectory, "card-data-map-defaults.json"),
            """
            {
              "format": "hearthstone-card-search.card-data-map-package",
              "version": 1,
              "kind": "source-defaults",
              "maps": {
                "relatedCardMap": {
                  "CARD_A=>": "CARD_C"
                }
              }
            }
            """);

        try
        {
            CardDataMaps.ResetOverrides();
            CardDataMaps.ReloadSourceDefaults(resourceRoot);
            CardDataMaps.ResetOverrides();

            var repository = CardRepository.Load(resourceRoot);
            var detailA = repository.GetDetail("CARD_A");
            var detailC = repository.GetDetail("CARD_C");

            Assert.NotNull(detailA);
            Assert.NotNull(detailC);
            Assert.Equal("CARD_C", CardDataMaps.DefaultRelatedCardMap["CARD_A=>"]);
            Assert.Contains(detailA!.RelatedCards, item => item.CardId == "CARD_C");
            Assert.Contains(detailC!.ParentCards, item => item.CardId == "CARD_A");
        }
        finally
        {
            var originalRoot = ResourceLocator.LocateResourceRoot(AppContext.BaseDirectory, Directory.GetCurrentDirectory());
            CardDataMaps.ReloadSourceDefaults(originalRoot);
            CardDataMaps.ResetOverrides();
            Directory.Delete(resourceRoot, recursive: true);
        }
    }

    private static CardRepository LoadRepository()
    {
        var resourceRoot = ResourceLocator.LocateResourceRoot(AppContext.BaseDirectory, Directory.GetCurrentDirectory());
        return CardRepository.Load(resourceRoot);
    }

    private static string CreateTemporaryCardDataRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"hearthstone-card-search-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        File.WriteAllText(
            Path.Combine(root, "CardDefs.xml"),
            """
            <CardDefs>
              <Entity CardID="CARD_A" ID="1">
                <Tag name="CARDNAME" type="LocString"><zhCN>卡牌A</zhCN><enUS>Card A</enUS></Tag>
                <Tag name="CARDTEXT" type="LocString"><zhCN>A</zhCN><enUS>A</enUS></Tag>
                <Tag name="CARD_SET" value="1637" />
                <Tag name="CARDTYPE" value="4" />
              </Entity>
              <Entity CardID="CARD_B" ID="2">
                <Tag name="CARDNAME" type="LocString"><zhCN>卡牌B</zhCN><enUS>Card B</enUS></Tag>
                <Tag name="CARDTEXT" type="LocString"><zhCN>B</zhCN><enUS>B</enUS></Tag>
                <Tag name="CARD_SET" value="1637" />
                <Tag name="CARDTYPE" value="4" />
              </Entity>
              <Entity CardID="CARD_C" ID="3">
                <Tag name="CARDNAME" type="LocString"><zhCN>卡牌C</zhCN><enUS>Card C</enUS></Tag>
                <Tag name="CARDTEXT" type="LocString"><zhCN>C</zhCN><enUS>C</enUS></Tag>
                <Tag name="CARD_SET" value="1637" />
                <Tag name="CARDTYPE" value="4" />
              </Entity>
              <Entity CardID="CARD_D" ID="4">
                <Tag name="CARDNAME" type="LocString"><zhCN>卡牌D</zhCN><enUS>Card D</enUS></Tag>
                <Tag name="CARDTEXT" type="LocString"><zhCN>D</zhCN><enUS>D</enUS></Tag>
                <Tag name="CARD_SET" value="1637" />
                <Tag name="CARDTYPE" value="4" />
              </Entity>
              <Entity CardID="CARD_E" ID="5">
                <Tag name="CARDNAME" type="LocString"><zhCN>卡牌E</zhCN><enUS>Card E</enUS></Tag>
                <Tag name="CARDTEXT" type="LocString"><zhCN>E</zhCN><enUS>E</enUS></Tag>
                <Tag name="CARD_SET" value="1637" />
                <Tag name="CARDTYPE" value="6" />
              </Entity>
            </CardDefs>
            """);

        return root;
    }
}
