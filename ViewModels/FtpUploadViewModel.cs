using StarRuptureSaveFixer.Models;
using StarRuptureSaveFixer.Services;
using StarRuptureSaveFixer.Views;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace StarRuptureSaveFixer.ViewModels;

public class FtpUploadViewModel : ViewModelBase
{
    private readonly SessionManager _sessionManager;
    private readonly FtpService _ftpService;
    private readonly SettingsService _settingsService;

    private string _host = "";
    private int _port = 21;
    private string _username = "";
    private string _password = "";
    private string _remotePath = "/";
    private bool _useFtps;
    private bool _passiveMode = true;
    private SaveSession? _selectedSession;
    private SaveFileInfo? _selectedFile;
    private int _uploadProgress;
    private string _uploadStatus = "";
    private bool _isUploading;
    private bool _remoteSessionSelected;
    private CancellationTokenSource? _cts;

    private const string REMOTE_SAVEGAMES_PATH = "/StarRupture/Saved/SaveGames";

    public FtpUploadViewModel(string? customPath = null)
    {
        _sessionManager = new SessionManager();
        _sessionManager.CustomSavePath = customPath;
        _ftpService = new FtpService();
        _settingsService = new SettingsService();

        Sessions = new ObservableCollection<SaveSession>();
        AvailableFiles = new ObservableCollection<SaveFileInfo>();

        TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync, () => CanTestConnection);
        UploadCommand = new AsyncRelayCommand(UploadAsync, () => CanUpload);
        BrowseRemoteCommand = new RelayCommand(async () => await BrowseRemoteAsync(), () => CanBrowseRemote);
        SaveSettingsCommand = new RelayCommand(SaveSettings);
        RefreshCommand = new RelayCommand(Refresh);
        CancelCommand = new RelayCommand(CancelOperation, () => IsUploading);

