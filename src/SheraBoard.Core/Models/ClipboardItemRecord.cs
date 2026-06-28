namespace SheraBoard.Core.Models;

public sealed record ClipboardItemRecord(
    Guid Id,
    DateTimeOffset CapturedAt,
    ClipboardKind Kind,
    string PreviewText,
    string? SourceApp,
    List<string> Formats,
    long SizeBytes,
    bool Pinned,
    bool Favorite,
    string PayloadRef,
    string ContentHash,
    string SemanticHash,
    int FormatScore);

