using System.Globalization;
using System.IO;
using System.Windows;
using SheraBoard.App.Services;
using Forms = System.Windows.Forms;

namespace SheraBoard.App;

public partial class SettingsWindow : Window
{
    private readonly AppServices _services;
    private string _selectedDataPath;

    public SettingsWindow(AppServices services)
    {
        _services = services;
        _selectedDataPath = services.Paths.RootDirectory;
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        var settings = _services.Settings;
        CapturePausedBox.IsChecked = settings.CapturePaused;
        StartWithWindowsBox.IsChecked = settings.StartWithWindows;
        CloseAfterCopyBox.IsChecked = settings.CloseWindowAfterCopy;
        HotkeyBox.Text = settings.GlobalHotkey;
        MaxStorageBox.Text = Math.Max(1, settings.MaxStorageBytes / 1024 / 1024).ToString(CultureInfo.InvariantCulture);
        DataPathBox.Text = _selectedDataPath;
        IgnoredProcessesBox.Text = string.Join(Environment.NewLine, settings.IgnoredProcesses);
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!long.TryParse(MaxStorageBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxStorageMb) ||
            maxStorageMb <= 0)
        {
            System.Windows.MessageBox.Show(this, "容量上限需要是正整数。", "SheraBoard", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var ignoredProcesses = IgnoredProcessesBox.Text
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var updated = _services.Settings with
        {
            CapturePaused = CapturePausedBox.IsChecked == true,
            StartWithWindows = StartWithWindowsBox.IsChecked == true,
            CloseWindowAfterCopy = CloseAfterCopyBox.IsChecked == true,
            GlobalHotkey = string.IsNullOrWhiteSpace(HotkeyBox.Text) ? "Ctrl+Alt+V" : HotkeyBox.Text.Trim(),
            MaxStorageBytes = maxStorageMb * 1024L * 1024L,
            IgnoredProcesses = ignoredProcesses
        };

        try
        {
            if (!string.Equals(
                    Path.TrimEndingDirectorySeparator(_services.Paths.RootDirectory),
                    Path.TrimEndingDirectorySeparator(Path.GetFullPath(_selectedDataPath)),
                    StringComparison.OrdinalIgnoreCase))
            {
                await _services.ChangeStorageRootAsync(_selectedDataPath);
            }

            await _services.UpdateSettingsAsync(updated);
            _selectedDataPath = _services.Paths.RootDirectory;
            DataPathBox.Text = _selectedDataPath;
            DialogResult = true;
            Close();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            System.Windows.MessageBox.Show(
                this,
                $"数据位置没有保存成功：{ex.Message}",
                "SheraBoard",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void ChangeDataPathButton_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "选择 SheraBoard 数据存储位置",
            SelectedPath = Directory.Exists(_selectedDataPath) ? _selectedDataPath : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() == Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
        {
            _selectedDataPath = dialog.SelectedPath;
            DataPathBox.Text = _selectedDataPath;
        }
    }

    private void SearchHelpButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new SearchHelpWindow
        {
            Owner = this
        };
        window.ShowDialog();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
