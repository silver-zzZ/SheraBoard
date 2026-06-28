using SheraBoard.Core.Settings;

namespace SheraBoard.Tests;

public sealed class SettingsStoreTests
{
    [Fact]
    public async Task JsonSettingsStoreLoadsDefaultsAndPersistsUserChanges()
    {
        using var fixture = await CoreFixture.CreateAsync();
        var store = new JsonSettingsStore(fixture.Paths.SettingsPath);

        var defaults = await store.LoadAsync();
        Assert.False(defaults.CapturePaused);
        Assert.Equal("Ctrl+Alt+V", defaults.GlobalHotkey);
        Assert.True(defaults.MaxStorageBytes > 0);

        var updated = defaults with
        {
            CapturePaused = true,
            GlobalHotkey = "Ctrl+Shift+V",
            IgnoredProcesses = ["1password.exe", "keepassxc.exe"]
        };

        await store.SaveAsync(updated);
        var reloaded = await store.LoadAsync();

        Assert.True(reloaded.CapturePaused);
        Assert.Equal("Ctrl+Shift+V", reloaded.GlobalHotkey);
        Assert.Equal(["1password.exe", "keepassxc.exe"], reloaded.IgnoredProcesses);
    }
}

