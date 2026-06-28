namespace SheraBoard.Core.Capture;

public sealed class ClipboardWriteGuard
{
    private DateTimeOffset _ignoreUntil = DateTimeOffset.MinValue;

    public void MarkInternalWrite(DateTimeOffset now, TimeSpan duration)
    {
        var nextIgnoreUntil = now.Add(duration);
        if (nextIgnoreUntil > _ignoreUntil)
        {
            _ignoreUntil = nextIgnoreUntil;
        }
    }

    public bool ShouldIgnoreCapture(DateTimeOffset now)
    {
        return now <= _ignoreUntil;
    }
}

