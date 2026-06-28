using System.Threading;
using System.Windows;
using System.Windows.Interop;
using SheraBoard.App.Services;

namespace SheraBoard.App;

public partial class App : System.Windows.Application
{
    private Mutex? _singleInstanceMutex;
    private AppServices? _services;
    private MainWindow? _mainWindow;
    private TrayIconService? _trayIcon;
    private int _capturePending;
    private readonly SemaphoreSlim _captureGate = new(1, 1);

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // v2 avoids a stale mutex left behind by older builds that could make a
        // fresh launch show "already running" without starting clipboard capture.
        _singleInstanceMutex = new Mutex(initiallyOwned: true, "SheraBoard.SingleInstance.v2", out var isFirstInstance);
        if (!isFirstInstance)
        {
            System.Windows.MessageBox.Show("SheraBoard 已在运行。", "SheraBoard", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        _services = await AppServices.CreateAsync();
        _services.StartupService.SetStartWithWindows(_services.Settings.StartWithWindows);

        _mainWindow = new MainWindow(_services);
        _trayIcon = new TrayIconService(_services.Settings);

        _services.ClipboardMonitor.ClipboardChanged += ClipboardMonitor_ClipboardChanged;
        _services.HotkeyService.HotkeyPressed += HotkeyService_HotkeyPressed;
        _services.SettingsChanged += Services_SettingsChanged;

        _trayIcon.ShowRequested += async (_, _) => await ShowMainWindowAsync();
        _trayIcon.SettingsRequested += (_, _) => ShowSettingsWindow();
        _trayIcon.TogglePauseRequested += async (_, _) => await TogglePauseAsync();
        _trayIcon.ExitRequested += (_, _) => ExitApplication();

        _services.ClipboardMonitor.Start();
        _services.HotkeyService.Register(_services.Settings.GlobalHotkey);

        _mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_services is not null)
        {
            await _services.Repository.DisposeAsync();
            _services.HotkeyService.Dispose();
            _services.ClipboardMonitor.Dispose();
        }

        _trayIcon?.Dispose();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }

    private async void ClipboardMonitor_ClipboardChanged(object? sender, EventArgs e)
    {
        await QueueClipboardCaptureAsync();
    }

    private async Task QueueClipboardCaptureAsync()
    {
        if (_services is null)
        {
            return;
        }

        Interlocked.Exchange(ref _capturePending, 1);

        while (true)
        {
            if (!await _captureGate.WaitAsync(0))
            {
                return;
            }

            try
            {
                while (Interlocked.Exchange(ref _capturePending, 0) == 1)
                {
                    await CaptureClipboardOnceAsync();
                }
            }
            finally
            {
                _captureGate.Release();
            }

            // A pending Clipboard event can arrive after the final pending check
            // but before the gate is released. In that race window its handler
            // returns because capture is still active, so this pass picks it up.
            if (Interlocked.CompareExchange(ref _capturePending, 0, 0) == 0)
            {
                return;
            }
        }
    }

    private async Task CaptureClipboardOnceAsync()
    {
        if (_services is null || _services.ClipboardWriteGuard.ShouldIgnoreCapture(DateTimeOffset.UtcNow))
        {
            return;
        }

        var services = _services;
        var sourceApp = services.ForegroundWindowService.GetClipboardSourceProcessName();
        var capture = await TryReadClipboardWithRetryAsync();
        if (capture is null)
        {
            return;
        }

        sourceApp ??= services.ForegroundWindowService.GetClipboardSourceProcessName();
        var capturedAt = DateTimeOffset.Now;
        var maxStorageBytes = services.Settings.MaxStorageBytes;
        var result = await Task.Run(async () =>
        {
            var captureResult = await services.Pipeline.CaptureAsync(capture, capturedAt, sourceApp);
            if (!captureResult.Skipped)
            {
                await services.RetentionService.EnforceCapacityAsync(maxStorageBytes);
            }

            return captureResult;
        });
        if (!result.Skipped)
        {
            if (_mainWindow is { IsVisible: true })
            {
                await _mainWindow.RefreshItemsAsync();
            }

        }
    }

    private async Task<SheraBoard.Core.Capture.ClipboardCapture?> TryReadClipboardWithRetryAsync()
    {
        if (_services is null)
        {
            return null;
        }

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var capture = _services.ClipboardReader.TryRead();
            if (capture is not null)
            {
                return capture;
            }

            await Task.Delay(50);
        }

        return null;
    }

    private async void HotkeyService_HotkeyPressed(object? sender, EventArgs e)
    {
        await ShowMainWindowAsync();
    }

    private async Task ShowMainWindowAsync()
    {
        if (_mainWindow is null)
        {
            return;
        }

        if (!_mainWindow.IsVisible)
        {
            _mainWindow.ResetTransientViewStateForFreshOpen();
        }

        BringMainWindowToFront();
        await _mainWindow.RefreshItemsAsync();
        BringMainWindowToFront();
    }

    private void BringMainWindowToFront()
    {
        if (_mainWindow is null)
        {
            return;
        }

        _mainWindow.Show();
        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.WindowState = WindowState.Normal;
        }

        var hwnd = new WindowInteropHelper(_mainWindow).Handle;
        WindowForegroundActivator.Activate(hwnd);
        _mainWindow.Focus();
    }

    private void ShowSettingsWindow()
    {
        if (_services is null)
        {
            return;
        }

        var window = new SettingsWindow(_services);
        if (_mainWindow is { IsVisible: true })
        {
            window.Owner = _mainWindow;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        window.ShowDialog();
    }

    private async Task TogglePauseAsync()
    {
        if (_services is null)
        {
            return;
        }

        await _services.UpdateSettingsAsync(_services.Settings with
        {
            CapturePaused = !_services.Settings.CapturePaused
        });
    }

    private void Services_SettingsChanged(object? sender, EventArgs e)
    {
        if (_services is not null)
        {
            _trayIcon?.Refresh(_services.Settings);
        }
    }

    private void ExitApplication()
    {
        if (_services is not null)
        {
            _services.MarkExiting();
        }

        _mainWindow?.Close();
        Shutdown();
    }
}
