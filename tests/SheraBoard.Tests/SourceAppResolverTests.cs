using SheraBoard.Core.Capture;

namespace SheraBoard.Tests;

public sealed class SourceAppResolverTests
{
    [Theory]
    [InlineData("WINWORD.exe", "Windows.exe", "WINWORD.exe")]
    [InlineData("Windows.exe", "WINWORD.exe", "WINWORD.exe")]
    [InlineData("ApplicationFrameHost.exe", "chrome.exe", "chrome.exe")]
    [InlineData(null, "Code.exe", "Code.exe")]
    [InlineData("Windows.exe", "Windows.exe", null)]
    [InlineData("SheraBoard.exe", "WINWORD.exe", "WINWORD.exe")]
    public void ChooseBestProcessNamePrefersRealClipboardOrForegroundApplication(
        string? clipboardOwner,
        string? foreground,
        string? expected)
    {
        Assert.Equal(expected, SourceAppResolver.ChooseBestProcessName(clipboardOwner, foreground));
    }
}
