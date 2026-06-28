using SheraBoard.Core.Capture;

namespace SheraBoard.Tests;

public sealed class ClipboardWriteGuardTests
{
    [Fact]
    public void ShouldIgnoreCaptureOnlyInsideInternalWriteWindow()
    {
        var guard = new ClipboardWriteGuard();
        var now = new DateTimeOffset(2026, 5, 12, 20, 0, 0, TimeSpan.Zero);

        Assert.False(guard.ShouldIgnoreCapture(now));

        guard.MarkInternalWrite(now, TimeSpan.FromSeconds(2));

        Assert.True(guard.ShouldIgnoreCapture(now.AddMilliseconds(500)));
        Assert.True(guard.ShouldIgnoreCapture(now.AddSeconds(2)));
        Assert.False(guard.ShouldIgnoreCapture(now.AddSeconds(3)));
    }
}

