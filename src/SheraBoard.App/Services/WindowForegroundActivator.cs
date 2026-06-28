using System.Runtime.InteropServices;

namespace SheraBoard.App.Services;

internal static class WindowForegroundActivator
{
    private const int SW_RESTORE = 9;

    public static void Activate(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var currentThread = GetCurrentThreadId();
        var foregroundWindow = GetForegroundWindow();
        var foregroundThread = foregroundWindow == IntPtr.Zero
            ? 0
            : GetWindowThreadProcessId(foregroundWindow, out _);
        var targetThread = GetWindowThreadProcessId(hwnd, out _);

        var attachForeground = foregroundThread != 0 && foregroundThread != currentThread;
        var attachTarget = targetThread != 0 && targetThread != currentThread;

        try
        {
            if (attachForeground)
            {
                AttachThreadInput(currentThread, foregroundThread, true);
            }

            if (attachTarget)
            {
                AttachThreadInput(currentThread, targetThread, true);
            }

            ShowWindow(hwnd, SW_RESTORE);
            BringWindowToTop(hwnd);
            SetForegroundWindow(hwnd);
        }
        finally
        {
            if (attachTarget)
            {
                AttachThreadInput(currentThread, targetThread, false);
            }

            if (attachForeground)
            {
                AttachThreadInput(currentThread, foregroundThread, false);
            }
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
