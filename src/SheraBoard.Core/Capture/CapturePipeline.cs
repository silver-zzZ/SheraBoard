using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using SheraBoard.Core.Models;
using SheraBoard.Core.Persistence;
using SheraBoard.Core.Settings;

namespace SheraBoard.Core.Capture;

public sealed partial class CapturePipeline
{
    private readonly StorageRepository _repository;
    private readonly FilePayloadStore _payloadStore;
    private readonly AppSettings _settings;

    public CapturePipeline(StorageRepository repository, FilePayloadStore payloadStore, AppSettings settings)
    {
        _repository = repository;
        _payloadStore = payloadStore;
        _settings = settings;
    }

    public async Task<CaptureResult> CaptureAsync(
        ClipboardCapture capture,
        DateTimeOffset capturedAt,
        string? sourceApp,
        CancellationToken cancellationToken = default)
    {
        if (_settings.CapturePaused)
        {
            return CaptureResult.Skip(CaptureSkipReason.CapturePaused);
        }

        if (IsIgnoredProcess(sourceApp))
        {
            return CaptureResult.Skip(CaptureSkipReason.IgnoredProcess);
        }

        var normalized = Normalize(capture);
        if (normalized is null)
        {
            return CaptureResult.Skip(CaptureSkipReason.Empty);
        }

        var latest = await _repository.GetLatestAsync(cancellationToken);
        if (latest is not null && IsRecentDuplicate(latest, normalized, capturedAt))
        {
            return CaptureResult.Skip(CaptureSkipReason.RecentDuplicate);
        }

        var id = Guid.NewGuid();
        var payloadRef = await WritePayloadAsync(id, normalized, cancellationToken);
        var item = new ClipboardItemRecord(
            id,
            capturedAt,
            normalized.Kind,
            normalized.PreviewText,
            sourceApp,
            normalized.Formats,
            normalized.SizeBytes,
            Pinned: false,
            Favorite: false,
            payloadRef,
            normalized.ContentHash,
            normalized.SemanticHash,
            normalized.FormatScore);

        await _repository.InsertAsync(item, cancellationToken);
        return CaptureResult.Captured(item);
    }

