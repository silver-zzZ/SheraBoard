namespace SheraBoard.Tests;

public sealed class WindowsClipboardShortcutModeTests
{
    [Fact]
    public void WinVOverrideIsAlwaysOnAndNotASettingsCheckbox()
    {
        var settingsSource = File.ReadAllText(FindProjectFile("src", "SheraBoard.Core", "Settings", "AppSettings.cs"));
        var settingsXaml = File.ReadAllText(FindProjectFile("src", "SheraBoard.App", "SettingsWindow.xaml"));
        var settingsCode = File.ReadAllText(FindProjectFile("src", "SheraBoard.App", "SettingsWindow.xaml.cs"));
        var appServicesSource = File.ReadAllText(FindProjectFile("src", "SheraBoard.App", "Services", "AppServices.cs"));
        var mainWindowXaml = File.ReadAllText(FindProjectFile("src", "SheraBoard.App", "MainWindow.xaml"));

        Assert.DoesNotContain("OverrideWindowsClipboardShortcut", settingsSource);
        Assert.DoesNotContain("WinVOverrideBox", settingsXaml);
        Assert.DoesNotContain("接管 Win+V", settingsXaml);
        Assert.Contains("备用快捷键", settingsXaml);
        Assert.Contains("Win+V", mainWindowXaml);
        Assert.DoesNotContain("WinVOverrideBox", settingsCode);
        Assert.Contains("SetWindowsClipboardShortcutOverride(true)", appServicesSource);
    }

    [Fact]
    public void HotkeyServiceSuppressesWinVWithoutBlockingSoloWindowsKey()
    {
        var source = File.ReadAllText(FindProjectFile("src", "SheraBoard.App", "Services", "HotkeyService.cs"));

        Assert.Contains("WH_KEYBOARD_LL", source);
        Assert.Contains("SetWindowsClipboardShortcutOverride", source);
        Assert.Contains("SetWindowsHookEx", source);
        Assert.Contains("IsWindowsKeyDown()", source);
        Assert.Contains("VK_V", source);
        Assert.Contains("_isWindowsClipboardShortcutDown", source);
        Assert.DoesNotContain("_discardNextVUpAfterClipboardShortcut", source);
        Assert.DoesNotContain("SendInput", source);
        Assert.Contains("keybd_event", source);
        Assert.DoesNotContain("MarkWindowsKeyAsUsedForShortcut", source);
        Assert.Contains("VK_CONTROL", source);
        Assert.DoesNotContain("VK_F24", source);
        Assert.DoesNotContain("SendSyntheticWindowsKeyUp", source);
        Assert.DoesNotContain("KeyeventfExtendedKey", source);
        Assert.DoesNotContain("_deferredWindowsKeyUpVirtualKey", source);
        Assert.DoesNotContain("_pendingWindowsKey", source);
        Assert.DoesNotContain("_suppressWindowsKeyUpAfterClipboardShortcut", source);
        Assert.DoesNotContain("SendStartMenuShortcut", source);
        Assert.DoesNotContain("VK_ESCAPE", source);
        Assert.DoesNotContain("_suppressNextWinKeyUp", source);
    }

    [Fact]
    public void WinVHookLetsSoloWindowsKeyPassThroughAndMasksStartMenu()
    {
        var source = File.ReadAllText(FindProjectFile("src", "SheraBoard.App", "Services", "HotkeyService.cs"));

        Assert.DoesNotContain("if (keyDown && IsWindowsKey(virtualKey))", source);
        Assert.Contains("keyDown &&", source);
        Assert.Contains("virtualKey == VK_V", source);
        Assert.Contains("IsWindowsKeyDown()", source);
        Assert.Contains("_isWindowsClipboardShortcutDown = true", source);
        Assert.Contains("_isWindowsClipboardShortcutDown = false", source);
        Assert.Contains("return new IntPtr(1);", source);
        Assert.DoesNotContain("virtualKey == VK_V &&\r\n                _isWindowsClipboardShortcutDown)\r\n            {\r\n                _isWindowsClipboardShortcutDown = false;\r\n                return new IntPtr(1);", source);
        Assert.DoesNotContain("keyUp &&\r\n                virtualKey == VK_V &&\r\n                _isWindowsClipboardShortcutDown)\r\n            {\r\n                _isWindowsClipboardShortcutDown = false;\r\n                return new IntPtr(1);", source);
        Assert.DoesNotContain("SendSyntheticWindowsKeyUp(virtualKey);", source);
        Assert.Contains("return CallNextHookEx(_windowsClipboardShortcutHook, nCode, wParam, lParam);", source);
    }

