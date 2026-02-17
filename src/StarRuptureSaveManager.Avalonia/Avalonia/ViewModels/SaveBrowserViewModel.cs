using StarRuptureSaveFixer.Fixers;
using StarRuptureSaveFixer.Models;
using StarRuptureSaveFixer.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Input;

namespace StarRuptureSaveFixer.AvaloniaApp.ViewModels;

public sealed class SaveBrowserViewModel : ViewModelBase
{
    private readonly SessionManager _sessionManager = new();
    private readonly SaveFileService _saveFileService = new();

    private SaveSession? _selectedSession;
    private SaveFileInfo? _selectedSaveFile;
    private string _logOutput = "";
    private bool _isProcessing;
    private int _selectedFixerIndex;

    public SaveBrowserViewModel()
    {
        Sessions = new ObservableCollection<SaveSession>();
        Fixers = new ObservableCollection<FixerOption>
        {
            new FixerOption("Fix Drones (Remove invalid targets)", () => new DroneFixer()),
            new FixerOption("Remove All Drones", () => new DroneRemover())
        };

        RefreshCommand = new RelayCommand(Refresh);
        FixSelectedCommand = new AsyncRelayCommand(FixSelectedAsync, () => CanFixSelected);
        OpenSessionFolderCommand = new RelayCommand(OpenSessionFolder, () => SelectedSession != null);

        Refresh();
        Log("Avalonia Save Browser initialized.");
    }

    public void UpdateCustomPath(string? customPath)
    {
        _sessionManager.CustomSavePath = customPath;
    }

    public ObservableCollection<SaveSession> Sessions { get; }
    public ObservableCollection<FixerOption> Fixers { get; }

    public SaveSession? SelectedSession
    {
        get => _selectedSession;
        set
        {
            if (SetProperty(ref _selectedSession, value))
            {
                SelectedSaveFile = null;
                OnPropertyChanged(nameof(SaveFilesInSession));
                ((RelayCommand)OpenSessionFolderCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public SaveFileInfo? SelectedSaveFile
    {
        get => _selectedSaveFile;
        set
        {
            if (SetProperty(ref _selectedSaveFile, value))
                ((AsyncRelayCommand)FixSelectedCommand).RaiseCanExecuteChanged();
        }
    }

    public IEnumerable<SaveFileInfo> SaveFilesInSession =>
        SelectedSession?.SaveFiles ?? Enumerable.Empty<SaveFileInfo>();

    public int SelectedFixerIndex
    {
        get => _selectedFixerIndex;
        set => SetProperty(ref _selectedFixerIndex, value);
    }

    public string LogOutput
    {
        get => _logOutput;
        private set => SetProperty(ref _logOutput, value);
    }

    public bool IsProcessing
    {
        get => _isProcessing;
        private set
        {
            if (SetProperty(ref _isProcessing, value))
                ((AsyncRelayCommand)FixSelectedCommand).RaiseCanExecuteChanged();
        }
    }

    public bool CanFixSelected => !IsProcessing && SelectedSaveFile != null && !SelectedSaveFile.IsBackup;

    public ICommand RefreshCommand { get; }
    public ICommand FixSelectedCommand { get; }
    public ICommand OpenSessionFolderCommand { get; }

    public void Refresh()
    {
        var previousSelection = SelectedSession?.FullPath;
        Sessions.Clear();

        foreach (var session in _sessionManager.GetAllSessions())
            Sessions.Add(session);

        if (!string.IsNullOrWhiteSpace(previousSelection))
            SelectedSession = Sessions.FirstOrDefault(s => s.FullPath == previousSelection);

        if (Sessions.Count == 0)
            Log("No save sessions found. Set a custom path later in Settings port.");
        else
            Log($"Found {Sessions.Count} session(s).");
    }

    private async Task FixSelectedAsync()
    {
        if (SelectedSaveFile == null)
            return;

        if (SelectedFixerIndex < 0 || SelectedFixerIndex >= Fixers.Count)
            return;

        var filePath = SelectedSaveFile.FullPath;
        var fixerOption = Fixers[SelectedFixerIndex];

        IsProcessing = true;
        try
        {
            Log($"Loading save: {filePath}");
            var saveFile = await Task.Run(() => _saveFileService.LoadSaveFile(filePath));

            var fixer = fixerOption.CreateFixer();
            Log($"Applying fixer: {fixerOption.Name}");
            var changed = await Task.Run(() => fixer.ApplyFix(saveFile));

            if (!changed)
            {
                Log("No changes required.");
                return;
            }

            var backupPath = GetBackupFilePath(filePath);
            File.Move(filePath, backupPath, overwrite: false);
            await Task.Run(() => _saveFileService.SaveSaveFile(saveFile, filePath));

            Log("Fix complete.");
            Log($"Backup written to: {backupPath}");
            Refresh();
        }
        catch (Exception ex)
        {
            Log($"Error: {ex.Message}");
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private void OpenSessionFolder()
    {
        if (SelectedSession == null)
            return;

        try
        {
            var path = SelectedSession.FullPath;

            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer",
                    Arguments = $"\"{path}\"",
                    UseShellExecute = true
                });
                return;
            }

            if (OperatingSystem.IsLinux())
            {
                Process.Start("xdg-open", $"\"{path}\"");
                return;
            }

            if (OperatingSystem.IsMacOS())
            {
                Process.Start("open", $"\"{path}\"");
            }
        }
        catch (Exception ex)
        {
            Log($"Failed to open folder: {ex.Message}");
        }
    }

    private static string GetBackupFilePath(string originalPath)
    {
        var directory = Path.GetDirectoryName(originalPath) ?? "";
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(originalPath);
        var defaultBackupPath = Path.Combine(directory, $"{fileNameWithoutExtension}_original.sav");
        if (!File.Exists(defaultBackupPath))
            return defaultBackupPath;

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        return Path.Combine(directory, $"{fileNameWithoutExtension}_original_{timestamp}.sav");
    }

    private void Log(string message)
    {
        var builder = new StringBuilder(LogOutput);
        builder.AppendLine($"[{DateTime.Now:HH:mm:ss}] {message}");
        LogOutput = builder.ToString();
    }
}
