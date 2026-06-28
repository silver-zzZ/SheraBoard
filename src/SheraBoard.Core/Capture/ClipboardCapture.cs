using SheraBoard.Core.Models;

namespace SheraBoard.Core.Capture;

public sealed record ClipboardCapture(
    string? PlainText,
    string? Html,
    string? Rtf,
    byte[]? ImagePngBytes,
    int? ImageWidth,
    int? ImageHeight,
    IReadOnlyList<string> FilePaths)
{
    public IReadOnlyList<ClipboardRawFormat> RawFormats { get; init; } = [];

    public static ClipboardCapture FromText(string text)
    {
        return new ClipboardCapture(text, null, null, null, null, null, []);
    }

    public static ClipboardCapture FromFiles(IReadOnlyList<string> filePaths)
    {
        return new ClipboardCapture(null, null, null, null, null, null, filePaths);
    }

    public static ClipboardCapture FromImage(byte[] pngBytes, int? width = null, int? height = null)
    {
        return new ClipboardCapture(null, null, null, pngBytes, width, height, []);
    }
}
