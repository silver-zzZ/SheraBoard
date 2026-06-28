using System.Collections.Specialized;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media.Imaging;
using SheraBoard.Core.Models;
using SheraBoard.Core.Persistence;

namespace SheraBoard.App.Services;

public sealed class RestoreService
{
    private readonly FilePayloadStore _payloadStore;

    public RestoreService(FilePayloadStore payloadStore)
    {
        _payloadStore = payloadStore;
    }

    public async Task RestoreAsync(
        ClipboardItemRecord item,
        RestoreMode mode = RestoreMode.Original,
        CancellationToken cancellationToken = default)
    {
        var data = new System.Windows.DataObject();

        switch (item.Kind)
        {
            case ClipboardKind.Text:
                var textPayload = await _payloadStore.ReadAsync<TextPayload>(item.PayloadRef, cancellationToken);
                data.SetText(textPayload.Text, System.Windows.TextDataFormat.UnicodeText);
                break;
            case ClipboardKind.RichText:
                var richPayload = await _payloadStore.ReadAsync<RichTextPayload>(item.PayloadRef, cancellationToken);
                if (mode == RestoreMode.Image && richPayload.PreviewPngBytes is { Length: > 0 } previewBytes)
                {
                    data.SetImage(CreateBitmap(previewBytes));
                    break;
                }

                if (mode == RestoreMode.Original)
                {
                    SetRawFormats(data, richPayload.RawFormats);

                    if (!string.IsNullOrWhiteSpace(richPayload.Html))
                    {
                        data.SetData(System.Windows.DataFormats.Html, richPayload.Html);
                    }

                    if (!string.IsNullOrWhiteSpace(richPayload.Rtf))
                    {
                        data.SetData(System.Windows.DataFormats.Rtf, richPayload.Rtf);
                    }

                    if (item.Formats.Contains(ClipboardFormatNames.Table) &&
                        LooksLikeTabularText(richPayload.PlainText))
                    {
                        data.SetData(System.Windows.DataFormats.CommaSeparatedValue, ToCsv(richPayload.PlainText));
                    }
                }

                data.SetText(richPayload.PlainText, System.Windows.TextDataFormat.UnicodeText);
                break;
            case ClipboardKind.Image:
                var imagePayload = await _payloadStore.ReadAsync<ImagePayload>(item.PayloadRef, cancellationToken);
                if (mode is RestoreMode.Original or RestoreMode.Image)
                {
                    data.SetImage(CreateBitmap(imagePayload.PngBytes));
                }
                else
                {
                    data.SetText(item.PreviewText, System.Windows.TextDataFormat.UnicodeText);
                }

                break;
            case ClipboardKind.FileList:
                var filePayload = await _payloadStore.ReadAsync<FileListPayload>(item.PayloadRef, cancellationToken);
                if (mode == RestoreMode.Original)
                {
                    data.SetFileDropList(CreateFileDropList(filePayload));
                }
                else
                {
                    data.SetText(string.Join(Environment.NewLine, filePayload.Items.Select(file => file.OriginalPath)), System.Windows.TextDataFormat.UnicodeText);
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(item.Kind));
        }

        System.Windows.Clipboard.SetDataObject(data, copy: true);
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

    private static void SetRawFormats(System.Windows.DataObject data, IReadOnlyList<ClipboardRawFormat>? rawFormats)
    {
        if (rawFormats is null || rawFormats.Count == 0)
        {
            return;
        }

        foreach (var format in rawFormats)
        {
            if (string.IsNullOrWhiteSpace(format.Format) || format.Data.Length == 0)
            {
                continue;
            }

            if (format.IsText)
            {
                data.SetData(format.Format, Encoding.UTF8.GetString(format.Data), autoConvert: false);
                continue;
            }

            data.SetData(format.Format, new MemoryStream(format.Data), autoConvert: false);
        }
    }

    private static bool LooksLikeTabularText(string text)
    {
        var lines = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return lines.Any(line => line.Contains('\t'));
    }

    private static string ToCsv(string tabSeparatedText)
    {
        var lines = tabSeparatedText
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
        return string.Join(Environment.NewLine, lines.Select(line =>
            string.Join(",", line.Split('\t').Select(EscapeCsv))));
    }

    private static string EscapeCsv(string value)
    {
        if (!value.Contains('"') && !value.Contains(',') && !value.Contains('\n') && !value.Contains('\r'))
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    private static StringCollection CreateFileDropList(FileListPayload payload)
    {
        var paths = new StringCollection();
        var restoredPaths = ResolveRestorablePaths(payload);
        foreach (var path in restoredPaths)
        {
            paths.Add(path);
        }

        return paths;
    }

    private static IEnumerable<string> ResolveRestorablePaths(FileListPayload payload)
    {
        if (!string.IsNullOrWhiteSpace(payload.SnapshotRef) && Directory.Exists(payload.SnapshotRef))
        {
            return Directory.EnumerateFileSystemEntries(payload.SnapshotRef);
        }

        return payload.Items
            .Select(item => item.OriginalPath)
            .Where(path => File.Exists(path) || Directory.Exists(path));
    }
}
