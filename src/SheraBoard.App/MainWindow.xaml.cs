using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SheraBoard.App.Services;
using SheraBoard.App.ViewModels;
using SheraBoard.Core.Imaging;
using SheraBoard.Core.Models;
using SheraBoard.Core.Persistence;

namespace SheraBoard.App;

public partial class MainWindow : Window
{
    private const int PageSize = 120;

    private enum DateFilterMode
    {
        All,
        Today,
        Yesterday,
        Last7Days,
        Custom
    }

    private readonly AppServices _services;
    private readonly ObservableCollection<ClipboardItemViewModel> _items = [];
    private readonly DispatcherTimer _hoverPreviewTimer;
    private readonly DispatcherTimer _hoverPreviewHideTimer;
    private readonly DispatcherTimer _searchDebounceTimer;
    private CancellationTokenSource? _toastCancellation;
    private ClipboardItemViewModel? _hoverPreviewCandidate;
    private FrameworkElement? _hoverPreviewTarget;
    private DateFilterMode _dateFilterMode = DateFilterMode.Today;
    private DateOnly? _customDate;
    private string? _selectedSourceApp;
    private bool _hasMoreItems;
    private bool _isLoadingItems;
    private bool _isPointerOverPreview;
    private bool _syncingDateControls;
    private bool _isResettingTransientState;
    private bool _scrollToTopAfterNextRefresh;
    private int _queryVersion;

