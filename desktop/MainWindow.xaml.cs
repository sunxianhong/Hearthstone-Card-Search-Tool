using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.IO;
using System.Windows.Threading;
using HearthstoneCardSearchTool.Core;

namespace HearthstoneCardSearchTool;

public partial class MainWindow : Window
{
    private const int MaxDisplay = 100;

    private readonly FileImageConverter imageConverter = new();
    private readonly List<CardRecord> currentResults = [];
    private readonly DispatcherTimer toastTimer = new() { Interval = TimeSpan.FromSeconds(2.2) };
    private CardRepository? repository;

    public MainWindow()
    {
        InitializeComponent();

        Loaded += Window_Loaded;
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        MainScrollViewer.ScrollChanged += MainScrollViewer_ScrollChanged;
        SearchTextBox.KeyDown += SearchInput_KeyDown;
        ApplyFiltersButton.Click += async (_, _) => await ApplyFiltersAsync();
        ResetFiltersButton.Click += async (_, _) => await ResetFiltersAsync();
        BackToTopButton.Click += (_, _) => MainScrollViewer.ScrollToTop();
        DetailOverlay.MouseDown += DetailOverlay_MouseDown;
        RegisterSingleClickCopy(DetailNameText);
        RegisterSingleClickCopy(DetailCardIdValueText);
        RegisterSingleClickCopy(DetailDbfIdValueText);
        toastTimer.Tick += ToastTimer_Tick;

        foreach (var comboBox in new[]
                 {
                     CostComboBox,
                     ClassComboBox,
                     SetComboBox,
                     RarityComboBox,
                     TypeComboBox,
                     RaceComboBox,
                     SchoolComboBox,
                     CollectibleComboBox,
                     KeywordComboBox,
                 })
        {
            comboBox.KeyDown += SearchInput_KeyDown;
        }

        BuildStaticFilters();
    }

