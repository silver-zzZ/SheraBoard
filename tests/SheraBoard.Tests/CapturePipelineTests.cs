using SheraBoard.Core.Capture;
using SheraBoard.Core.Models;
using SheraBoard.Core.Persistence;
using SheraBoard.Core.Security;
using SheraBoard.Core.Settings;

namespace SheraBoard.Tests;

public sealed class CapturePipelineTests
{
    [Fact]
    public async Task CaptureAsyncStoresRichTextWithRecoverableFormats()
    {
        using var fixture = await CoreFixture.CreateAsync();
        var pipeline = fixture.CreatePipeline();
        var capturedAt = new DateTimeOffset(2026, 5, 12, 18, 0, 0, TimeSpan.FromHours(8));

        var result = await pipeline.CaptureAsync(
            new ClipboardCapture(
                PlainText: "Hello formatted clipboard",
                Html: "<p><strong>Hello</strong> formatted clipboard</p>",
                Rtf: @"{\rtf1\b Hello\b0 formatted clipboard}",
                ImagePngBytes: null,
                ImageWidth: null,
                ImageHeight: null,
                FilePaths: []),
            capturedAt,
            "WINWORD.EXE");

        Assert.False(result.Skipped);
        Assert.NotNull(result.Item);
        Assert.Equal(ClipboardKind.RichText, result.Item.Kind);
        Assert.Contains(ClipboardFormatNames.Html, result.Item.Formats);
        Assert.Contains(ClipboardFormatNames.Rtf, result.Item.Formats);
        Assert.Contains(ClipboardFormatNames.UnicodeText, result.Item.Formats);
        Assert.Equal("WINWORD.EXE", result.Item.SourceApp);
        Assert.Equal(capturedAt, result.Item.CapturedAt);

        var payload = await fixture.PayloadStore.ReadAsync<RichTextPayload>(result.Item.PayloadRef);
        Assert.Equal("Hello formatted clipboard", payload.PlainText);
        Assert.Equal("<p><strong>Hello</strong> formatted clipboard</p>", payload.Html);
        Assert.Equal(@"{\rtf1\b Hello\b0 formatted clipboard}", payload.Rtf);

        var storedItems = await fixture.Repository.ListAsync(new ClipboardQuery());
        var item = Assert.Single(storedItems);
        Assert.Equal(result.Item.Id, item.Id);
    }

    [Fact]
    public async Task CaptureAsyncSkipsRecentDuplicateButKeepsRicherEquivalentFormat()
    {
        using var fixture = await CoreFixture.CreateAsync();
        var pipeline = fixture.CreatePipeline();
        var firstTime = new DateTimeOffset(2026, 5, 12, 18, 0, 0, TimeSpan.FromHours(8));

        var first = await pipeline.CaptureAsync(
            ClipboardCapture.FromText("repeat me"),
            firstTime,
            "notepad.exe");
        var duplicate = await pipeline.CaptureAsync(
            ClipboardCapture.FromText("repeat me"),
            firstTime.AddSeconds(2),
            "notepad.exe");
        var richer = await pipeline.CaptureAsync(
            new ClipboardCapture("repeat me", "<b>repeat me</b>", null, null, null, null, []),
            firstTime.AddSeconds(3),
            "chrome.exe");

        Assert.False(first.Skipped);
        Assert.True(duplicate.Skipped);
        Assert.Equal(CaptureSkipReason.RecentDuplicate, duplicate.SkipReason);
        Assert.False(richer.Skipped);
        Assert.Equal(ClipboardKind.RichText, richer.Item?.Kind);

        var storedItems = await fixture.Repository.ListAsync(new ClipboardQuery());
        Assert.Equal(2, storedItems.Count);
        Assert.Equal(ClipboardKind.RichText, storedItems[0].Kind);
        Assert.Equal(ClipboardKind.Text, storedItems[1].Kind);
    }

    [Fact]
    public async Task CaptureAsyncTreatsPlainHtmlWrapperAsText()
    {
        using var fixture = await CoreFixture.CreateAsync();
        var pipeline = fixture.CreatePipeline();

        var result = await pipeline.CaptureAsync(
            new ClipboardCapture(
                PlainText: "plain copied text",
                Html: """
                      Version:0.9
                      StartHTML:00000097
                      EndHTML:00000184
                      StartFragment:00000131
                      EndFragment:00000148
                      <html><body><!--StartFragment-->plain copied text<!--EndFragment--></body></html>
                      """,
                Rtf: null,
                ImagePngBytes: null,
                ImageWidth: null,
                ImageHeight: null,
                FilePaths: []),
            DateTimeOffset.UtcNow,
            "chrome.exe");

        Assert.False(result.Skipped);
        Assert.NotNull(result.Item);
        Assert.Equal(ClipboardKind.Text, result.Item.Kind);
        Assert.Contains(ClipboardFormatNames.UnicodeText, result.Item.Formats);
        Assert.DoesNotContain(ClipboardFormatNames.Html, result.Item.Formats);

        var payload = await fixture.PayloadStore.ReadAsync<TextPayload>(result.Item.PayloadRef);
        Assert.Equal("plain copied text", payload.Text);
    }