        LoadSettings();
        Refresh();
    }

    public void UpdateCustomPath(string? customPath)
    {
        _sessionManager.CustomSavePath = customPath;
    }

    public ObservableCollection<SaveSession> Sessions { get; }
    public ObservableCollection<SaveFileInfo> AvailableFiles { get; }

    public string Host
    {
        get => _host;
        set
        {
            if (SetProperty(ref _host, value))
            {
                OnPropertyChanged(nameof(CanTestConnection));
                OnPropertyChanged(nameof(CanBrowseRemote));
                OnPropertyChanged(nameof(CanUpload));
            }
        }
    }

    public int Port
    {
        get => _port;
        set => SetProperty(ref _port, value);
    }

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public string RemotePath
    {
        get => _remotePath;
        set => SetProperty(ref _remotePath, value);
    }

    public bool UseFtps
    {
        get => _useFtps;
        set => SetProperty(ref _useFtps, value);
    }

    public bool PassiveMode
    {
        get => _passiveMode;
        set => SetProperty(ref _passiveMode, value);
    }

    public SaveSession? SelectedSession
    {
        get => _selectedSession;
        set
        {
            if (SetProperty(ref _selectedSession, value))
            {
                RefreshFileList();
            }
        }
    }

    public SaveFileInfo? SelectedFile
    {
        get => _selectedFile;
        set
        {
            if (SetProperty(ref _selectedFile, value))
            {
                OnPropertyChanged(nameof(CanUpload));
                OnPropertyChanged(nameof(SelectedFileInfo));
            }
        }
    }

    public string SelectedFileInfo => SelectedFile != null
        ? $"Selected: {SelectedFile.FileName} ({SelectedFile.FileSizeDisplay}) - Will be uploaded as AutoSave0.sav"
        : "Select a save file to upload";

    public int UploadProgress
    {
        get => _uploadProgress;
        set => SetProperty(ref _uploadProgress, value);
    }

    public string UploadStatus
    {
        get => _uploadStatus;
        set => SetProperty(ref _uploadStatus, value);
    }

    public bool IsUploading
    {
        get => _isUploading;
        set
        {
            if (SetProperty(ref _isUploading, value))
            {
                OnPropertyChanged(nameof(CanTestConnection));
                OnPropertyChanged(nameof(CanUpload));
                OnPropertyChanged(nameof(CanBrowseRemote));
            }
        }
    }

    public bool RemoteSessionSelected
    {
        get => _remoteSessionSelected;
        private set
        {
            if (SetProperty(ref _remoteSessionSelected, value))
            {
                OnPropertyChanged(nameof(CanUpload));
            }
        }
    }

    public bool CanTestConnection => !IsUploading && !string.IsNullOrWhiteSpace(Host);
    public bool CanUpload => !IsUploading && !string.IsNullOrWhiteSpace(Host) && SelectedFile != null && RemoteSessionSelected;
    public bool CanBrowseRemote => !IsUploading && !string.IsNullOrWhiteSpace(Host);

    public ICommand TestConnectionCommand { get; }
    public ICommand UploadCommand { get; }
    public ICommand BrowseRemoteCommand { get; }
    public ICommand SaveSettingsCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand CancelCommand { get; }

    public void Refresh()
    {
        var previousSelection = SelectedSession?.FullPath;
        Sessions.Clear();

        foreach (var session in _sessionManager.GetAllSessions())
        {
            Sessions.Add(session);
        }

        if (previousSelection != null)
            SelectedSession = Sessions.FirstOrDefault(s => s.FullPath == previousSelection);
        else if (Sessions.Count > 0)
            SelectedSession = Sessions[0];
    }

    private void RefreshFileList()
    {
        AvailableFiles.Clear();
        SelectedFile = null;

        if (SelectedSession == null)
            return;

        foreach (var file in SelectedSession.SaveFiles.Where(f => !f.IsBackup))
        {
            AvailableFiles.Add(file);
        }
    }

    private void CancelOperation()
    {
        _cts?.Cancel();
        UploadStatus = "Cancelling...";
    }

    private async Task TestConnectionAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        IsUploading = true;
        UploadStatus = "Testing connection...";
        UploadProgress = 0;

        try
        {
            var settings = CreateFtpSettings();
            var (success, message) = await _ftpService.TestConnectionAsync(settings, Password, token).ConfigureAwait(false);

            if (!token.IsCancellationRequested)
            {
                UploadStatus = message;
                UploadProgress = success ? 100 : 0;
            }
        }
        catch (OperationCanceledException)
        {
            UploadStatus = "Connection test cancelled.";
            UploadProgress = 0;
        }
        finally
        {
            IsUploading = false;
        }
    }

    private async Task BrowseRemoteAsync()
    {
        if (string.IsNullOrWhiteSpace(Host))
        {
            UploadStatus = "Please enter a host first.";
            return;
        }

        var settings = CreateFtpSettings();

        // Try detecting the StarRupture savegames path on the server
        string initialPath = "/";
        try
        {
            var exists = await _ftpService.RemotePathExistsAsync(REMOTE_SAVEGAMES_PATH, settings, Password).ConfigureAwait(false);
            if (exists)
            {
                initialPath = REMOTE_SAVEGAMES_PATH;
            }
        }
        catch
        {
            // ignore and fall back to root
        }

        // Show folder browser dialog starting at the detected path on the UI thread
        string? selectedPath = null;
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null)
        {
            // Can't show dialog safely without a UI dispatcher
            UploadStatus = "Unable to open folder browser: UI dispatcher not available.";
            return;
        }

        try
        {
            dispatcher.Invoke(() =>
            {
                var dialog = new FtpFolderBrowserDialog(settings, Password, initialPath)
                {
                    Owner = Application.Current.MainWindow
                };

                if (dialog.ShowDialog() == true)
                {
                    selectedPath = dialog.SelectedPath;
                }
            });
        }
        catch (Exception ex)
        {
            UploadStatus = $"Failed to show folder browser: {ex.Message}";
            return;
        }

        if (!string.IsNullOrEmpty(selectedPath))
        {
            RemotePath = selectedPath;
            // Only consider this a valid session selection if it's within the savegames path and not the base folder
            if (RemotePath.StartsWith(REMOTE_SAVEGAMES_PATH) && RemotePath != REMOTE_SAVEGAMES_PATH)
            {
                RemoteSessionSelected = true;
                UploadStatus = $"Selected remote session: {RemotePath}";
            }
            else
            {
                RemoteSessionSelected = false;
                UploadStatus = "Please select a session folder inside /StarRupture/Saved/SaveGames";
            }
        }
    }

    private async Task UploadAsync()
    {
        if (SelectedFile == null)
        {
            UploadStatus = "No file selected for upload.";
            return;
        }

        if (!RemoteSessionSelected)
        {
            UploadStatus = "Please select a remote session folder before uploading.";
            return;
        }

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        IsUploading = true;
        UploadProgress = 0;
        UploadStatus = "Starting upload...";

        try
        {
            var settings = CreateFtpSettings();
            var progress = new Progress<(int Percent, string Status)>(p =>
            {
                if (!token.IsCancellationRequested)
                {
                    UploadProgress = p.Percent;
                    UploadStatus = p.Status;
                }
            });

            // Always upload as AutoSave0.sav
            var success = await _ftpService.UploadFileAsAsync(
                SelectedFile.FullPath,
                RemotePath,
                "AutoSave0.sav",
                settings,
                Password,
                progress,
                token).ConfigureAwait(false);

            if (!token.IsCancellationRequested)
            {
                if (success)
                {
                    UploadStatus = "Successfully uploaded as AutoSave0.sav!";
                }
                else
                {
                    UploadStatus = "Upload failed.";
                }
            }
        }
        catch (OperationCanceledException)
        {
            UploadStatus = "Upload cancelled.";
            UploadProgress = 0;
        }
        catch (Exception ex)
        {
            UploadStatus = $"Upload failed: {ex.Message}";
            UploadProgress = 0;
        }
        finally
        {
            IsUploading = false;
        }
    }

    private void SaveSettings()
    {
        var appSettings = _settingsService.LoadSettings();

        appSettings.FtpSettings = CreateFtpSettings();
        appSettings.FtpSettings.EncryptedPassword = _settingsService.EncryptPassword(Password);

        _settingsService.SaveSettings(appSettings);
        UploadStatus = "Settings saved!";
    }

    private void LoadSettings()
    {
        var appSettings = _settingsService.LoadSettings();
        var ftpSettings = appSettings.FtpSettings;

        Host = ftpSettings.Host;
        Port = ftpSettings.Port;
        Username = ftpSettings.Username;
        Password = _settingsService.DecryptPassword(ftpSettings.EncryptedPassword);
        RemotePath = ftpSettings.RemotePath;
        UseFtps = ftpSettings.UseFtps;
        PassiveMode = ftpSettings.PassiveMode;
    }

    private FtpSettings CreateFtpSettings()
    {
        return new FtpSettings
        {
            Host = Host,
            Port = Port,
            Username = Username,
            RemotePath = RemotePath,
            UseFtps = UseFtps,
            PassiveMode = PassiveMode
        };
    }
}
