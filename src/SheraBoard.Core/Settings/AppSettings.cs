namespace SheraBoard.Core.Settings;

public sealed record AppSettings
{
    public bool CapturePaused { get; init; }

    public string GlobalHotkey { get; init; } = "Ctrl+Alt+V";

    public bool CloseWindowAfterCopy { get; init; }

    public bool StartWithWindows { get; init; } = true;

    public long MaxStorageBytes { get; init; } = 512L * 1024L * 1024L;

    public int DeduplicateWindowSeconds { get; init; } = 5;

    public int RichContentDeduplicateWindowSeconds { get; init; } = 30;

    public int MaxPreviewChars { get; init; } = 240;

    public List<string> IgnoredProcesses { get; init; } = [];

    public static AppSettings Default => new();
}
