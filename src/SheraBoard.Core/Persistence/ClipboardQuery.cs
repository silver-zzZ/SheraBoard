using SheraBoard.Core.Models;

namespace SheraBoard.Core.Persistence;

public enum ClipboardContentFeature
{
    Url,
    Code
}

public sealed record ClipboardQuery(
    string? SearchText = null,
    ClipboardKind? Kind = null,
    DateOnly? Date = null,
    int Limit = 500,
    int Offset = 0,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    IReadOnlyList<string>? SearchTerms = null,
    string? SourceApp = null,
    bool PinnedOnly = false,
    IReadOnlyList<ClipboardContentFeature>? Features = null);

public sealed record SourceAppSummary(
    string SourceApp,
    int ItemCount,
    DateTimeOffset LastCapturedAt);
