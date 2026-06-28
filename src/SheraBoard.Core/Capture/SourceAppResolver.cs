using System.IO;

namespace SheraBoard.Core.Capture;

public static class SourceAppResolver
{
    private static readonly HashSet<string> ShellOrContainerProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "ApplicationFrameHost.exe",
        "RuntimeBroker.exe",
        "SearchHost.exe",
        "ShellExperienceHost.exe",
        "StartMenuExperienceHost.exe",
        "TextInputHost.exe",
        "LockApp.exe",
        "dwm.exe",
        "Windows.exe",
        "SheraBoard.exe"
    };

    public static string? ChooseBestProcessName(string? clipboardOwnerProcess, string? foregroundProcess)
    {
        var owner = NormalizeProcessName(clipboardOwnerProcess);
        var foreground = NormalizeProcessName(foregroundProcess);

        if (IsUsableApplicationProcess(owner))
        {
            return owner;
        }

        if (IsUsableApplicationProcess(foreground))
        {
            return foreground;
        }

        return null;
    }

    public static string? NormalizeProcessName(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return null;
        }

        var trimmed = Path.GetFileName(processName.Trim());
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        return Path.HasExtension(trimmed) ? trimmed : $"{trimmed}.exe";
    }

    public static bool IsUsableApplicationProcess(string? processName)
    {
        var normalized = NormalizeProcessName(processName);
        return !string.IsNullOrWhiteSpace(normalized) &&
               !ShellOrContainerProcesses.Contains(normalized);
    }
}
