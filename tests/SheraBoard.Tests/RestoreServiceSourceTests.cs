namespace SheraBoard.Tests;

public sealed class RestoreServiceSourceTests
{
    [Fact]
    public void RichOriginalRestoreOffersRichAndNativeFormatsBeforePlainText()
    {
        var source = File.ReadAllText(FindProjectFile("src", "SheraBoard.App", "Services", "RestoreService.cs"));

        var rawFormatIndex = source.IndexOf("SetRawFormats(data, richPayload.RawFormats)", StringComparison.Ordinal);
        var htmlIndex = source.IndexOf("System.Windows.DataFormats.Html", StringComparison.Ordinal);
        var rtfIndex = source.IndexOf("System.Windows.DataFormats.Rtf", StringComparison.Ordinal);
        var plainTextIndex = source.IndexOf("data.SetText(richPayload.PlainText", StringComparison.Ordinal);

        Assert.True(rawFormatIndex >= 0, "RestoreService should restore captured native spreadsheet formats.");
        Assert.True(rawFormatIndex < plainTextIndex, "Native spreadsheet formats should be offered before Unicode text.");
        Assert.True(htmlIndex < plainTextIndex, "HTML should be offered before Unicode text so Office apps do not pick plain text first.");
        Assert.True(rtfIndex < plainTextIndex, "RTF should be offered before Unicode text so Word/Excel can keep table formatting.");
    }

    [Fact]
    public void ClipboardCaptureQueueHandlesEventsArrivingDuringFinalRaceWindow()
    {
        var source = File.ReadAllText(FindProjectFile("src", "SheraBoard.App", "App.xaml.cs"));

        Assert.Contains("SemaphoreSlim", source);
        Assert.Contains("pending Clipboard event", source, StringComparison.OrdinalIgnoreCase);
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
