using Microsoft.Win32;

namespace SheraBoard.App.Services;

public sealed class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private readonly string _applicationName;

    public StartupService(string applicationName)
    {
        _applicationName = applicationName;
    }

    public bool IsStartWithWindowsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(_applicationName) is string;
    }

    public void SetStartWithWindows(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

        if (enabled)
        {
            var executablePath = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrWhiteSpace(executablePath))
            {
                key.SetValue(_applicationName, $"\"{executablePath}\"");
            }
        }
        else
        {
            key.DeleteValue(_applicationName, throwOnMissingValue: false);
        }
    }
}

