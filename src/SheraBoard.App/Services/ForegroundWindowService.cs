using System.Diagnostics;
using System.Runtime.InteropServices;
using SheraBoard.Core.Capture;

namespace SheraBoard.App.Services;

public sealed class ForegroundWindowService
{
    public string? GetClipboardSourceProcessName()
    {
        var clipboardOwner = GetProcessNameFromWindow(GetClipboardOwner());
        var foreground = GetForegroundProcessName();
        return SourceAppResolver.ChooseBestProcessName(clipboardOwner, foreground);
    }

    public string? GetForegroundProcessName()
    {
        return GetProcessNameFromWindow(GetForegroundWindow());
    }

    private static string? GetProcessNameFromWindow(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        _ = GetWindowThreadProcessId(handle, out var processId);
        if (processId == 0)
        {
            return null;
        }

        try
        {
            using var process = Process.GetProcessById((int)processId);
            return $"{process.ProcessName}.exe";
        }
        catch
        {
            return null;
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetClipboardOwner();

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
}
