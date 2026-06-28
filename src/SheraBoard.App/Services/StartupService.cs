using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace SheraBoard.App.Services;

public sealed class StartupService
{
    private readonly string _applicationName;
    private readonly Func<string?> _executablePathProvider;
    private readonly IStartupEntryStore _store;

    public StartupService(string applicationName)
        : this(
            applicationName,
            () => Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName,
            new RegistryStartupEntryStore())
    {
    }

    internal StartupService(
        string applicationName,
        Func<string?> executablePathProvider,
        IStartupEntryStore store)
    {
        _applicationName = applicationName;
        _executablePathProvider = executablePathProvider;
        _store = store;
    }

    public bool IsStartWithWindowsEnabled()
    {
        return TryGetRegisteredExecutablePath(out var executablePath) && File.Exists(executablePath);
    }

    public string? GetRegisteredExecutablePath()
    {
        return TryGetRegisteredExecutablePath(out var executablePath) ? executablePath : null;
    }

    public void SetStartWithWindows(bool enabled)
    {
        if (enabled)
        {
            var executablePath = _executablePathProvider();
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                throw new InvalidOperationException("The application executable path could not be resolved.");
            }

            _store.SetValue(_applicationName, QuoteExecutablePath(executablePath));
        }
        else
        {
            _store.DeleteValue(_applicationName);
        }
    }

    private bool TryGetRegisteredExecutablePath(out string executablePath)
    {
        return TryExtractExecutablePath(_store.GetValue(_applicationName), out executablePath);
    }

    internal static bool TryExtractExecutablePath(string? command, out string executablePath)
    {
        executablePath = string.Empty;
        if (string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        var trimmed = command.Trim();
        if (trimmed.StartsWith('"'))
        {
            var endQuote = trimmed.IndexOf('"', 1);
            if (endQuote <= 1)
            {
                return false;
            }

            executablePath = trimmed[1..endQuote];
            return !string.IsNullOrWhiteSpace(executablePath);
        }

        var exeIndex = trimmed.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exeIndex >= 0)
        {
            executablePath = trimmed[..(exeIndex + 4)].Trim();
            return !string.IsNullOrWhiteSpace(executablePath);
        }

        var firstSpace = trimmed.IndexOf(' ');
        executablePath = firstSpace >= 0 ? trimmed[..firstSpace] : trimmed;
        return !string.IsNullOrWhiteSpace(executablePath);
    }

    private static string QuoteExecutablePath(string executablePath)
    {
        return $"\"{executablePath}\"";
    }
}

internal interface IStartupEntryStore
{
    string? GetValue(string name);

    void SetValue(string name, string value);

    void DeleteValue(string name);
}

internal sealed class RegistryStartupEntryStore : IStartupEntryStore
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public string? GetValue(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(name) as string;
    }

    public void SetValue(string name, string value)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        key.SetValue(name, value, RegistryValueKind.String);
    }

    public void DeleteValue(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        key.DeleteValue(name, throwOnMissingValue: false);
    }
}
