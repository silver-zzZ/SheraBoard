namespace SheraBoard.Tests;

public sealed class MainWindowXamlPerformanceTests
{
    [Fact]
    public void HistoryListEnablesVirtualizationForLargeClipboardHistories()
    {
        var xaml = File.ReadAllText(FindProjectFile("src", "SheraBoard.App", "MainWindow.xaml"));

        Assert.Contains("VirtualizingPanel.IsVirtualizing=\"True\"", xaml);
        Assert.Contains("VirtualizingPanel.IsVirtualizingWhenGrouping=\"True\"", xaml);
        Assert.Contains("VirtualizingPanel.VirtualizationMode=\"Recycling\"", xaml);
        Assert.Contains("<VirtualizingStackPanel", xaml);
    }

    [Fact]
    public void MainWindowDefaultsToTodayInsteadOfAllHistory()
    {
        var xaml = File.ReadAllText(FindProjectFile("src", "SheraBoard.App", "MainWindow.xaml"));

        Assert.DoesNotContain("x:Name=\"DateAllFilter\" Content=\"全部\" Tag=\"All\" GroupName=\"DateFilter\" IsChecked=\"True\"", xaml);
        Assert.Contains("Content=\"今天\" Tag=\"Today\" GroupName=\"DateFilter\" IsChecked=\"True\"", xaml);
    }

    [Fact]
    public void RowContextMenuUsesPerItemCopyOptionVisibility()
    {
        var xaml = File.ReadAllText(FindProjectFile("src", "SheraBoard.App", "MainWindow.xaml"));

        Assert.Contains("Visibility=\"{Binding CopyOriginalVisibility}\"", xaml);
        Assert.Contains("Visibility=\"{Binding CopyPlainTextVisibility}\"", xaml);
        Assert.Contains("Visibility=\"{Binding CopyImageVisibility}\"", xaml);
    }

    [Fact]
    public void MainWindowCanMinimizeFromTaskbarButCannotResize()
    {
        var xaml = File.ReadAllText(FindProjectFile("src", "SheraBoard.App", "MainWindow.xaml"));

        Assert.Contains("ResizeMode=\"CanMinimize\"", xaml);
        Assert.DoesNotContain("ResizeMode=\"NoResize\"", xaml);
    }

    [Fact]
    public void SearchAreaUsesModernSearchAndFeatureFilters()
    {
        var xaml = File.ReadAllText(FindProjectFile("src", "SheraBoard.App", "MainWindow.xaml"));

        Assert.Contains("x:Name=\"SearchPlaceholder\"", xaml);
        Assert.Contains("搜索内容、来源应用、链接、代码", xaml);
        Assert.Contains("Tag=\"Feature:Url\"", xaml);
        Assert.Contains("Tag=\"Feature:Code\"", xaml);
        Assert.Contains("x:Name=\"SourceAppFilterButton\"", xaml);
        Assert.Contains("x:Name=\"SourceAppPopup\"", xaml);
        Assert.Contains("x:Name=\"SourceAppPopupList\"", xaml);
        Assert.DoesNotContain("x:Name=\"SourceAppFiltersPanel\"", xaml);
    }

    [Fact]
    public void SearchInputTextIsVerticallyCenteredAndAlignedAfterIcon()
    {
        var appXaml = File.ReadAllText(FindProjectFile("src", "SheraBoard.App", "App.xaml"));
        var mainXaml = File.ReadAllText(FindProjectFile("src", "SheraBoard.App", "MainWindow.xaml"));

        Assert.Contains("<Style x:Key=\"SearchTextBoxStyle\"", appXaml);
        Assert.Contains("<Setter Property=\"Height\" Value=\"42\"/>", appXaml);
        Assert.Contains("<Setter Property=\"Padding\" Value=\"0,0,12,1\"/>", appXaml);
        Assert.Contains("<Setter Property=\"TextAlignment\" Value=\"Left\"/>", appXaml);
        Assert.Contains("<Setter Property=\"VerticalContentAlignment\" Value=\"Center\"/>", appXaml);
        Assert.Contains("VerticalAlignment=\"{TemplateBinding VerticalContentAlignment}\"", appXaml);
        Assert.Contains("<ColumnDefinition Width=\"42\"/>", mainXaml);
        Assert.Contains("Grid.Column=\"1\"", mainXaml);
        Assert.Contains("Margin=\"0,0,12,1\"", mainXaml);
    }

    [Fact]
    public void SourceAppFilterIsCompactPopupInsteadOfWrappingRecentApps()
    {
        var xaml = File.ReadAllText(FindProjectFile("src", "SheraBoard.App", "MainWindow.xaml"));

        Assert.Contains("应用：全部", xaml);
        Assert.Contains("x:Key=\"SourceAppPopupScrollBarStyle\"", xaml);
        Assert.Contains("Width=\"6\"", xaml);
        Assert.Contains("MaxHeight=\"260\"", xaml);
        Assert.Contains("最近", xaml);
        Assert.DoesNotContain("也可以直接搜索", xaml);
        Assert.DoesNotContain("Grid.Row=\"3\" Margin=\"2,7,0,0\"/>", xaml);
    }

    [Fact]
    public void PrimaryButtonsUseLightTonalStylingInsteadOfDarkFilledGreen()
    {
        var xaml = File.ReadAllText(FindProjectFile("src", "SheraBoard.App", "App.xaml"));

        Assert.Contains("TextElement.Foreground=\"{TemplateBinding Foreground}\"", xaml);
        Assert.Contains("<Style x:Key=\"PrimaryButtonStyle\"", xaml);
        Assert.Contains("<Setter Property=\"Background\" Value=\"#E8F5F3\"/>", xaml);
        Assert.Contains("<Setter Property=\"Foreground\" Value=\"#0E6B64\"/>", xaml);
        Assert.DoesNotContain("<Setter Property=\"Foreground\" Value=\"White\"/>\r\n            <Setter Property=\"Background\" Value=\"{StaticResource AccentBrush}\"/>", xaml);
    }

    private static string FindProjectFile(params string[] relativeParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate {Path.Combine(relativeParts)} from {AppContext.BaseDirectory}.");
    }
}