    [Fact]
    public void WinVReleaseOrderIsMaskedBeforeRealWindowsKeyUpPassesThrough()
    {
        var source = File.ReadAllText(FindProjectFile("src", "SheraBoard.App", "Services", "HotkeyService.cs"));

        Assert.Contains("IsWindowsKey(virtualKey)", source);
        Assert.Contains("SendMenuMaskKey();", source);
        Assert.Contains("keybd_event(VK_CONTROL, scanCode, 0, UIntPtr.Zero);", source);
        Assert.Contains("keybd_event(VK_CONTROL, scanCode, KeyeventfKeyup, UIntPtr.Zero);", source);
        Assert.Contains("MapVirtualKey(VK_CONTROL, MapvkVkToVsc)", source);
        Assert.DoesNotContain("return new IntPtr(1);\r\n            }\r\n\r\n            if (keyDown", source);
        Assert.DoesNotContain("SendDeferredWindowsClipboardShortcutRelease", source);
        Assert.DoesNotContain("SendKeyUp(VK_V", source);
        Assert.DoesNotContain("CreateKeyboardInput", source);

        var methodIndex = source.IndexOf("IsWindowsKey(virtualKey)", StringComparison.Ordinal);
        Assert.True(methodIndex >= 0);
        var methodEnd = source.IndexOf("if (keyDown &&", methodIndex, StringComparison.Ordinal);
        Assert.True(methodEnd > methodIndex);
        var methodSource = source[methodIndex..methodEnd];
        Assert.Contains("SendMenuMaskKey();", methodSource);
        Assert.Contains("_isWindowsClipboardShortcutDown = false;", methodSource);
        Assert.DoesNotContain("return new IntPtr(1);", methodSource);
    }

    [Fact]
    public void CopyAfterCloseModeHidesImmediatelyWithoutWaitingForToast()
    {
        var mainWindowSource = File.ReadAllText(FindProjectFile("src", "SheraBoard.App", "MainWindow.xaml.cs"));

        Assert.Contains("HideAfterCopyIfConfigured();", mainWindowSource);
        Assert.Contains("if (_services.Settings.CloseWindowAfterCopy)", mainWindowSource);
        Assert.DoesNotContain("await Task.Delay(80)", mainWindowSource);
        Assert.DoesNotContain("await ShowToastAsync($\"已复制 · {modeText}\");\r\n        HideAfterCopyIfConfigured();", mainWindowSource);
    }

    [Fact]
    public void RepeatedWinVBringsExistingMainWindowToFront()
    {
        var appSource = File.ReadAllText(FindProjectFile("src", "SheraBoard.App", "App.xaml.cs"));
        var foregroundSource = File.ReadAllText(FindProjectFile("src", "SheraBoard.App", "Services", "WindowForegroundActivator.cs"));

        Assert.Contains("BringMainWindowToFront", appSource);
        Assert.Contains("WindowForegroundActivator.Activate", appSource);
        Assert.DoesNotContain("Topmost = true", appSource);
        Assert.DoesNotContain("_mainWindow.Activate()", appSource);
        Assert.Contains("SetForegroundWindow", foregroundSource);
        Assert.Contains("AttachThreadInput", foregroundSource);
        Assert.Contains("BringWindowToTop", foregroundSource);

        var methodIndex = appSource.IndexOf("private async Task ShowMainWindowAsync()", StringComparison.Ordinal);
        var methodSource = appSource[methodIndex..appSource.IndexOf("private void BringMainWindowToFront()", StringComparison.Ordinal)];
        var bringIndex = methodSource.IndexOf("BringMainWindowToFront();", StringComparison.Ordinal);
        var refreshIndex = methodSource.IndexOf("await _mainWindow.RefreshItemsAsync();", StringComparison.Ordinal);
        Assert.True(bringIndex >= 0);
        Assert.True(refreshIndex > bringIndex);
    }