    private void RegisterSingleClickCopy(TextBox textBox)
    {
        textBox.PreviewMouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            CopyText(textBox.Text);
        };
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadRepositoryAsync();
    }

    private async Task LoadRepositoryAsync()
    {
        try
        {
            StatusTextBlock.Text = "正在载入卡牌数据与图片索引...";
            SearchTextBox.IsEnabled = false;
            ApplyFiltersButton.IsEnabled = false;
            ResetFiltersButton.IsEnabled = false;

            var resourceRoot = ResourceLocator.LocateResourceRoot(AppContext.BaseDirectory, Directory.GetCurrentDirectory());
            repository = await Task.Run(() => CardRepository.Load(resourceRoot));

            FillDynamicFilters();

            SearchTextBox.IsEnabled = true;
            ApplyFiltersButton.IsEnabled = true;
            ResetFiltersButton.IsEnabled = true;
            SearchTextBox.Focus();

            await ApplyFiltersAsync();
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"加载失败：{ex.Message}";
            ResultHintText.Text = "资源读取失败，请确认 CardDefs.xml 和 cardpng 与程序放在一起。";
        }
    }

    private void BuildStaticFilters()
    {
        CostComboBox.ItemsSource = BuildCostOptions();
        CostComboBox.SelectedIndex = 0;

        CollectibleComboBox.ItemsSource = new List<FilterOption>
        {
            new FilterOption(string.Empty, "可否收藏 (全部)"),
            new FilterOption("1", "可收藏"),
            new FilterOption("0", "不可收藏"),
        };
        CollectibleComboBox.SelectedIndex = 0;

        KeywordComboBox.ItemsSource = new List<FilterOption>
        {
            new FilterOption(string.Empty, "关键词 (无)"),
            new FilterOption("BATTLECRY", "战吼 (BATTLECRY)"),
            new FilterOption("TAUNT", "嘲讽 (TAUNT)"),
            new FilterOption("DIVINE_SHIELD", "圣盾 (DIVINE_SHIELD)"),
            new FilterOption("DEATHRATTLE", "亡语 (DEATHRATTLE)"),
            new FilterOption("DISCOVER", "发现 (DISCOVER)"),
            new FilterOption("RUSH", "突袭 (RUSH)"),
            new FilterOption("LIFESTEAL", "吸血 (LIFESTEAL)"),
            new FilterOption("WINDFURY", "风怒 (WINDFURY)"),
            new FilterOption("STEALTH", "潜行 (STEALTH)"),
        };
        KeywordComboBox.SelectedIndex = 0;
    }

    private void FillDynamicFilters()
    {
        if (repository is null)
        {
            return;
        }

        FillComboBox(SetComboBox, "扩展包 (全部)", BuildPresentOptions(repository.Bootstrap.Sets));
        FillComboBox(ClassComboBox, "职业 (全部)", BuildMappedOptions(CardDataMaps.ClassMap));
        FillComboBox(RarityComboBox, "稀有度 (全部)", BuildMappedOptions(CardDataMaps.RarityMap));
        FillComboBox(TypeComboBox, "卡牌类型 (全部)", BuildMappedOptions(CardDataMaps.CardTypeMap));
        FillComboBox(RaceComboBox, "随从类型 (全部)", BuildPresentOptions(repository.Bootstrap.Races));
        FillComboBox(SchoolComboBox, "法术派系 (全部)", BuildPresentOptions(repository.Bootstrap.Schools));
    }

    private static List<FilterOption> BuildCostOptions()
    {
        var items = new List<FilterOption>
        {
            new(string.Empty, "法力值 (全部)"),
        };

        for (var cost = 0; cost <= 9; cost++)
        {
            items.Add(new FilterOption(cost.ToString(), cost.ToString()));
        }

        items.Add(new FilterOption("10", "10及以上"));
        return items;
    }

    private static void FillComboBox(ComboBox comboBox, string defaultLabel, IEnumerable<FilterOption> items)
    {
        comboBox.ItemsSource = new[]
        {
            new FilterOption(string.Empty, defaultLabel),
        }.Concat(items).ToList();
        comboBox.SelectedIndex = 0;
    }

    private static IEnumerable<FilterOption> BuildMappedOptions(IReadOnlyDictionary<string, string> map)
    {
        return map
            .Select(static pair => BuildLabeledOption(pair.Key, pair.Value))
            .OrderBy(static item => SortKey(item.Value))
            .ThenBy(static item => item.Label, StringComparer.Ordinal);
    }

    private static IEnumerable<FilterOption> BuildPresentOptions(IEnumerable<FilterOption> items)
    {
        return items
            .Select(static item => BuildLabeledOption(item.Value, item.Label))
            .OrderBy(static item => SortKey(item.Value))
            .ThenBy(static item => item.Label, StringComparer.Ordinal);
    }

    private static FilterOption BuildLabeledOption(string value, string label)
    {
        return new FilterOption(value, $"{label.Trim()} ({value})");
    }

    private static int SortKey(string value)
    {
        return int.TryParse(value, out var parsed)
            ? parsed
            : int.MaxValue;
    }

    private async Task ApplyFiltersAsync()
    {
        if (repository is null)
        {
            return;
        }

        try
        {
            StatusTextBlock.Text = "正在筛选卡牌...";
            ApplyFiltersButton.IsEnabled = false;
            ResetFiltersButton.IsEnabled = false;

            var filters = new SearchFilters
            {
                Cost = SelectedValue(CostComboBox),
                Class = SelectedValue(ClassComboBox),
                Set = SelectedValue(SetComboBox),
                Rarity = SelectedValue(RarityComboBox),
                CardType = SelectedValue(TypeComboBox),
                Race = SelectedValue(RaceComboBox),
                School = SelectedValue(SchoolComboBox),
                Collectible = SelectedValue(CollectibleComboBox),
                Keyword = SelectedValue(KeywordComboBox),
            };

            var query = SearchTextBox.Text ?? string.Empty;
            var results = await Task.Run(() => repository.Search(query, filters, MaxDisplay));

            currentResults.Clear();
            currentResults.AddRange(results);
            RenderCards(results);

            SearchModeText.Text = DescribeSearchMode(query);

            if (results.Count == 0)
            {
                StatusTextBlock.Text = $"卡牌库共 {repository.Bootstrap.TotalCards:N0} 张。未找到符合条件的卡牌。";
                ResultHintText.Text = "可以试试普通文本、Tag:值，或者像 45:5 这样的 EnumID:值 检索。";
            }
            else
            {
                StatusTextBlock.Text = $"卡牌库共 {repository.Bootstrap.TotalCards:N0} 张，当前显示 {results.Count} 张卡牌。";
                ResultHintText.Text = "图片一张挨着一张展示，点击打开详情。";
            }

            if (results.Count >= MaxDisplay)
            {
                ResultHintText.Text = "图片一张挨着一张显示，点击打开详情。结果过多，当前仅显示前 100 张。";
            }
        }
        catch (Exception ex)
        {
            CardWallPanel.Children.Clear();
            StatusTextBlock.Text = $"筛选失败：{ex.Message}";
        }
        finally
        {
            ApplyFiltersButton.IsEnabled = true;
            ResetFiltersButton.IsEnabled = true;
        }
    }

    private async Task ResetFiltersAsync()
    {
        SearchTextBox.Text = string.Empty;
        CostComboBox.SelectedIndex = 0;
        ClassComboBox.SelectedIndex = 0;
        SetComboBox.SelectedIndex = 0;
        RarityComboBox.SelectedIndex = 0;
        TypeComboBox.SelectedIndex = 0;
        RaceComboBox.SelectedIndex = 0;
        SchoolComboBox.SelectedIndex = 0;
        CollectibleComboBox.SelectedIndex = 0;
        KeywordComboBox.SelectedIndex = 0;

        await ApplyFiltersAsync();
    }

    private void RenderCards(IReadOnlyList<CardRecord> cards)
    {
        CardWallPanel.Children.Clear();

        if (cards.Count == 0)
        {
            CardWallPanel.Children.Add(
                new Border
                {
                    Width = 1320,
                    Padding = new Thickness(28),
                    Background = new SolidColorBrush(Color.FromRgb(249, 241, 228)),
                    CornerRadius = new CornerRadius(0),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(164, 147, 116)),
                    BorderThickness = new Thickness(1),
                    Child = new TextBlock
                    {
                        Text = "没有找到符合条件的卡牌。",
                        FontSize = 16,
                        Foreground = new SolidColorBrush(Color.FromRgb(92, 72, 49)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        TextAlignment = TextAlignment.Center,
                    },
                });
            return;
        }

        foreach (var card in cards)
        {
            CardWallPanel.Children.Add(CreateCardTile(card));
        }
    }

    private Button CreateCardTile(CardRecord card)
    {
        UIElement content;
        if (string.IsNullOrWhiteSpace(card.ImagePath))
        {
            var description = string.IsNullOrWhiteSpace(card.TextZh)
                ? "暂无图片索引"
                : card.TextZh;

            content = new Border
            {
                Width = 214,
                Height = 300,
                Background = new SolidColorBrush(Color.FromRgb(246, 236, 218)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(201, 180, 137)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(16),
                Child = new Grid
                {
                    RowDefinitions =
                    {
                        new RowDefinition { Height = GridLength.Auto },
                        new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                    },
                    Children =
                    {
                        new TextBlock
                        {
                            Text = card.NameZh,
                            FontSize = 18,
                            FontWeight = FontWeights.Bold,
                            Foreground = new SolidColorBrush(Color.FromRgb(61, 48, 34)),
                            TextAlignment = TextAlignment.Center,
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(0, 0, 0, 14),
                        },
                        new TextBlock
                        {
                            Text = description,
                            FontSize = 14,
                            Foreground = new SolidColorBrush(Color.FromRgb(92, 72, 49)),
                            TextAlignment = TextAlignment.Center,
                            TextWrapping = TextWrapping.Wrap,
                            VerticalAlignment = VerticalAlignment.Center,
                        },
                    },
                },
            };

            Grid.SetRow(((Grid)((Border)content).Child).Children[1], 1);
        }
        else
        {
            content = new Image
            {
                Width = 214,
                Height = 300,
                Stretch = Stretch.Uniform,
                Source = imageConverter.Convert(card.ImagePath, typeof(ImageSource), null!, System.Globalization.CultureInfo.CurrentUICulture) as ImageSource,
            };
        }

        var button = new Button
        {
            Width = 214,
            Height = 300,
            Margin = new Thickness(0, 0, 18, 18),
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            ToolTip = $"{card.NameZh}\nCardID: {card.CardId}\nID: {card.DbfId}",
            Content = content,
            Tag = card.CardId,
            Style = (Style)FindResource("CardTileButtonStyle"),
        };

        button.Click += (_, _) => ShowCardDetail(card.CardId);
        return button;
    }

    private static string? SelectedValue(ComboBox comboBox)
    {
        return comboBox.SelectedItem is FilterOption option && !string.IsNullOrWhiteSpace(option.Value)
            ? option.Value
            : null;
    }

    private static string DescribeSearchMode(string query)
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
                ? "EnumID 检索"
                : "标签检索";
        }

        return "普通搜索";
    }

    private async void SearchInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        await ApplyFiltersAsync();
    }

    private void MainScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        BackToTopButton.Visibility = e.VerticalOffset > 320
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void ShowCardDetail(string cardId)
    {
        if (repository is null)
        {
            return;
        }

        var detail = repository.GetDetail(cardId);
        if (detail is null)
        {
            return;
        }

        DetailNameText.Text = detail.Name;
        DetailDescriptionText.Text = string.IsNullOrWhiteSpace(detail.Text) ? "（无描述）" : detail.Text;
        DetailCardIdValueText.Text = detail.CardId;
        DetailDbfIdValueText.Text = detail.DbfId.ToString();
        EnchantBadge.Visibility = detail.IsEnchantment ? Visibility.Visible : Visibility.Collapsed;
        DetailImage.Source = imageConverter.Convert(detail.ImagePath ?? string.Empty, typeof(ImageSource), null!, System.Globalization.CultureInfo.CurrentUICulture) as ImageSource;

        RenderRelatedGroup(ParentSection, ParentLinksPanel, detail.ParentCards, Color.FromRgb(40, 99, 124));
        RenderRelatedGroup(RelatedSection, RelatedLinksPanel, detail.RelatedCards, Color.FromRgb(111, 76, 32));
        RenderRelatedGroup(EnchantmentSection, EnchantmentLinksPanel, detail.EnchantmentCards, Color.FromRgb(154, 101, 13));
        RenderTags(detail.Tags);

        DetailOverlay.Visibility = Visibility.Visible;
        DetailScrollViewer.ScrollToVerticalOffset(0);
        Dispatcher.BeginInvoke(
            () => DetailScrollViewer.ScrollToVerticalOffset(0),
            DispatcherPriority.Loaded);
    }

    private void RenderRelatedGroup(Border section, WrapPanel panel, IReadOnlyList<RelatedCardLink> links, Color foregroundColor)
    {
        panel.Children.Clear();
        section.Visibility = links.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        foreach (var link in links)
        {
            var button = new Button
            {
                Margin = new Thickness(0, 0, 12, 12),
                Padding = new Thickness(14, 8, 14, 8),
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(foregroundColor),
                Content = link.Name,
                Tag = link.CardId,
                ToolTip = link.Reason,
            };

            button.Click += (_, _) => ShowCardDetail(link.CardId);
            panel.Children.Add(button);
        }
    }

    private void RenderTags(IReadOnlyList<CardTagView> tags)
    {
        TagsPanel.Children.Clear();

        foreach (var tag in tags)
        {
            var lineBlock = new TextBox
            {
                Text = $"{tag.DisplayName} = {tag.Value}",
                Style = (Style)FindResource("TagLineTextBoxStyle"),
                VerticalContentAlignment = VerticalAlignment.Center,
                AcceptsReturn = false,
                Cursor = Cursors.IBeam,
            };

            TagsPanel.Children.Add(lineBlock);
        }
    }

    private void CopyText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        Clipboard.SetText(text);
        ToastTextBlock.Text = $"已复制到剪贴板: {text}";
        ToastBorder.Visibility = Visibility.Visible;
        toastTimer.Stop();
        toastTimer.Start();
    }

    private void ToastTimer_Tick(object? sender, EventArgs e)
    {
        toastTimer.Stop();
        ToastBorder.Visibility = Visibility.Collapsed;
    }

    private void DetailOverlay_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (ReferenceEquals(e.Source, DetailOverlay))
        {
            CloseDetailOverlay();
        }
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DetailOverlay.Visibility == Visibility.Visible)
        {
            e.Handled = true;
            CloseDetailOverlay();
        }
    }

    private void CloseDetailOverlay()
    {
        DetailOverlay.Visibility = Visibility.Collapsed;
    }
}
