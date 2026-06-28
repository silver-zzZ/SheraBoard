using SheraBoard.App.Services;

namespace SheraBoard.Tests;

public sealed class StartupServiceTests
{
    [Fact]
    public void SetStartWithWindows_WritesQuotedExecutablePath()
    {
        var store = new InMemoryStartupEntryStore();
        var service = new StartupService("SheraBoard", () => @"F:\SheraBoard\SheraBoard.exe", store);

        service.SetStartWithWindows(true);

        Assert.Equal(@"""F:\SheraBoard\SheraBoard.exe""", store.GetValue("SheraBoard"));
    }

    [Fact]
    public void SetStartWithWindows_DisabledDeletesStartupEntry()
    {
        var store = new InMemoryStartupEntryStore();
        var service = new StartupService("SheraBoard", () => @"F:\SheraBoard\SheraBoard.exe", store);
        service.SetStartWithWindows(true);

        service.SetStartWithWindows(false);

        Assert.Null(store.GetValue("SheraBoard"));
    }

    [Fact]
    public void IsStartWithWindowsEnabled_ReturnsTrueForExistingRegisteredExecutable()
    {
        using var fixture = StartupFixture.Create();
        var store = new InMemoryStartupEntryStore();
        store.SetValue("SheraBoard", $@"""{fixture.ExecutablePath}""");
        var service = new StartupService("SheraBoard", () => fixture.ExecutablePath, store);

        Assert.True(service.IsStartWithWindowsEnabled());
        Assert.Equal(fixture.ExecutablePath, service.GetRegisteredExecutablePath());
    }

    [Fact]
    public void IsStartWithWindowsEnabled_ReturnsFalseForMissingRegisteredExecutable()
    {
        var store = new InMemoryStartupEntryStore();
        store.SetValue("SheraBoard", @"""F:\SheraBoard\Missing.exe""");
        var service = new StartupService("SheraBoard", () => @"F:\SheraBoard\SheraBoard.exe", store);

        Assert.False(service.IsStartWithWindowsEnabled());
        Assert.Equal(@"F:\SheraBoard\Missing.exe", service.GetRegisteredExecutablePath());
    }

    [Theory]
    [InlineData(@"""F:\SheraBoard\SheraBoard.exe""", @"F:\SheraBoard\SheraBoard.exe")]
    [InlineData(@"""F:\SheraBoard\SheraBoard.exe"" --minimized", @"F:\SheraBoard\SheraBoard.exe")]
    [InlineData(@"F:\SheraBoard\SheraBoard.exe", @"F:\SheraBoard\SheraBoard.exe")]
    [InlineData(@"F:\Shera Board\SheraBoard.exe --minimized", @"F:\Shera Board\SheraBoard.exe")]
    public void TryExtractExecutablePath_ParsesRegistryCommand(string command, string expected)
    {
        Assert.True(StartupService.TryExtractExecutablePath(command, out var executablePath));
        Assert.Equal(expected, executablePath);
    }

    private sealed class InMemoryStartupEntryStore : IStartupEntryStore
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

        public string? GetValue(string name)
        {
            return _values.TryGetValue(name, out var value) ? value : null;
        }

        public void SetValue(string name, string value)
        {
            _values[name] = value;
        }

        public void DeleteValue(string name)
        {
            _values.Remove(name);
        }
    }

    private sealed class StartupFixture : IDisposable
    {
        private StartupFixture(string root, string executablePath)
        {
            Root = root;
            ExecutablePath = executablePath;
        }

        public string Root { get; }

        public string ExecutablePath { get; }

        public static StartupFixture Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "SheraBoard.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var executablePath = Path.Combine(root, "SheraBoard.exe");
            File.WriteAllText(executablePath, string.Empty);

            return new StartupFixture(root, executablePath);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch
            {
                // Test cleanup should not mask the test result.
            }
        }
    }
}
