using SheraBoard.Core.Models;

namespace SheraBoard.Core.Capture;

public sealed record CaptureResult(
    bool Skipped,
    CaptureSkipReason? SkipReason,
    ClipboardItemRecord? Item)
{
    public static CaptureResult Captured(ClipboardItemRecord item)
    {
        return new CaptureResult(false, null, item);
    }

    public static CaptureResult Skip(CaptureSkipReason reason)
    {
        return new CaptureResult(true, reason, null);
    }
}

public enum CaptureSkipReason
{
    CapturePaused,
    IgnoredProcess,
    Empty,
    RecentDuplicate
}

