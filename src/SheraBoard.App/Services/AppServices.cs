using System.IO;
using SheraBoard.Core.Capture;
using SheraBoard.Core.Persistence;
using SheraBoard.Core.Security;
using SheraBoard.Core.Settings;

namespace SheraBoard.App.Services;

public sealed class AppServices
{
    public AppServices(
        StorageLocationStore storageLocationStore,
        SheraBoardPaths paths,
        JsonSettingsStore settingsStore,
        StorageRepository repository,
        FilePayloadStore payloadStore,
        ClipboardMonitor clipboardMonitor,
        HotkeyService hotkeyService,
        ClipboardReader clipboardReader,
        ForegroundWindowService foregroundWindowService,
        RestoreService restoreService,
        StartupService startupService,
        AppSettings settings)
    {
        StorageLocationStore = storageLocationStore;
        Paths = paths;
        SettingsStore = settingsStore;
        Repository = repository;
        PayloadStore = payloadStore;
        ClipboardMonitor = clipboardMonitor;
        HotkeyService = hotkeyService;
        ClipboardReader = clipboardReader;
        ForegroundWindowService = foregroundWindowService;
        RestoreService = restoreService;
        StartupService = startupService;
        Settings = settings;
        Pipeline = new CapturePipeline(repository, payloadStore, settings);
        ClipboardWriteGuard = new ClipboardWriteGuard();
        FileSnapshotService = new FileSnapshotService(paths, repository, payloadStore);
        RetentionService = new RetentionService(repository, payloadStore);
        HotkeyService.SetWindowsClipboardShortcutOverride(true);
    }

    public StorageLocationStore StorageLocationStore { get; }

    public SheraBoardPaths Paths { get; private set; }

    public JsonSettingsStore SettingsStore { get; private set; }

    public StorageRepository Repository { get; private set; }

    public FilePayloadStore PayloadStore { get; private set; }

    public ClipboardMonitor ClipboardMonitor { get; }

    public HotkeyService HotkeyService { get; }

    public ClipboardReader ClipboardReader { get; }

    public ForegroundWindowService ForegroundWindowService { get; }

    public RestoreService RestoreService { get; private set; }

    public StartupService StartupService { get; }

    public FileSnapshotService FileSnapshotService { get; private set; }

    public RetentionService RetentionService { get; private set; }

    public AppSettings Settings { get; private set; }

    public CapturePipeline Pipeline { get; private set; }

    public ClipboardWriteGuard ClipboardWriteGuard { get; }

    public bool IsExiting { get; private set; }

    public event EventHandler? SettingsChanged;

    public async Task UpdateSettingsAsync(AppSettings settings)
    {
        Settings = settings;
        Pipeline = new CapturePipeline(Repository, PayloadStore, settings);
        await SettingsStore.SaveAsync(settings);
        StartupService.SetStartWithWindows(settings.StartWithWindows);
        HotkeyService.Register(settings.GlobalHotkey);
        HotkeyService.SetWindowsClipboardShortcutOverride(true);
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task ChangeStorageRootAsync(string newRootDirectory)
    {
        var normalizedNewRoot = Path.GetFullPath(newRootDirectory);
        if (string.Equals(
                Path.TrimEndingDirectorySeparator(Paths.RootDirectory),
                Path.TrimEndingDirectorySeparator(normalizedNewRoot),
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await SettingsStore.SaveAsync(Settings);
        await Repository.DisposeAsync();

        var newPaths = await StorageMigration.CopyToRootAsync(Paths, normalizedNewRoot);
        await StorageLocationStore.SaveRootDirectoryAsync(newPaths.RootDirectory);
        var persistedRoot = Path.TrimEndingDirectorySeparator(StorageLocationStore.ResolveRootDirectory());
        if (!string.Equals(persistedRoot, Path.TrimEndingDirectorySeparator(newPaths.RootDirectory), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Storage location could not be saved for the next launch.");
        }

        Paths = newPaths;
        Paths.EnsureDirectories();
        SettingsStore = new JsonSettingsStore(Paths.SettingsPath);
        await SettingsStore.SaveAsync(Settings);
        Repository = new StorageRepository(Paths.DatabasePath);
        await Repository.InitializeAsync();
        PayloadStore = new FilePayloadStore(Paths, new DpapiDataProtector("SheraBoard"));
        RestoreService = new RestoreService(PayloadStore);
        FileSnapshotService = new FileSnapshotService(Paths, Repository, PayloadStore);
        RetentionService = new RetentionService(Repository, PayloadStore);
        Pipeline = new CapturePipeline(Repository, PayloadStore, Settings);
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void MarkExiting()
    {
        IsExiting = true;
    }

    public void PrepareForInternalClipboardWrite()
    {
        ClipboardMonitor.SuppressNextChange();
        ClipboardWriteGuard.MarkInternalWrite(DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(500));
    }

    public static async Task<AppServices> CreateAsync()
    {
        var storageLocationStore = StorageLocationStore.ForCurrentUser();
        var paths = storageLocationStore.ResolvePaths();
        paths.EnsureDirectories();

        var settingsStore = new JsonSettingsStore(paths.SettingsPath);
        var settings = await settingsStore.LoadAsync();
        var repository = new StorageRepository(paths.DatabasePath);
        await repository.InitializeAsync();
        var payloadStore = new FilePayloadStore(paths, new DpapiDataProtector("SheraBoard"));
        var clipboardMonitor = new ClipboardMonitor();
        var hotkeyService = new HotkeyService();
        var clipboardReader = new ClipboardReader();
        var foregroundWindowService = new ForegroundWindowService();
        var restoreService = new RestoreService(payloadStore);
        var startupService = new StartupService("SheraBoard");
        startupService.TrySetStartWithWindows(settings.StartWithWindows);

        return new AppServices(
            storageLocationStore,
            paths,
            settingsStore,
            repository,
            payloadStore,
            clipboardMonitor,
            hotkeyService,
            clipboardReader,
            foregroundWindowService,
            restoreService,
            startupService,
            settings);
    }
}