    [Fact]
    public void ReopeningHiddenMainWindowResetsTransientSearchAndFilters()
    {
        var appSource = File.ReadAllText(FindProjectFile("src", "SheraBoard.App", "App.xaml.cs"));
        var mainWindowSource = File.ReadAllText(FindProjectFile("src", "SheraBoard.App", "MainWindow.xaml.cs"));

        Assert.Contains("if (!_mainWindow.IsVisible)", appSource);
        Assert.Contains("ResetTransientViewStateForFreshOpen();", appSource);
        Assert.Contains("public void ResetTransientViewStateForFreshOpen()", mainWindowSource);
        Assert.Contains("SearchBox.Clear();", mainWindowSource);
        Assert.Contains("_selectedSourceApp = null;", mainWindowSource);
        Assert.Contains("_dateFilterMode = DateFilterMode.Today;", mainWindowSource);
        Assert.Contains("CheckRadioButtonByTag(KindFiltersPanel, \"All\")", mainWindowSource);
        Assert.Contains("CheckRadioButtonByTag(dateFiltersPanel, \"Today\")", mainWindowSource);
        Assert.Contains("_scrollToTopAfterNextRefresh = true;", mainWindowSource);
        Assert.Contains("_isResettingTransientState", mainWindowSource);
    }

    [Fact]
    public void HotkeyAndTrayOpenMainWindowInsteadOfQuickPanel()
    {
        var appSource = File.ReadAllText(FindProjectFile("src", "SheraBoard.App", "App.xaml.cs"));
        var traySource = File.ReadAllText(FindProjectFile("src", "SheraBoard.App", "Services", "TrayIconService.cs"));
        var appDirectory = Path.GetDirectoryName(FindProjectFile("src", "SheraBoard.App", "App.xaml.cs"))!;

        Assert.Contains("ShowMainWindowAsync", appSource);
        Assert.DoesNotContain("QuickPanel", appSource);
        Assert.DoesNotContain("QuickPanel", traySource);
        Assert.False(File.Exists(Path.Combine(appDirectory, "QuickPanel.xaml")));
        Assert.False(File.Exists(Path.Combine(appDirectory, "QuickPanel.xaml.cs")));
    }

    [Fact]
    public void CopyingCanAutoHideWindowWhenCloseAfterCopyIsEnabled()
    {
        var settingsSource = File.ReadAllText(FindProjectFile("src", "SheraBoard.Core", "Settings", "AppSettings.cs"));
        var settingsXaml = File.ReadAllText(FindProjectFile("src", "SheraBoard.App", "SettingsWindow.xaml"));
        var settingsCode = File.ReadAllText(FindProjectFile("src", "SheraBoard.App", "SettingsWindow.xaml.cs"));
        var mainWindowSource = File.ReadAllText(FindProjectFile("src", "SheraBoard.App", "MainWindow.xaml.cs"));

        Assert.Contains("CloseWindowAfterCopy", settingsSource);
        Assert.Contains("CloseAfterCopyBox", settingsXaml);
        Assert.Contains("复制后隐藏窗口", settingsXaml);
        Assert.Contains("CloseWindowAfterCopy = CloseAfterCopyBox.IsChecked == true", settingsCode);
        Assert.Contains("CloseWindowAfterCopy", mainWindowSource);
        Assert.Contains("HideAfterCopyIfConfigured", mainWindowSource);
    }

    private static string FindProjectFile(params string[] relativeParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate {Path.Combine(relativeParts)} from {AppContext.BaseDirectory}.");
    }
}
