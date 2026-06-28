namespace SheraBoard.Tests;

public sealed class ClipboardCaptureResponsivenessTests
{
    [Fact]
    public void ClipboardReaderDoesNotEagerlyFetchNativeSpreadsheetFormatsOnClipboardThread()
    {
        var source = File.ReadAllText(FindProjectFile("src", "SheraBoard.App", "Services", "ClipboardReader.cs"));
        var tryReadBody = Between(source, "public ClipboardCapture? TryRead()", "private static string? ReadString");

        Assert.DoesNotContain("ReadNativeSpreadsheetFormats", tryReadBody, StringComparison.Ordinal);
        Assert.DoesNotContain("GetData(format, autoConvert: false)", tryReadBody, StringComparison.Ordinal);
    }

    [Fact]
    public void ClipboardCapturePersistsPayloadOffUiThread()
    {
        var source = File.ReadAllText(FindProjectFile("src", "SheraBoard.App", "App.xaml.cs"));
        var captureBody = Between(source, "private async Task CaptureClipboardOnceAsync()", "private async Task<SheraBoard.Core.Capture.ClipboardCapture?> TryReadClipboardWithRetryAsync()");

        Assert.Contains("Task.Run", captureBody, StringComparison.Ordinal);
        Assert.Contains("Pipeline.CaptureAsync", captureBody, StringComparison.Ordinal);
        Assert.Contains("RetentionService.EnforceCapacityAsync", captureBody, StringComparison.Ordinal);
    }

    private static string Between(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Missing start marker: {startMarker}");
        var end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.True(end > start, $"Missing end marker: {endMarker}");
        return source[start..end];
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