    public MainWindow(AppServices services)
    {
        _services = services;
        _hoverPreviewTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(450)
        };
        _hoverPreviewTimer.Tick += HoverPreviewTimer_Tick;
        _hoverPreviewHideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(180)
        };
        _hoverPreviewHideTimer.Tick += HoverPreviewHideTimer_Tick;
        _searchDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _searchDebounceTimer.Tick += SearchDebounceTimer_Tick;
        InitializeComponent();
        UpdateHotkeyHint();
        ItemsList.ItemsSource = _items;
        var itemsView = CollectionViewSource.GetDefaultView(_items);
        itemsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ClipboardItemViewModel.GroupText)));
        UpdateSearchPlaceholder();
        Loaded += (_, _) => Dispatcher.BeginInvoke(
            new Action(async () =>
            {
                SearchBox.Focus();
                Keyboard.Focus(SearchBox);
                await RefreshSourceAppFiltersAsync();
                await RefreshItemsAsync();
            }),
            DispatcherPriority.Background);
        Activated += async (_, _) =>
        {
            if (IsLoaded)
            {
                await RefreshItemsAsync();
            }
        };
    }

    public async Task RefreshItemsAsync()
    {
        if (_isLoadingItems)
        {
            return;
        }

        var selectedId = (ItemsList.SelectedItem as ClipboardItemViewModel)?.Record.Id;
        var version = ++_queryVersion;

        await RefreshSourceAppFiltersAsync();
        _items.Clear();
        _hasMoreItems = false;
        StatusText.Text = $"加载中 · {CurrentDateFilterText()}";
        await LoadItemsPageAsync(reset: true, selectedId: selectedId, version: version);
        if (_scrollToTopAfterNextRefresh)
        {
            _scrollToTopAfterNextRefresh = false;
            ScrollItemsToTopSoon();
        }
    }

    public void ResetTransientViewStateForFreshOpen()
    {
        _searchDebounceTimer.Stop();
        _hoverPreviewTimer.Stop();
        _hoverPreviewHideTimer.Stop();
        _toastCancellation?.Cancel();
        HideHoverPreview();
        SourceAppPopup.IsOpen = false;
        DateCalendarPopup.IsOpen = false;

        _isResettingTransientState = true;
        _syncingDateControls = true;
        try
        {
            SearchBox.Clear();
            ItemsList.SelectedItem = null;
            _selectedSourceApp = null;
            _dateFilterMode = DateFilterMode.Today;
            _customDate = null;
            DatePickerButton.Content = "选择日期";
            DateCalendar.SelectedDate = null;
            DateCalendar.DisplayDate = DateTime.Today;

            CheckRadioButtonByTag(KindFiltersPanel, "All");
            if (DateAllFilter.Parent is System.Windows.Controls.Panel dateFiltersPanel)
            {
                CheckRadioButtonByTag(dateFiltersPanel, "Today");
            }
        }
        finally
        {
            _syncingDateControls = false;
            _isResettingTransientState = false;
        }

        ToastPanel.BeginAnimation(OpacityProperty, null);
        ToastPanel.Visibility = Visibility.Collapsed;
        ToastPanel.Opacity = 0;
        UpdateSearchPlaceholder();
        UpdateSourceAppFilterButton();
        UpdateEmptyStateText();
        _scrollToTopAfterNextRefresh = true;
    }

    private async Task LoadNextPageAsync()
    {
        if (!_hasMoreItems || _isLoadingItems)
        {
            return;
        }

        await LoadItemsPageAsync(reset: false, selectedId: null, version: _queryVersion);
    }

    private async Task LoadItemsPageAsync(bool reset, Guid? selectedId, int version)
    {
        if (_isLoadingItems)
        {
            return;
        }

        _isLoadingItems = true;
        try
        {
            var offset = reset ? 0 : _items.Count;
            var records = await _services.Repository.ListAsync(BuildQuery(PageSize + 1, offset));
            if (version != _queryVersion)
            {
                return;
            }

            var pageItems = records.Take(PageSize).ToList();
            if (reset)
            {
                _items.Clear();
            }

            foreach (var item in pageItems)
            {
                _items.Add(new ClipboardItemViewModel(item));
            }

            _hasMoreItems = records.Count > PageSize;
            if (selectedId is not null)
            {
                ItemsList.SelectedItem = _items.FirstOrDefault(item => item.Record.Id == selectedId.Value);
            }
        }
        finally
        {
            _isLoadingItems = false;
        }

        UpdateListStatus();
    }

    private void UpdateListStatus()
    {
        EmptyStatePanel.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateEmptyStateText();
        StatusText.Text = _hasMoreItems
            ? $"{_items.Count} 条 · {CurrentDateFilterText()} · 向下滚动继续加载"
            : $"{_items.Count} 条 · {CurrentDateFilterText()}";
    }

    private void UpdateEmptyStateText()
    {
        if (EmptyStateTitleText is null || EmptyStateDescriptionText is null)
        {
            return;
        }

        var hasSearchOrFilter = !string.IsNullOrWhiteSpace(SearchBox.Text)
                                || !string.IsNullOrWhiteSpace(_selectedSourceApp)
                                || (KindFiltersPanel?.Children
                                    .OfType<System.Windows.Controls.RadioButton>()
                                    .Any(button => button.IsChecked == true && button.Tag is string tag && tag != "All") == true)
                                || _dateFilterMode != DateFilterMode.Today;
        EmptyStateTitleText.Text = hasSearchOrFilter ? "没有找到匹配记录" : "还没有剪贴板记录";
        EmptyStateDescriptionText.Text = hasSearchOrFilter
            ? "试试减少关键词，切换到“全部日期”，或去掉应用/类型筛选。"
            : "复制文字、图片或文件后会自动出现在这里。";
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_services.IsExiting)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    private ClipboardQuery BuildQuery(int limit = PageSize + 1, int offset = 0)
    {
        var parsedSearch = ClipboardSearchParser.Parse(SearchBox.Text);
        var checkedKind = KindFiltersPanel.Children
            .OfType<System.Windows.Controls.RadioButton>()
            .FirstOrDefault(button => button.IsChecked == true);
        var kind = parsedSearch.Kind ?? SelectedKindFilter(checkedKind);
        var features = parsedSearch.Features.ToList();
        if (SelectedFeatureFilter(checkedKind) is { } selectedFeature && !features.Contains(selectedFeature))
        {
            features.Add(selectedFeature);
        }

        var (startDate, endDate) = DateRangeFilter();

        return new ClipboardQuery(
            SearchText: parsedSearch.SearchText,
            SearchTerms: parsedSearch.SearchTerms,
            Kind: kind,
            Date: null,
            Limit: limit,
            Offset: offset,
            StartDate: startDate,
            EndDate: endDate,
            SourceApp: string.IsNullOrWhiteSpace(parsedSearch.SourceApp) ? _selectedSourceApp : parsedSearch.SourceApp,
            PinnedOnly: parsedSearch.PinnedOnly,
            Features: features);
    }

    private static ClipboardKind? SelectedKindFilter(System.Windows.Controls.RadioButton? checkedKind)
    {
        if (checkedKind?.Tag is not string tag)
        {
            return null;
        }

        var normalized = tag.StartsWith("Kind:", StringComparison.OrdinalIgnoreCase)
            ? tag["Kind:".Length..]
            : tag;
        return Enum.TryParse<ClipboardKind>(normalized, out var parsedKind)
            ? parsedKind
            : null;
    }

    private static ClipboardContentFeature? SelectedFeatureFilter(System.Windows.Controls.RadioButton? checkedKind)
    {
        if (checkedKind?.Tag is not string tag ||
            !tag.StartsWith("Feature:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Enum.TryParse<ClipboardContentFeature>(tag["Feature:".Length..], out var feature)
            ? feature
            : null;
    }

    private (DateOnly? StartDate, DateOnly? EndDate) DateRangeFilter()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return _dateFilterMode switch
        {
            DateFilterMode.Today => (today, today),
            DateFilterMode.Yesterday => (today.AddDays(-1), today.AddDays(-1)),
            DateFilterMode.Last7Days => (today.AddDays(-6), today),
            DateFilterMode.Custom when _customDate is { } customDate => (customDate, customDate),
            _ => (null, null)
        };
    }

    private string CurrentDateFilterText()
    {
        return _dateFilterMode switch
        {
            DateFilterMode.Today => "今天",
            DateFilterMode.Yesterday => "昨天",
            DateFilterMode.Last7Days => "近7天",
            DateFilterMode.Custom when _customDate is { } date => FormatDateChip(date),
            _ => "全部日期"
        };
    }

    private static BitmapImage CreateBitmap(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static BitmapSource? CreateDisplayBitmap(byte[] bytes)
    {
        var bitmap = CreateBitmap(bytes);
        var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
        var stride = Math.Max(1, converted.PixelWidth * 4);
        var pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);

        var visibility = ImageVisibilityAnalyzer.AnalyzeBgra32(pixels);
        if (!visibility.HasRenderableContent)
        {
            return null;
        }

        if (!visibility.ShouldForceOpaqueForDisplay)
        {
            return bitmap;
        }

        for (var i = 3; i < pixels.Length; i += 4)
        {
            pixels[i] = 255;
        }

        var displayBitmap = BitmapSource.Create(
            converted.PixelWidth,
            converted.PixelHeight,
            bitmap.DpiX > 0 ? bitmap.DpiX : 96,
            bitmap.DpiY > 0 ? bitmap.DpiY : 96,
            PixelFormats.Bgra32,
            null,
            pixels,
            stride);
        displayBitmap.Freeze();
        return displayBitmap;
    }

    private static SolidColorBrush CreateBrush(string color)
    {
        var brush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }

    private ClipboardItemViewModel? SelectedItem()
    {
        return ItemsList.SelectedItem as ClipboardItemViewModel;
    }

    private async Task CopySelectedAsync()
    {
        var selected = SelectedItem();
        if (selected is null)
        {
            return;
        }

        await CopyItemAsync(selected, RestoreMode.Original);
    }

    private async Task CopyItemAsync(ClipboardItemViewModel item, RestoreMode restoreMode)
    {
        _services.PrepareForInternalClipboardWrite();
        await _services.RestoreService.RestoreAsync(item.Record, restoreMode);
        var modeText = restoreMode switch
        {
            RestoreMode.Original => "原格式",
            RestoreMode.Image => "图片",
            _ => "纯文本/路径"
        };
        StatusText.Text = $"已复制 · {modeText}";
        if (_services.Settings.CloseWindowAfterCopy)
        {
            HideAfterCopyIfConfigured();
            return;
        }

        await ShowToastAsync($"已复制 · {modeText}");
    }

    private void HideAfterCopyIfConfigured()
    {
        if (!_services.Settings.CloseWindowAfterCopy)
        {
            return;
        }

        HideHoverPreview();
        Hide();
    }

    private async Task DeleteItemAsync(ClipboardItemViewModel item)
    {
        await _services.PayloadStore.DeleteAsync(item.Record.PayloadRef);
        await _services.Repository.DeleteAsync(item.Record.Id);
        await RefreshItemsAsync();
        HideHoverPreview();
        StatusText.Text = "已删除";
        await ShowToastAsync("已删除");
    }

    private async Task TogglePinItemAsync(ClipboardItemViewModel item)
    {
        var pinned = !item.Record.Pinned;
        await _services.Repository.SetPinnedAsync(item.Record.Id, pinned);
        await RefreshItemsAsync();
        StatusText.Text = pinned ? "已固定到顶部" : "已取消固定";
        await ShowToastAsync(pinned ? "已固定到顶部" : "已取消固定");
    }

    private async Task ToggleFavoriteItemAsync(ClipboardItemViewModel item)
    {
        await _services.Repository.SetFavoriteAsync(item.Record.Id, !item.Record.Favorite);
        await RefreshItemsAsync();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateSearchPlaceholder();
        if (IsLoaded && !_isResettingTransientState)
        {
            _searchDebounceTimer.Stop();
            _searchDebounceTimer.Start();
        }
    }

    private void UpdateSearchPlaceholder()
    {
        if (SearchPlaceholder is null || SearchBox is null)
        {
            return;
        }

        SearchPlaceholder.Visibility = string.IsNullOrWhiteSpace(SearchBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private async void SearchDebounceTimer_Tick(object? sender, EventArgs e)
    {
        _searchDebounceTimer.Stop();
        await RefreshItemsAsync();
    }

    private async void Filter_Changed(object sender, RoutedEventArgs e)
    {
        if (IsLoaded && !_isResettingTransientState)
        {
            await RefreshItemsAsync();
        }
    }

    private async Task RefreshSourceAppFiltersAsync()
    {
        if (SourceAppPopupList is null)
        {
            return;
        }

        var apps = await _services.Repository.ListRecentSourceAppsAsync(limit: 10);
        SourceAppPopupList.Children.Clear();
        AddSourceAppPopupRow(
            primaryText: "全部应用",
            secondaryText: "查看所有来源",
            sourceApp: null,
            isSelected: string.IsNullOrWhiteSpace(_selectedSourceApp));

        foreach (var app in apps)
        {
            AddSourceAppPopupRow(
                DisplaySourceAppName(app.SourceApp),
                $"{app.ItemCount} 条",
                app.SourceApp,
                SourceAppMatches(app.SourceApp, _selectedSourceApp));
        }

        if (!string.IsNullOrWhiteSpace(_selectedSourceApp) &&
            apps.All(app => !SourceAppMatches(app.SourceApp, _selectedSourceApp)))
        {
            AddSourceAppPopupRow(
                DisplaySourceAppName(_selectedSourceApp),
                "当前筛选",
                _selectedSourceApp,
                isSelected: true);
        }

        UpdateSourceAppFilterButton();
    }

    private void AddSourceAppPopupRow(string primaryText, string secondaryText, string? sourceApp, bool isSelected)
    {
        var row = new System.Windows.Controls.Button
        {
            Tag = sourceApp ?? string.Empty,
            MinWidth = 0,
            Height = 30,
            Margin = new Thickness(0, 0, 0, 2),
            Padding = new Thickness(8, 0, 8, 0),
            HorizontalContentAlignment = System.Windows.HorizontalAlignment.Stretch,
            Background = isSelected
                ? CreateBrush("#F3F8F8")
                : System.Windows.Media.Brushes.Transparent,
            BorderBrush = isSelected
                ? CreateBrush("#CFE3E1")
                : System.Windows.Media.Brushes.Transparent,
            ToolTip = string.IsNullOrWhiteSpace(sourceApp) ? "全部应用来源" : sourceApp
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var nameText = new TextBlock
        {
            Text = primaryText,
            FontSize = 12,
            FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = isSelected
                ? CreateBrush("#0E6B64")
                : (System.Windows.Media.Brush)FindResource("TextBrush")
        };
        Grid.SetColumn(nameText, 0);

        var detailText = new TextBlock
        {
            Text = secondaryText,
            FontSize = 10.5,
            Foreground = (System.Windows.Media.Brush)FindResource("MutedTextBrush"),
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(detailText, 1);

        grid.Children.Add(nameText);
        grid.Children.Add(detailText);
        row.Content = grid;
        row.Click += SourceAppPopupItem_Click;
        SourceAppPopupList.Children.Add(row);
    }

    private void SourceAppFilterButton_Click(object sender, RoutedEventArgs e)
    {
        SourceAppPopup.IsOpen = !SourceAppPopup.IsOpen;
    }

    private async void SourceAppPopupItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button)
        {
            return;
        }

        _selectedSourceApp = button.Tag as string;
        if (string.IsNullOrWhiteSpace(_selectedSourceApp))
        {
            _selectedSourceApp = null;
        }

        SourceAppPopup.IsOpen = false;
        UpdateSourceAppFilterButton();
        if (IsLoaded)
        {
            await RefreshItemsAsync();
        }
    }

    private void UpdateSourceAppFilterButton()
    {
        if (SourceAppFilterText is null)
        {
            return;
        }

        SourceAppFilterText.Text = string.IsNullOrWhiteSpace(_selectedSourceApp)
            ? "应用：全部"
            : $"应用：{DisplaySourceAppName(_selectedSourceApp)}";
    }

    private void DatePickerButton_Click(object sender, RoutedEventArgs e)
    {
        if (_customDate is { } customDate)
        {
            DateCalendar.SelectedDate = customDate.ToDateTime(TimeOnly.MinValue);
            DateCalendar.DisplayDate = customDate.ToDateTime(TimeOnly.MinValue);
        }
        else
        {
            DateCalendar.SelectedDate = null;
            DateCalendar.DisplayDate = DateTime.Today;
        }

        DateCalendarPopup.IsOpen = true;
    }

    private async void DateQuickFilter_Changed(object sender, RoutedEventArgs e)
    {
        if (_syncingDateControls ||
            _isResettingTransientState ||
            sender is not System.Windows.Controls.RadioButton { IsChecked: true, Tag: string tag })
        {
            return;
        }

        _dateFilterMode = tag switch
        {
            "Today" => DateFilterMode.Today,
            "Yesterday" => DateFilterMode.Yesterday,
            "Last7Days" => DateFilterMode.Last7Days,
            _ => DateFilterMode.All
        };
        _customDate = null;

        // The checked event for the default date chip can fire while XAML is
        // still constructing later named controls. Guard those references so
        // startup never crashes before the window finishes loading.
        if (DatePickerButton is not null)
        {
            DatePickerButton.Content = "选择日期";
        }

        if (DateCalendar is not null)
        {
            DateCalendar.SelectedDate = null;
        }

        if (IsLoaded)
        {
            await RefreshItemsAsync();
        }
    }

    private async void DateCalendar_SelectedDatesChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DateCalendar.SelectedDate is not { } selectedDate)
        {
            return;
        }

        _syncingDateControls = true;
        _dateFilterMode = DateFilterMode.Custom;
        _customDate = DateOnly.FromDateTime(selectedDate);
        DatePickerButton.Content = FormatDateChip(_customDate.Value);
        foreach (var radioButton in FindVisualChildren<System.Windows.Controls.RadioButton>(DateAllFilter.Parent))
        {
            if (string.Equals(radioButton.GroupName, "DateFilter", StringComparison.Ordinal))
            {
                radioButton.IsChecked = false;
            }
        }
        _syncingDateControls = false;
        DateCalendarPopup.IsOpen = false;

        if (IsLoaded)
        {
            await RefreshItemsAsync();
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshItemsAsync();
    }

    private void ItemsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
    }

    private async void ItemsList_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.ExtentHeight <= 0 || e.ViewportHeight <= 0)
        {
            return;
        }

        if (e.VerticalOffset + e.ViewportHeight >= e.ExtentHeight - 6)
        {
            await LoadNextPageAsync();
        }
    }

    private async void ItemsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        await CopySelectedAsync();
    }

    private async void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        await CopySelectedAsync();
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = SelectedItem();
        if (selected is null)
        {
            return;
        }

        await DeleteItemAsync(selected);
    }

    private async void PinButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = SelectedItem();
        if (selected is null)
        {
            return;
        }

        await TogglePinItemAsync(selected);
    }

    private async void FavoriteButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = SelectedItem();
        if (selected is null)
        {
            return;
        }

        await ToggleFavoriteItemAsync(selected);
    }

    private async void RowPinButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is not System.Windows.Controls.Button button || button.Tag is not ClipboardItemViewModel item)
        {
            return;
        }

        ItemsList.SelectedItem = item;
        await TogglePinItemAsync(item);
    }

    private async void RowDeleteButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is not System.Windows.Controls.Button button || button.Tag is not ClipboardItemViewModel item)
        {
            return;
        }

        ItemsList.SelectedItem = item;
        await DeleteItemAsync(item);
    }

    private void ItemRow_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ClipboardItemViewModel item })
        {
            ItemsList.SelectedItem = item;
        }
    }

    private async void RowCopyOriginalMenu_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ClipboardItemViewModel item)
        {
            ItemsList.SelectedItem = item;
            await CopyItemAsync(item, RestoreMode.Original);
        }
    }

    private async void RowCopyPlainTextMenu_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ClipboardItemViewModel item)
        {
            ItemsList.SelectedItem = item;
            await CopyItemAsync(item, RestoreMode.PlainText);
        }
    }

    private async void RowCopyImageMenu_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ClipboardItemViewModel item)
        {
            ItemsList.SelectedItem = item;
            await CopyItemAsync(item, RestoreMode.Image);
        }
    }

    private async void SaveSnapshotButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = SelectedItem();
        if (selected is null || selected.Record.Kind != ClipboardKind.FileList)
        {
            StatusText.Text = "只支持文件记录保存副本";
            return;
        }

        var snapshotPath = await _services.FileSnapshotService.SaveSnapshotAsync(selected.Record.Id);
        StatusText.Text = $"已保存副本：{snapshotPath}";
        await ShowToastAsync("已保存副本");
        await RefreshItemsAsync();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new SettingsWindow(_services)
        {
            Owner = this
        };
        window.ShowDialog();
        UpdateHotkeyHint();
    }

    private void UpdateHotkeyHint()
    {
        if (HotkeyHintText is null)
        {
            return;
        }

        HotkeyHintText.Text = "Win+V";
    }

    private void ItemRow_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: ClipboardItemViewModel item } target)
        {
            return;
        }

        _hoverPreviewHideTimer.Stop();
        _hoverPreviewCandidate = item;
        _hoverPreviewTarget = target;
        _hoverPreviewTimer.Stop();

        if (ShouldSkipHoverPreview(item))
        {
            HideHoverPreview();
            return;
        }

        _hoverPreviewTimer.Start();
    }

    private void ItemRow_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _hoverPreviewTimer.Stop();
        StartHoverPreviewHideTimer();
    }

    private async void HoverPreviewTimer_Tick(object? sender, EventArgs e)
    {
        _hoverPreviewTimer.Stop();
        if (_hoverPreviewCandidate is not { } item)
        {
            return;
        }

        await ShowHoverPreviewAsync(item);
    }

    private void HoverPreviewHideTimer_Tick(object? sender, EventArgs e)
    {
        _hoverPreviewHideTimer.Stop();
        if (_isPointerOverPreview || _hoverPreviewTarget?.IsMouseOver == true)
        {
            return;
        }

        _hoverPreviewCandidate = null;
        HideHoverPreview();
    }

    private static bool ShouldSkipHoverPreview(ClipboardItemViewModel item)
    {
        return false;
    }

    private async Task ShowHoverPreviewAsync(ClipboardItemViewModel item)
    {
        HoverPreviewTextBlock.Text = string.Empty;
        HoverPreviewTextScroll.Visibility = Visibility.Visible;
        HoverPreviewRichBox.Visibility = Visibility.Collapsed;
        HoverPreviewRichBox.Document = RichPreviewDocumentBuilder.FromPlainText(string.Empty);
        HoverPreviewImagePanel.Visibility = Visibility.Collapsed;
        HoverPreviewImage.Source = null;
        HoverPreviewFilesList.Visibility = Visibility.Collapsed;
        HoverPreviewFilesList.ItemsSource = null;

        switch (item.Record.Kind)
        {
            case ClipboardKind.Text:
                var textPayload = await _services.PayloadStore.ReadAsync<TextPayload>(item.Record.PayloadRef);
                ShowHoverText(textPayload.Text);
                break;
            case ClipboardKind.RichText:
                var richPayload = await _services.PayloadStore.ReadAsync<RichTextPayload>(item.Record.PayloadRef);
                ShowHoverRichText(richPayload);
                break;
            case ClipboardKind.Image:
                var imagePayload = await _services.PayloadStore.ReadAsync<ImagePayload>(item.Record.PayloadRef);
                var imageSource = CreateDisplayBitmap(imagePayload.PngBytes);
                if (imageSource is null)
                {
                    ShowHoverText(item.PreviewText);
                }
                else
                {
                    HoverPreviewTextScroll.Visibility = Visibility.Collapsed;
                    HoverPreviewImagePanel.Visibility = Visibility.Visible;
                    HoverPreviewImage.Source = imageSource;
                }
                break;
            case ClipboardKind.FileList:
                var filePayload = await _services.PayloadStore.ReadAsync<FileListPayload>(item.Record.PayloadRef);
                HoverPreviewTextScroll.Visibility = Visibility.Collapsed;
                HoverPreviewFilesList.Visibility = Visibility.Visible;
                HoverPreviewFilesList.ItemsSource = filePayload.Items.Select(file =>
                    $"{(file.Exists ? "可用" : "缺失")} · {file.Name} · {file.OriginalPath}");
                break;
        }

        HoverPreviewPopup.PlacementTarget = _hoverPreviewTarget ?? ItemsList;
        HoverPreviewPopup.Placement = PlacementMode.Right;
        HoverPreviewPopup.HorizontalOffset = 10;
        HoverPreviewPopup.IsOpen = true;
    }

    private void ShowHoverText(string text)
    {
        HoverPreviewTextScroll.Visibility = Visibility.Visible;
        HoverPreviewRichBox.Visibility = Visibility.Collapsed;
        HoverPreviewImagePanel.Visibility = Visibility.Collapsed;
        HoverPreviewFilesList.Visibility = Visibility.Collapsed;
        HoverPreviewTextBlock.Text = text;
    }

    private void ShowHoverRichText(RichTextPayload payload)
    {
        if (payload.PreviewPngBytes is { Length: > 0 } previewBytes &&
            CreateDisplayBitmap(previewBytes) is { } previewImage)
        {
            HoverPreviewTextScroll.Visibility = Visibility.Collapsed;
            HoverPreviewRichBox.Visibility = Visibility.Collapsed;
            HoverPreviewImagePanel.Visibility = Visibility.Visible;
            HoverPreviewFilesList.Visibility = Visibility.Collapsed;
            HoverPreviewImage.Source = previewImage;
            return;
        }

        HoverPreviewTextScroll.Visibility = Visibility.Collapsed;
        HoverPreviewRichBox.Visibility = Visibility.Visible;
        HoverPreviewImagePanel.Visibility = Visibility.Collapsed;
        HoverPreviewFilesList.Visibility = Visibility.Collapsed;

        try
        {
            HoverPreviewRichBox.Document = !string.IsNullOrWhiteSpace(payload.Html)
                ? RichPreviewDocumentBuilder.FromHtml(payload.Html)
                : !string.IsNullOrWhiteSpace(payload.Rtf)
                    ? RichPreviewDocumentBuilder.FromRtf(payload.Rtf)
                    : RichPreviewDocumentBuilder.FromPlainText(payload.PlainText);
        }
        catch (Exception)
        {
            HoverPreviewRichBox.Document = RichPreviewDocumentBuilder.FromPlainText(payload.PlainText);
        }
    }

    private void HideHoverPreview()
    {
        _hoverPreviewHideTimer.Stop();
        HoverPreviewPopup.IsOpen = false;
        _isPointerOverPreview = false;
    }

    private void HoverPreviewPopup_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _isPointerOverPreview = true;
        _hoverPreviewHideTimer.Stop();
    }

    private void HoverPreviewPopup_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _isPointerOverPreview = false;
        StartHoverPreviewHideTimer();
    }

    private void HoverPreviewImagePanel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (HoverPreviewImage.Source is not ImageSource imageSource)
        {
            return;
        }

        var title = _hoverPreviewCandidate?.PreviewText is { Length: > 0 } previewText
            ? previewText
            : "图片预览";
        var window = new ImagePreviewWindow(imageSource, title)
        {
            Owner = this
        };
        HideHoverPreview();
        window.Show();
        e.Handled = true;
    }

    private void StartHoverPreviewHideTimer()
    {
        _hoverPreviewHideTimer.Stop();
        _hoverPreviewHideTimer.Start();
    }

    private static string FormatDateChip(DateOnly date)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (date == today)
        {
            return $"今天 · {date.Month}月{date.Day}日";
        }

        if (date == today.AddDays(-1))
        {
            return $"昨天 · {date.Month}月{date.Day}日";
        }

        return $"{date.Month}月{date.Day}日 {FormatWeekday(date.DayOfWeek)}";
    }

    private static string FormatSourceAppChip(SourceAppSummary app)
    {
        var name = DisplaySourceAppName(app.SourceApp);
        return app.ItemCount > 1 ? $"{name} · {app.ItemCount}" : name;
    }

    private static string DisplaySourceAppName(string sourceApp)
    {
        var name = Path.GetFileNameWithoutExtension(sourceApp.Trim());
        return string.IsNullOrWhiteSpace(name) ? sourceApp : name;
    }

    private static bool SourceAppMatches(string sourceApp, string? selectedSourceApp)
    {
        return !string.IsNullOrWhiteSpace(selectedSourceApp)
               && string.Equals(sourceApp, selectedSourceApp, StringComparison.OrdinalIgnoreCase);
    }

    private static void CheckRadioButtonByTag(System.Windows.Controls.Panel panel, string tag)
    {
        foreach (var radioButton in panel.Children.OfType<System.Windows.Controls.RadioButton>())
        {
            if (string.Equals(radioButton.Tag as string, tag, StringComparison.Ordinal))
            {
                radioButton.IsChecked = true;
                return;
            }
        }
    }

    private void ScrollItemsToTopSoon()
    {
        Dispatcher.BeginInvoke(
            new Action(() =>
            {
                ItemsList.UpdateLayout();
                FindVisualChildren<ScrollViewer>(ItemsList).FirstOrDefault()?.ScrollToTop();
                if (_items.Count > 0)
                {
                    ItemsList.ScrollIntoView(_items[0]);
                }
            }),
            DispatcherPriority.Background);
    }

    private static string FormatWeekday(DayOfWeek dayOfWeek)
    {
        return dayOfWeek switch
        {
            DayOfWeek.Monday => "周一",
            DayOfWeek.Tuesday => "周二",
            DayOfWeek.Wednesday => "周三",
            DayOfWeek.Thursday => "周四",
            DayOfWeek.Friday => "周五",
            DayOfWeek.Saturday => "周六",
            DayOfWeek.Sunday => "周日",
            _ => string.Empty
        };
    }

    private static IEnumerable<T> FindVisualChildren<T>(object? parent)
        where T : DependencyObject
    {
        if (parent is not DependencyObject dependencyObject)
        {
            yield break;
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(dependencyObject); i++)
        {
            var child = VisualTreeHelper.GetChild(dependencyObject, i);
            if (child is T typedChild)
            {
                yield return typedChild;
            }

            foreach (var descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private async Task ShowToastAsync(string message)
    {
        _toastCancellation?.Cancel();
        _toastCancellation = new CancellationTokenSource();
        var token = _toastCancellation.Token;

        ToastText.Text = message;
        ToastPanel.Visibility = Visibility.Visible;
        ToastPanel.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)));

        try
        {
            await Task.Delay(1150, token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(220));
        fadeOut.Completed += (_, _) =>
        {
            if (!token.IsCancellationRequested)
            {
                ToastPanel.Visibility = Visibility.Collapsed;
            }
        };
        ToastPanel.BeginAnimation(OpacityProperty, fadeOut);
    }
}

