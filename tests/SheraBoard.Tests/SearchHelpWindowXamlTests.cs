namespace SheraBoard.Tests;

public sealed class SearchHelpWindowXamlTests
{
    [Fact]
    public void SettingsWindowExposesSearchHelpEntryPoint()
    {
        var settingsXaml = File.ReadAllText(FindProjectFile("src", "SheraBoard.App", "SettingsWindow.xaml"));
        var settingsCode = File.ReadAllText(FindProjectFile("src", "SheraBoard.App", "SettingsWindow.xaml.cs"));

        Assert.Contains("x:Name=\"SearchHelpButton\"", settingsXaml);
        Assert.Contains("搜索用法", settingsXaml);
        Assert.Contains("SearchHelpButton_Click", settingsXaml);
        Assert.Contains("new SearchHelpWindow", settingsCode);
    }

    [Fact]
    public void SearchHelpWindowDocumentsSupportedSearchSyntax()
    {
        var xaml = File.ReadAllText(FindProjectFile("src", "SheraBoard.App", "SearchHelpWindow.xaml"));

        Assert.Contains("SheraBoard 搜索用法", xaml);
        Assert.Contains("app:vscode", xaml);
        Assert.Contains("type:image", xaml);
        Assert.Contains("has:url", xaml);
        Assert.Contains("has:code", xaml);
        Assert.Contains("is:pinned", xaml);
        Assert.Contains("app:vscode has:code class", xaml);
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