    [Fact]
    public async Task CaptureAsyncKeepsStyledHtmlAsRichText()
    {
        using var fixture = await CoreFixture.CreateAsync();
        var pipeline = fixture.CreatePipeline();

        var result = await pipeline.CaptureAsync(
            new ClipboardCapture(
                PlainText: "colored code",
                Html: """<pre><span style="color: #c678dd; font-weight: 600">colored</span> <span style="color: #61afef">code</span></pre>""",
                Rtf: null,
                ImagePngBytes: null,
                ImageWidth: null,
                ImageHeight: null,
                FilePaths: []),
            DateTimeOffset.UtcNow,
            "Code.exe");

        Assert.False(result.Skipped);
        Assert.NotNull(result.Item);
        Assert.Equal(ClipboardKind.RichText, result.Item.Kind);
        Assert.Contains(ClipboardFormatNames.Html, result.Item.Formats);

        var payload = await fixture.PayloadStore.ReadAsync<RichTextPayload>(result.Item.PayloadRef);
        Assert.Contains("color: #c678dd", payload.Html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CaptureAsyncDoesNotTreatSingleCellCodeLayoutTableAsDataTable()
    {
        using var fixture = await CoreFixture.CreateAsync();
        var pipeline = fixture.CreatePipeline();

        var result = await pipeline.CaptureAsync(
            new ClipboardCapture(
                PlainText: "def hello():\r\n    return 1",
                Html: """
                      <html><body><table style="font-family: Consolas"><tbody><tr><td>
                      <code style="color: #005cc5">def</code> <code>hello</code><code>()</code><br>
                      <code style="color: #d73a49">return</code> <code>1</code>
                      </td></tr></tbody></table></body></html>
                      """,
                Rtf: null,
                ImagePngBytes: null,
                ImageWidth: null,
                ImageHeight: null,
                FilePaths: []),
            DateTimeOffset.UtcNow,
            "chrome.exe");

        Assert.False(result.Skipped);
        Assert.NotNull(result.Item);
        Assert.Equal(ClipboardKind.RichText, result.Item.Kind);
        Assert.Contains(ClipboardFormatNames.Html, result.Item.Formats);
        Assert.DoesNotContain(ClipboardFormatNames.Table, result.Item.Formats);
    }

    [Fact]
    public async Task CaptureAsyncDoesNotTreatSyntaxHighlightedLineNumberTableAsDataTable()
    {
        using var fixture = await CoreFixture.CreateAsync();
        var pipeline = fixture.CreatePipeline();

        var result = await pipeline.CaptureAsync(
            new ClipboardCapture(
                PlainText: "1  public static void Main()\r\n2  Console.WriteLine(\"hi\");",
                Html: """
                      <html><body><table class="code-table"><tbody>
                      <tr><td class="line-number">1</td><td><pre><code><span class="hljs-keyword">public</span> <span class="hljs-keyword">static</span> void Main()</code></pre></td></tr>
                      <tr><td class="line-number">2</td><td><pre><code><span class="hljs-title">Console</span>.WriteLine(<span style="color:#98c379">"hi"</span>);</code></pre></td></tr>
                      </tbody></table></body></html>
                      """,
                Rtf: null,
                ImagePngBytes: null,
                ImageWidth: null,
                ImageHeight: null,
                FilePaths: []),
            DateTimeOffset.UtcNow,
            "chrome.exe");

        Assert.False(result.Skipped);
        Assert.NotNull(result.Item);
        Assert.Equal(ClipboardKind.RichText, result.Item.Kind);
        Assert.Contains(ClipboardFormatNames.Html, result.Item.Formats);
        Assert.DoesNotContain(ClipboardFormatNames.Table, result.Item.Formats);
    }

    [Fact]
    public async Task CaptureAsyncPreservesNativeSpreadsheetFormatsForOriginalTableRestore()
    {
        using var fixture = await CoreFixture.CreateAsync();
        var pipeline = fixture.CreatePipeline();

        var result = await pipeline.CaptureAsync(
            new ClipboardCapture(
                PlainText: "Name\tScore\r\nAlice\t98",
                Html: "<table><tr><td>Name</td><td>Score</td></tr><tr><td>Alice</td><td>98</td></tr></table>",
                Rtf: null,
                ImagePngBytes: null,
                ImageWidth: null,
                ImageHeight: null,
                FilePaths: [])
            {
                RawFormats =
                [
                    new ClipboardRawFormat("Biff12", [1, 2, 3], IsText: false),
                    new ClipboardRawFormat("XML Spreadsheet", [4, 5, 6], IsText: false)
                ]
            },
            DateTimeOffset.UtcNow,
            "EXCEL.EXE");

        Assert.False(result.Skipped);
        Assert.NotNull(result.Item);
        Assert.Equal(ClipboardKind.RichText, result.Item.Kind);
        Assert.Contains(ClipboardFormatNames.Table, result.Item.Formats);

        var payload = await fixture.PayloadStore.ReadAsync<RichTextPayload>(result.Item.PayloadRef);
        Assert.NotNull(payload.RawFormats);
        Assert.Contains(payload.RawFormats, format => format.Format == "Biff12" && format.Data.SequenceEqual(new byte[] { 1, 2, 3 }));
        Assert.Contains(payload.RawFormats, format => format.Format == "XML Spreadsheet" && format.Data.SequenceEqual(new byte[] { 4, 5, 6 }));
    }

    [Fact]
    public async Task CaptureAsyncTreatsDefaultFontHtmlAsText()
    {
        using var fixture = await CoreFixture.CreateAsync();
        var pipeline = fixture.CreatePipeline();

        var result = await pipeline.CaptureAsync(
            new ClipboardCapture(
                PlainText: "plain copied text",
                Html: """<html><body><span style="font-family: Calibri, sans-serif">plain copied text</span></body></html>""",
                Rtf: null,
                ImagePngBytes: null,
                ImageWidth: null,
                ImageHeight: null,
                FilePaths: []),
            DateTimeOffset.UtcNow,
            "chrome.exe");

        Assert.False(result.Skipped);
        Assert.NotNull(result.Item);
        Assert.Equal(ClipboardKind.Text, result.Item.Kind);
        Assert.DoesNotContain(ClipboardFormatNames.Html, result.Item.Formats);
    }

    [Fact]
    public async Task CaptureAsyncTreatsDefaultRtfColorAsText()
    {
        using var fixture = await CoreFixture.CreateAsync();
        var pipeline = fixture.CreatePipeline();

        var result = await pipeline.CaptureAsync(
            new ClipboardCapture(
                PlainText: "plain rtf text",
                Html: null,
                Rtf: @"{\rtf1\ansi{\colortbl;\red0\green0\blue0;}\cf0\fs24 plain rtf text}",
                ImagePngBytes: null,
                ImageWidth: null,
                ImageHeight: null,
                FilePaths: []),
            DateTimeOffset.UtcNow,
            "WINWORD.EXE");

        Assert.False(result.Skipped);
        Assert.NotNull(result.Item);
        Assert.Equal(ClipboardKind.Text, result.Item.Kind);
        Assert.DoesNotContain(ClipboardFormatNames.Rtf, result.Item.Formats);
    }

    [Fact]
    public async Task CaptureAsyncKeepsEditableTableFormatsAheadOfImagePreview()
    {
        using var fixture = await CoreFixture.CreateAsync();
        var pipeline = fixture.CreatePipeline();
        var imageBytes = new byte[] { 1, 2, 3, 4, 5 };

        var result = await pipeline.CaptureAsync(
            new ClipboardCapture(
                PlainText: "Name\tScore\r\nAlice\t98\r\nBob\t95",
                Html: """
                      Version:0.9
                      StartHTML:00000097
                      EndHTML:00000280
                      StartFragment:00000131
                      EndFragment:00000244
                      <html><body><!--StartFragment--><table><tr><td>Name</td><td>Score</td></tr><tr><td>Alice</td><td>98</td></tr></table><!--EndFragment--></body></html>
                      """,
                Rtf: null,
                ImagePngBytes: imageBytes,
                ImageWidth: 320,
                ImageHeight: 120,
                FilePaths: []),
            DateTimeOffset.UtcNow,
            "EXCEL.EXE");

        Assert.False(result.Skipped);
        Assert.NotNull(result.Item);
        Assert.Equal(ClipboardKind.RichText, result.Item.Kind);
        Assert.Contains(ClipboardFormatNames.Html, result.Item.Formats);
        Assert.Contains(ClipboardFormatNames.Png, result.Item.Formats);
        Assert.Contains(ClipboardFormatNames.Table, result.Item.Formats);

        var payload = await fixture.PayloadStore.ReadAsync<RichTextPayload>(result.Item.PayloadRef);
        Assert.Equal("Name\tScore\r\nAlice\t98\r\nBob\t95", payload.PlainText);
        Assert.Equal(imageBytes, payload.PreviewPngBytes);
        Assert.Equal(320, payload.PreviewWidth);
        Assert.Equal(120, payload.PreviewHeight);
    }

    [Fact]
    public async Task CaptureAsyncSkipsLowerFidelityDuplicateTableUpdateWithinExtendedWindow()
    {
        using var fixture = await CoreFixture.CreateAsync();
        var pipeline = fixture.CreatePipeline();
        var firstTime = new DateTimeOffset(2026, 5, 27, 9, 0, 0, TimeSpan.FromHours(8));

        var rich = await pipeline.CaptureAsync(
            new ClipboardCapture(
                PlainText: "Name\tScore\r\nAlice\t98",
                Html: "<table><tr><td>Name</td><td>Score</td></tr><tr><td>Alice</td><td>98</td></tr></table>",
                Rtf: @"{\rtf1\trowd\cellx1000\cellx2000 Name\cell Score\cell\row Alice\cell 98\cell\row}",
                ImagePngBytes: new byte[] { 9, 8, 7 },
                ImageWidth: 200,
                ImageHeight: 80,
                FilePaths: []),
            firstTime,
            "EXCEL.EXE");

        var lowerFidelity = await pipeline.CaptureAsync(
            new ClipboardCapture(
                PlainText: "Name\tScore\r\nAlice\t98",
                Html: null,
                Rtf: @"{\rtf1\trowd\cellx1000\cellx2000 Name\cell Score\cell\row Alice\cell 98\cell\row}",
                ImagePngBytes: null,
                ImageWidth: null,
                ImageHeight: null,
                FilePaths: []),
            firstTime.AddSeconds(9),
            "EXCEL.EXE");

        Assert.False(rich.Skipped);
        Assert.True(lowerFidelity.Skipped);
        Assert.Equal(CaptureSkipReason.RecentDuplicate, lowerFidelity.SkipReason);

        var storedItems = await fixture.Repository.ListAsync(new ClipboardQuery());
        var item = Assert.Single(storedItems);
        Assert.Contains(ClipboardFormatNames.Png, item.Formats);
    }

    [Fact]
    public async Task CaptureAsyncTagsWordRtfTableAsTableRichText()
    {
        using var fixture = await CoreFixture.CreateAsync();
        var pipeline = fixture.CreatePipeline();

        var result = await pipeline.CaptureAsync(
            new ClipboardCapture(
                PlainText: "Name\tScore\r\nAlice\t98",
                Html: null,
                Rtf: @"{\rtf1\trowd\cellx1000\cellx2000 Name\cell Score\cell\row Alice\cell 98\cell\row}",
                ImagePngBytes: null,
                ImageWidth: null,
                ImageHeight: null,
                FilePaths: []),
            DateTimeOffset.UtcNow,
            "WINWORD.EXE");

        Assert.False(result.Skipped);
        Assert.NotNull(result.Item);
        Assert.Equal(ClipboardKind.RichText, result.Item.Kind);
        Assert.Contains(ClipboardFormatNames.Rtf, result.Item.Formats);
        Assert.Contains(ClipboardFormatNames.Table, result.Item.Formats);
    }

    [Fact]
    public async Task CaptureAsyncRecordsFileListMetadataWithoutCopyingFileContents()
    {
        using var fixture = await CoreFixture.CreateAsync();
        var pipeline = fixture.CreatePipeline();
        var sourceFile = Path.Combine(fixture.Root, "source.txt");
        await File.WriteAllTextAsync(sourceFile, "original file contents");

        var result = await pipeline.CaptureAsync(
            ClipboardCapture.FromFiles([sourceFile, Path.Combine(fixture.Root, "missing-folder")]),
            DateTimeOffset.UtcNow,
            "explorer.exe");

        Assert.False(result.Skipped);
        Assert.NotNull(result.Item);
        Assert.Equal(ClipboardKind.FileList, result.Item.Kind);
        Assert.Contains("source.txt", result.Item.PreviewText, StringComparison.OrdinalIgnoreCase);

        var payload = await fixture.PayloadStore.ReadAsync<FileListPayload>(result.Item.PayloadRef);
        Assert.Null(payload.SnapshotRef);
        Assert.Equal(2, payload.Items.Count);
        Assert.Contains(payload.Items, item => item.OriginalPath == sourceFile && item.Exists);
        Assert.DoesNotContain(
            Directory.EnumerateFiles(fixture.Paths.PayloadsDirectory, "*", SearchOption.AllDirectories),
            path => string.Equals(Path.GetFileName(path), "source.txt", StringComparison.OrdinalIgnoreCase));
    }
}
