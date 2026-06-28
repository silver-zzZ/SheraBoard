namespace SheraBoard.Core.Models;

public sealed record TextPayload(string Text);

public sealed record RichTextPayload(
    string PlainText,
    string? Html,
    string? Rtf,
    byte[]? PreviewPngBytes = null,
    int? PreviewWidth = null,
    int? PreviewHeight = null,
    IReadOnlyList<ClipboardRawFormat>? RawFormats = null);

public sealed record ClipboardRawFormat(string Format, byte[] Data, bool IsText);

public sealed record ImagePayload(byte[] PngBytes, int? Width, int? Height);

public sealed record FileListPayload(List<FileListEntry> Items, string? SnapshotRef);

public sealed record FileListEntry(
    string OriginalPath,
    string Name,
    bool Exists,
    bool IsDirectory,
    long? SizeBytes,
    DateTimeOffset? LastWriteTime);