    private bool IsIgnoredProcess(string? sourceApp)
    {
        if (string.IsNullOrWhiteSpace(sourceApp))
        {
            return false;
        }

        return _settings.IgnoredProcesses.Any(
            process => string.Equals(process.Trim(), sourceApp.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private bool IsRecentDuplicate(
        ClipboardItemRecord latest,
        NormalizedCapture normalized,
        DateTimeOffset capturedAt)
    {
        if (latest.SemanticHash != normalized.SemanticHash || normalized.FormatScore > latest.FormatScore)
        {
            return false;
        }

        var age = capturedAt - latest.CapturedAt;
        var normalWindow = TimeSpan.FromSeconds(Math.Max(1, _settings.DeduplicateWindowSeconds));
        if (age <= normalWindow)
        {
            return true;
        }

        var extendedWindow = TimeSpan.FromSeconds(Math.Max(
            _settings.DeduplicateWindowSeconds,
            _settings.RichContentDeduplicateWindowSeconds));
        return (latest.Kind == ClipboardKind.RichText || normalized.Kind == ClipboardKind.RichText) &&
               age <= extendedWindow;
    }

    private NormalizedCapture? Normalize(ClipboardCapture capture)
    {
        if (capture.FilePaths.Count > 0)
        {
            var entries = capture.FilePaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(CreateFileEntry)
                .ToList();
            if (entries.Count == 0)
            {
                return null;
            }

            var semanticText = string.Join('\n', entries.Select(item => item.OriginalPath).Order(StringComparer.OrdinalIgnoreCase));
            var payload = new FileListPayload(entries, SnapshotRef: null);
            return new NormalizedCapture(
                ClipboardKind.FileList,
                [ClipboardFormatNames.FileDrop],
                PreviewFiles(entries),
                payload,
                EstimateStringBytes(semanticText),
                Hash("files:semantic:" + semanticText.ToLowerInvariant()),
                Hash("files:content:" + semanticText.ToLowerInvariant()),
                FormatScore: 40);
        }

        var hasHtml = !string.IsNullOrWhiteSpace(capture.Html);
        var hasRtf = !string.IsNullOrWhiteSpace(capture.Rtf);
        var hasText = !string.IsNullOrWhiteSpace(capture.PlainText);
        var hasImage = capture.ImagePngBytes is { Length: > 0 };
        var hasTabularText = hasText && LooksLikeTabularText(capture.PlainText!);
        var rawFormats = NormalizeRawFormats(capture.RawFormats);
        var hasNativeSpreadsheetFormat = rawFormats.Any(format => IsNativeSpreadsheetFormat(format.Format));
        var htmlLooksLikeCodeLayout = HtmlLooksCodeLayout(capture.Html);
        var hasStructuredTable =
            HtmlLooksTabular(capture.Html) && !(htmlLooksLikeCodeLayout && !hasTabularText) ||
            RtfLooksTabular(capture.Rtf);
        var hasTable = hasStructuredTable || hasTabularText && (hasHtml || hasRtf || hasImage);
        if (hasNativeSpreadsheetFormat && hasTabularText)
        {
            hasTable = true;
        }

        var hasRichFormatting = (hasHtml || hasRtf) && HasMeaningfulRichFormatting(capture.Html, capture.Rtf);

        if ((hasHtml || hasRtf) && (!hasText || hasRichFormatting || hasTable) ||
            hasImage && hasTabularText ||
            hasNativeSpreadsheetFormat && hasText)
        {
            var plainText = hasText ? capture.PlainText! : ExtractReadableText(capture.Html ?? capture.Rtf ?? string.Empty);
            var formats = new List<string>();
            if (hasTable)
            {
                formats.Add(ClipboardFormatNames.Table);
            }

            if (hasHtml)
            {
                formats.Add(ClipboardFormatNames.Html);
            }

            if (hasRtf)
            {
                formats.Add(ClipboardFormatNames.Rtf);
            }

            if (!string.IsNullOrWhiteSpace(plainText))
            {
                formats.Add(ClipboardFormatNames.UnicodeText);
            }

            if (hasImage)
            {
                formats.Add(ClipboardFormatNames.Png);
            }

            var payload = new RichTextPayload(
                plainText,
                capture.Html,
                capture.Rtf,
                capture.ImagePngBytes,
                capture.ImageWidth,
                capture.ImageHeight,
                rawFormats);
            return new NormalizedCapture(
                ClipboardKind.RichText,
                formats,
                PreviewText(plainText),
                payload,
                EstimateStringBytes(plainText, capture.Html, capture.Rtf) +
                (capture.ImagePngBytes?.LongLength ?? 0) +
                rawFormats.Sum(format => (long)format.Data.Length),
                Hash("text:semantic:" + NormalizeTextForHash(plainText)),
                Hash("rich:content:" + string.Join(
                    '\u001f',
                    formats,
                    plainText,
                    capture.Html,
                    capture.Rtf,
                    capture.ImagePngBytes is null ? string.Empty : HashBytes("preview:", capture.ImagePngBytes),
                    string.Join('|', rawFormats.Select(format => $"{format.Format}:{HashBytes("raw:", format.Data)}")))),
                FormatScore: 20 + formats.Count + (hasTable ? 10 : 0) + Math.Min(20, rawFormats.Count * 4));
        }

        if (capture.ImagePngBytes is { Length: > 0 } imageBytes)
        {
            var dimensions = capture.ImageWidth is not null && capture.ImageHeight is not null
                ? $"{capture.ImageWidth} x {capture.ImageHeight}"
                : "unknown size";
            var payload = new ImagePayload(imageBytes, capture.ImageWidth, capture.ImageHeight);
            var contentHash = HashBytes("image:content:", imageBytes);
            return new NormalizedCapture(
                ClipboardKind.Image,
                [ClipboardFormatNames.Png],
                $"Image ({dimensions})",
                payload,
                imageBytes.LongLength,
                HashBytes("image:semantic:", imageBytes),
                contentHash,
                FormatScore: 30);
        }

        if (hasText)
        {
            var text = capture.PlainText!;
            var payload = new TextPayload(text);
            return new NormalizedCapture(
                ClipboardKind.Text,
                [ClipboardFormatNames.UnicodeText],
                PreviewText(text),
                payload,
                EstimateStringBytes(text),
                Hash("text:semantic:" + NormalizeTextForHash(text)),
                Hash("text:content:" + text),
                FormatScore: 1);
        }

        return null;
    }

    private async Task<string> WritePayloadAsync(
        Guid id,
        NormalizedCapture normalized,
        CancellationToken cancellationToken)
    {
        return normalized.Payload switch
        {
            TextPayload payload => await _payloadStore.WriteAsync(id, payload, cancellationToken),
            RichTextPayload payload => await _payloadStore.WriteAsync(id, payload, cancellationToken),
            ImagePayload payload => await _payloadStore.WriteAsync(id, payload, cancellationToken),
            FileListPayload payload => await _payloadStore.WriteAsync(id, payload, cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported payload type {normalized.Payload.GetType().FullName}.")
        };
    }

    private string PreviewText(string text)
    {
        var normalized = WhitespaceRegex().Replace(text, " ").Trim();
        if (normalized.Length <= _settings.MaxPreviewChars)
        {
            return normalized;
        }

        return normalized[..Math.Max(0, _settings.MaxPreviewChars - 1)] + "…";
    }

    private static string PreviewFiles(IReadOnlyList<FileListEntry> entries)
    {
        var names = entries.Take(3).Select(item => item.Name);
        var preview = string.Join(", ", names);
        return entries.Count > 3 ? $"{preview} +{entries.Count - 3} more" : preview;
    }

    private static FileListEntry CreateFileEntry(string path)
    {
        if (Directory.Exists(path))
        {
            var directory = new DirectoryInfo(path);
            return new FileListEntry(path, directory.Name, Exists: true, IsDirectory: true, null, directory.LastWriteTimeUtc);
        }

        if (File.Exists(path))
        {
            var file = new FileInfo(path);
            return new FileListEntry(path, file.Name, Exists: true, IsDirectory: false, file.Length, file.LastWriteTimeUtc);
        }

        return new FileListEntry(path, Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)), Exists: false, IsDirectory: false, null, null);
    }

    private static long EstimateStringBytes(params string?[] parts)
    {
        return parts.Where(part => part is not null).Sum(part => (long)Encoding.UTF8.GetByteCount(part!));
    }

    private static string NormalizeTextForHash(string text)
    {
        return WhitespaceRegex().Replace(text, " ").Trim();
    }

    private static string ExtractReadableText(string text)
    {
        return TagRegex().Replace(text, " ");
    }

    private static bool HasMeaningfulRichFormatting(string? html, string? rtf)
    {
        if (!string.IsNullOrWhiteSpace(html) && HtmlLooksFormatted(html))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(rtf) && RtfLooksFormatted(rtf);
    }

    private static List<ClipboardRawFormat> NormalizeRawFormats(IReadOnlyList<ClipboardRawFormat>? rawFormats)
    {
        if (rawFormats is null || rawFormats.Count == 0)
        {
            return [];
        }

        return rawFormats
            .Where(format => IsNativeSpreadsheetFormat(format.Format) && format.Data.Length > 0)
            .GroupBy(format => format.Format.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First() with { Format = group.Key })
            .OrderBy(format => NativeSpreadsheetFormatPriority(format.Format))
            .ToList();
    }

    private static bool IsNativeSpreadsheetFormat(string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return false;
        }

        var normalized = format.Trim();
        return normalized.Equals("Biff12", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("Biff8", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("Biff5", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("Biff", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("XML Spreadsheet", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("Microsoft Excel Worksheet", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("SYLK", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("DIF", StringComparison.OrdinalIgnoreCase);
    }

    private static int NativeSpreadsheetFormatPriority(string format)
    {
        if (format.Equals("Biff12", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (format.Equals("Biff8", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (format.Equals("Biff5", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (format.Equals("Biff", StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (format.Equals("XML Spreadsheet", StringComparison.OrdinalIgnoreCase))
        {
            return 4;
        }

        if (format.Equals("Microsoft Excel Worksheet", StringComparison.OrdinalIgnoreCase))
        {
            return 5;
        }

        if (format.Equals("SYLK", StringComparison.OrdinalIgnoreCase))
        {
            return 10;
        }

        if (format.Equals("DIF", StringComparison.OrdinalIgnoreCase))
        {
            return 11;
        }

        return 100;
    }

    private static bool HtmlLooksFormatted(string html)
    {
        var fragment = ExtractHtmlFragment(html);
        return RichHtmlStyleRegex().IsMatch(fragment) || RichHtmlTagRegex().IsMatch(fragment);
    }

    private static bool HtmlLooksTabular(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return false;
        }

        var fragment = ExtractHtmlFragment(html);
        if (!HtmlTableRegex().IsMatch(fragment))
        {
            return false;
        }

        var cellCount = HtmlTableCellRegex().Matches(fragment).Count;
        if (cellCount < 2)
        {
            return false;
        }

        var rowCount = HtmlTableRowRegex().Matches(fragment).Count;
        return rowCount >= 1;
    }

    private static bool HtmlLooksCodeLayout(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return false;
        }

        var fragment = ExtractHtmlFragment(html);
        return RichHtmlTagRegex().IsMatch(fragment) &&
               (CodeHtmlClassRegex().IsMatch(fragment) || CodeHtmlTagRegex().IsMatch(fragment));
    }

    private static string ExtractHtmlFragment(string html)
    {
        const string startMarker = "<!--StartFragment-->";
        const string endMarker = "<!--EndFragment-->";

        var start = html.IndexOf(startMarker, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return html;
        }

        start += startMarker.Length;
        var end = html.IndexOf(endMarker, start, StringComparison.OrdinalIgnoreCase);
        return end > start ? html[start..end] : html[start..];
    }

    private static bool RtfLooksFormatted(string rtf)
    {
        if (RichRtfCommandRegex().IsMatch(rtf))
        {
            return true;
        }

        if (RichRtfColorCommandRegex().Matches(rtf)
            .Any(match => int.TryParse(match.Groups["index"].Value, out var index) && index > 0))
        {
            return true;
        }

        return RtfFontSizeRegex().Matches(rtf)
            .Select(match => int.TryParse(match.Groups["size"].Value, out var size) ? size : 24)
            .Any(size => size is < 22 or > 26);
    }

    private static bool RtfLooksTabular(string? rtf)
    {
        return !string.IsNullOrWhiteSpace(rtf) && RtfTableRegex().IsMatch(rtf);
    }

    private static bool LooksLikeTabularText(string text)
    {
        var lines = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
        {
            return false;
        }

        return lines.Count(line => line.Contains('\t')) >= 1 &&
               (lines.Length > 1 || lines[0].Count(character => character == '\t') >= 2);
    }

    private static string Hash(string text)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    private static string HashBytes(string prefix, byte[] bytes)
    {
        var prefixBytes = Encoding.UTF8.GetBytes(prefix);
        var combined = new byte[prefixBytes.Length + bytes.Length];
        Buffer.BlockCopy(prefixBytes, 0, combined, 0, prefixBytes.Length);
        Buffer.BlockCopy(bytes, 0, combined, prefixBytes.Length, bytes.Length);
        return Convert.ToHexString(SHA256.HashData(combined));
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex("<.*?>")]
    private static partial Regex TagRegex();

    [GeneratedRegex("""\b(style|class)\s*=\s*["'][^"']*(color|background|font-size|font-weight|font-style|text-decoration|syntax|token|hljs|code|language|cm-|mtk)[^"']*["']""", RegexOptions.IgnoreCase)]
    private static partial Regex RichHtmlStyleRegex();

    [GeneratedRegex("""<\s*(strong|b|em|i|u|s|strike|mark|sub|sup|h[1-6]|pre|code|ul|ol|li|blockquote|a)\b""", RegexOptions.IgnoreCase)]
    private static partial Regex RichHtmlTagRegex();

    [GeneratedRegex("""\b(class|style)\s*=\s*["'][^"']*(hljs|token|code|language-|line-number|line-numbers|syntax|cm-|mtk)[^"']*["']""", RegexOptions.IgnoreCase)]
    private static partial Regex CodeHtmlClassRegex();

    [GeneratedRegex("""<\s*(pre|code)\b""", RegexOptions.IgnoreCase)]
    private static partial Regex CodeHtmlTagRegex();

    [GeneratedRegex("""<\s*(table|thead|tbody|tfoot|tr|td|th)\b""", RegexOptions.IgnoreCase)]
    private static partial Regex HtmlTableRegex();

    [GeneratedRegex("""<\s*(td|th)\b""", RegexOptions.IgnoreCase)]
    private static partial Regex HtmlTableCellRegex();

    [GeneratedRegex("""<\s*tr\b""", RegexOptions.IgnoreCase)]
    private static partial Regex HtmlTableRowRegex();

    [GeneratedRegex("""\\(b|i|ul|strike|super|sub|qc|qr|qj|bullet|listtext)\b""", RegexOptions.IgnoreCase)]
    private static partial Regex RichRtfCommandRegex();

    [GeneratedRegex("""\\(?:cf|highlight)(?<index>\d+)\b""", RegexOptions.IgnoreCase)]
    private static partial Regex RichRtfColorCommandRegex();

    [GeneratedRegex("""\\(trowd|cellx|cell|row)\b""", RegexOptions.IgnoreCase)]
    private static partial Regex RtfTableRegex();

    [GeneratedRegex("""\\fs(?<size>\d+)""", RegexOptions.IgnoreCase)]
    private static partial Regex RtfFontSizeRegex();

    private sealed record NormalizedCapture(
        ClipboardKind Kind,
        List<string> Formats,
        string PreviewText,
        object Payload,
        long SizeBytes,
        string SemanticHash,
        string ContentHash,
        int FormatScore);
}
