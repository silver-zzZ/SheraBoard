using SheraBoard.Core.Models;
using SheraBoard.Core.Persistence;

namespace SheraBoard.Tests;

public sealed class ClipboardSearchParserTests
{
    [Fact]
    public void ParseExtractsFiltersAndKeepsPlainTerms()
    {
        var parsed = ClipboardSearchParser.Parse("app:vscode type:image has:url is:pinned config \"two words\"");

        Assert.Equal("vscode", parsed.SourceApp);
        Assert.Equal(ClipboardKind.Image, parsed.Kind);
        Assert.True(parsed.PinnedOnly);
        Assert.Contains(ClipboardContentFeature.Url, parsed.Features);
        Assert.Equal(["config", "two words"], parsed.SearchTerms);
    }

    [Theory]
    [InlineData("type:text", ClipboardKind.Text)]
    [InlineData("type:rich", ClipboardKind.RichText)]
    [InlineData("type:html", ClipboardKind.RichText)]
    [InlineData("type:image", ClipboardKind.Image)]
    [InlineData("type:file", ClipboardKind.FileList)]
    public void ParseSupportsFriendlyTypeAliases(string input, ClipboardKind expectedKind)
    {
        var parsed = ClipboardSearchParser.Parse(input);

        Assert.Equal(expectedKind, parsed.Kind);
        Assert.Empty(parsed.SearchTerms);
    }

    [Fact]
    public void ParseSupportsCodeFeatureAliases()
    {
        var parsed = ClipboardSearchParser.Parse("has:code source:chrome hello");

        Assert.Equal("chrome", parsed.SourceApp);
        Assert.Contains(ClipboardContentFeature.Code, parsed.Features);
        Assert.Equal(["hello"], parsed.SearchTerms);
    }
}
