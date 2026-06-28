using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace SheraBoard.App.Services;

public sealed class HotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const int WH_KEYBOARD_LL = 13;
    private const int VK_V = 0x56;
    private const int VK_CONTROL = 0x11;
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;
    private const int HotkeyId = 0x5342;
    private const uint KeyeventfKeyup = 0x0002;
    private const uint MapvkVkToVsc = 0;
    private const int WsPopup = unchecked((int)0x80000000);
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;

    private HwndSource? _source;
    private bool _registered;
    private IntPtr _windowsClipboardShortcutHook;
    private LowLevelKeyboardProc? _windowsClipboardShortcutProc;
    private bool _isWindowsClipboardShortcutDown;

    public event EventHandler? HotkeyPressed;

    public bool Register(string hotkey)
    {
        EnsureSource();
        Unregister();

        if (_source is null || !TryParseHotkey(hotkey, out var modifiers, out var key))
        {
            return false;
        }

        var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        _registered = RegisterHotKey(_source.Handle, HotkeyId, modifiers, virtualKey);
        return _registered;
    }

    public bool SetWindowsClipboardShortcutOverride(bool enabled)
    {
        return enabled ? StartWindowsClipboardShortcutHook() : StopWindowsClipboardShortcutHook();
    }

    public void Unregister()
    {
        if (_source is not null && _registered)
        {
            UnregisterHotKey(_source.Handle, HotkeyId);
            _registered = false;
        }
    }

    public void Dispose()
    {
        StopWindowsClipboardShortcutHook();
        Unregister();
        if (_source is not null)
        {
            _source.RemoveHook(WndProc);
            _source.Dispose();
            _source = null;
        }
    }

    private void EnsureSource()
    {
        if (_source is not null)
        {
            return;
        }

        var parameters = new HwndSourceParameters("SheraBoardHotkeyWindow")
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
    }

    private bool StartWindowsClipboardShortcutHook()
    {
        if (_windowsClipboardShortcutHook != IntPtr.Zero)
        {
            return true;
        }

        EnsureSource();
        _windowsClipboardShortcutProc = WindowsClipboardShortcutHookProc;
        _windowsClipboardShortcutHook = SetWindowsHookEx(WH_KEYBOARD_LL, _windowsClipboardShortcutProc, IntPtr.Zero, 0);
        return _windowsClipboardShortcutHook != IntPtr.Zero;
    }

    private bool StopWindowsClipboardShortcutHook()
    {
        if (_windowsClipboardShortcutHook == IntPtr.Zero)
        {
            return true;
        }

        var removed = UnhookWindowsHookEx(_windowsClipboardShortcutHook);
        if (removed)
        {
            _windowsClipboardShortcutHook = IntPtr.Zero;
            _windowsClipboardShortcutProc = null;
            ResetClipboardShortcutState();
        }

        return removed;
    }

    private IntPtr WindowsClipboardShortcutHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var message = wParam.ToInt32();
            var keyboard = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            var virtualKey = (int)keyboard.vkCode;
            var keyDown = message == WmKeyDown || message == WmSysKeyDown;
            var keyUp = message == WmKeyUp || message == WmSysKeyUp;

            if (keyUp &&
                IsWindowsKey(virtualKey) &&
                _isWindowsClipboardShortcutDown)
            {
                SendMenuMaskKey();
                _isWindowsClipboardShortcutDown = false;
            }

            if (keyDown &&
                virtualKey == VK_V &&
                IsWindowsKeyDown())
            {
                if (!_isWindowsClipboardShortcutDown)
                {
                    _isWindowsClipboardShortcutDown = true;
                    RaiseHotkeyPressed();
                }

                return new IntPtr(1);
            }

            if (keyUp &&
                virtualKey == VK_V &&
                _isWindowsClipboardShortcutDown)
            {
                _isWindowsClipboardShortcutDown = false;
            }
        }

        return CallNextHookEx(_windowsClipboardShortcutHook, nCode, wParam, lParam);
    }

    private void ResetClipboardShortcutState()
    {
        _isWindowsClipboardShortcutDown = false;
    }

    private void RaiseHotkeyPressed()
    {
        if (_source?.Dispatcher is { } dispatcher)
        {
            dispatcher.BeginInvoke(new Action(() => HotkeyPressed?.Invoke(this, EventArgs.Empty)));
            return;
        }

        HotkeyPressed?.Invoke(this, EventArgs.Empty);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            RaiseHotkeyPressed();
            handled = true;
        }

        return IntPtr.Zero;
    }

    private static bool IsWindowsKeyDown()
    {
        return IsKeyDown(VK_LWIN) || IsKeyDown(VK_RWIN);
    }

    private static bool IsWindowsKey(int virtualKey)
    {
        return virtualKey == VK_LWIN || virtualKey == VK_RWIN;
    }

    private static void SendMenuMaskKey()
    {
        // Same idea as AutoHotkey's MenuMaskKey: when a Win-key hook hotkey
        // suppresses the final key down, Windows may otherwise treat the next
        // Win key-up as a solo Start-menu press. Inject a harmless Ctrl tap
        // immediately before the real Win key-up is allowed through.
        //
        // AutoHotkey's hook path uses keybd_event here; queued input APIs can
        // arrive after the pending WinUp in this exact hook timing, which is too
        // late to stop the Start menu.
        var scanCode = (byte)MapVirtualKey(VK_CONTROL, MapvkVkToVsc);
        keybd_event(VK_CONTROL, scanCode, 0, UIntPtr.Zero);
        keybd_event(VK_CONTROL, scanCode, KeyeventfKeyup, UIntPtr.Zero);
    }

    private static bool IsKeyDown(int virtualKey)
    {
        return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
    }

    private static bool TryParseHotkey(string hotkey, out uint modifiers, out Key key)
    {
        modifiers = 0;
        key = Key.None;

        foreach (var part in hotkey.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            switch (part.ToUpperInvariant())
            {
                case "CTRL":
                case "CONTROL":
                    modifiers |= ModControl;
                    break;
                case "ALT":
                    modifiers |= ModAlt;
                    break;
                case "SHIFT":
                    modifiers |= ModShift;
                    break;
                case "WIN":
                case "WINDOWS":
                    modifiers |= ModWin;
                    break;
                default:
                    if (Enum.TryParse(part, ignoreCase: true, out Key parsedKey))
                    {
                        key = parsedKey;
                    }
                    else if (part.Length == 1 && char.IsLetterOrDigit(part[0]))
                    {
                        key = Enum.Parse<Key>(part.ToUpperInvariant());
                    }
                    break;
            }
        }

        return modifiers != 0 && key != Key.None;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hmod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }
}
