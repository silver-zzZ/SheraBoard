using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace SheraBoard.App.Services;

public sealed class ClipboardMonitor : IDisposable
{
    private const int WmClipboardUpdate = 0x031D;
    private const int WsPopup = unchecked((int)0x80000000);
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    private HwndSource? _source;
    private int _suppressedChanges;

    public event EventHandler? ClipboardChanged;

    public void Start()
    {
        if (_source is not null)
        {
            return;
        }

        var parameters = new HwndSourceParameters("SheraBoardClipboardMonitor")
        {
            Width = 1,
            Height = 1,
            PositionX = -32000,
            PositionY = -32000,
            WindowStyle = WsPopup,
            ExtendedWindowStyle = WsExToolWindow | WsExNoActivate
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
        AddClipboardFormatListener(_source.Handle);
    }

    public void SuppressNextChange()
    {
        SuppressNextChanges(1);
    }

    public void SuppressNextChanges(int count)
    {
        if (count <= 0)
        {
            return;
        }

        Interlocked.Add(ref _suppressedChanges, count);
    }

    public void Dispose()
    {
        if (_source is null)
        {
            return;
        }

        RemoveClipboardFormatListener(_source.Handle);
        _source.RemoveHook(WndProc);
        _source.Dispose();
        _source = null;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmClipboardUpdate)
        {
            return IntPtr.Zero;
        }

        if (Interlocked.CompareExchange(ref _suppressedChanges, 0, 0) > 0)
        {
            Interlocked.Decrement(ref _suppressedChanges);
            handled = true;
            return IntPtr.Zero;
        }

        ClipboardChanged?.Invoke(this, EventArgs.Empty);
        handled = false;
        return IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
}
