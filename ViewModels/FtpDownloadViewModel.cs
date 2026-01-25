using StarRuptureSaveFixer.Models;
using StarRuptureSaveFixer.Services;
using StarRuptureSaveFixer.Views;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace StarRuptureSaveFixer.ViewModels;

public class FtpDownloadViewModel : ViewModelBase
{
    private readonly SessionManager _sessionManager;
    private readonly FtpService _ftpService;
    private readonly SettingsService _settings_service;

    private string _host = "";
    private int _port = 21;
    private string _username = "";
    private string _password = "";
    private string _remotePath = "/";
    private bool _useFtps;
    private bool _passiveMode = true;
    private bool _isDownloading;
    private int _downloadProgress;
    private string _downloadStatus = "";
    private bool _remoteHasSave;
    private CancellationTokenSource? _cts;
    private string? _customPath;

    public FtpDownloadViewModel(string? customPath = null)
    {
        _sessionManager = new SessionManager();
        _sessionManager.CustomSavePath = customPath;
        _customPath = customPath;
        _ftpService = new FtpService();
        _settings_service = new SettingsService();

        DownloadCommand = new AsyncRelayCommand(DownloadAsync, () => CanDownload);
        BrowseRemoteCommand = new RelayCommand(async () => await BrowseRemoteAsync(), () => CanBrowseRemote);
        CancelCommand = new RelayCommand(CancelOperation, () => IsDownloading);

        LoadSettings();
    }

    public void UpdateCustomPath(string? customPath)
    {
        _customPath = customPath;
        _sessionManager.CustomSavePath = customPath;
    }

    public void Refresh()
    {
        // no-op for parity
    }

    public string Host
    {
        get => _host;
        set
        {
            if (SetProperty(ref _host, value))
            {
                OnPropertyChanged(nameof(CanBrowseRemote));
                OnPropertyChanged(nameof(CanDownload));
            }
        }
    }

    public int Port { get => _port; set => SetProperty(ref _port, value); }
    public string Username { get => _username; set => SetProperty(ref _username, value); }
    public string Password { get => _password; set => SetProperty(ref _password, value); }
    public string RemotePath
    {
        get => _remotePath;
        set
        {
            if (SetProperty(ref _remotePath, value))
            {
                RemoteHasSave = false; // reset when path changes
                OnPropertyChanged(nameof(CanDownload));
            }
        }
    }
    public bool UseFtps { get => _useFtps; set => SetProperty(ref _useFtps, value); }
    public bool PassiveMode { get => _passiveMode; set => SetProperty(ref _passiveMode, value); }

    public bool IsDownloading
    {
        get => _isDownloading;
        private set
        {
            if (SetProperty(ref _isDownloading, value))
            {
                OnPropertyChanged(nameof(CanBrowseRemote));
                OnPropertyChanged(nameof(CanDownload));
            }
        }
    }

    public int DownloadProgress { get => _downloadProgress; private set => SetProperty(ref _downloadProgress, value); }
    public string DownloadStatus { get => _downloadStatus; private set => SetProperty(ref _downloadStatus, value); }

    public bool RemoteHasSave
    {
        get => _remoteHasSave;
        private set
        {
            if (SetProperty(ref _remoteHasSave, value))
            {
                OnPropertyChanged(nameof(CanDownload));
            }
        }
    }

    public bool CanBrowseRemote => !IsDownloading && !string.IsNullOrWhiteSpace(Host);
    public bool CanDownload => !IsDownloading && !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(RemotePath) && RemoteHasSave;

    public ICommand DownloadCommand { get; }
    public ICommand BrowseRemoteCommand { get; }
    public ICommand CancelCommand { get; }

    private void CancelOperation()
    {
        _cts?.Cancel();
        DownloadStatus = "Cancelling...";
    }

    private async Task BrowseRemoteAsync()
    {
        if (string.IsNullOrWhiteSpace(Host))
        {
            DownloadStatus = "Please enter a host first.";
            return;
        }

        var settings = CreateFtpSettings();
        string initialPath = "/";
        try
        {
            var exists = await _ftpService.RemotePathExistsAsync("/StarRupture/Saved/SaveGames", settings, Password).ConfigureAwait(false);
            if (exists) initialPath = "/StarRupture/Saved/SaveGames";
        }
        catch
        {
            // ignore
        }

        string? selectedPath = null;
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null)
        {
            DownloadStatus = "Unable to open folder browser: UI dispatcher not available.";
            return;
        }

        try
        {
            dispatcher.Invoke(() =>
            {
                var dialog = new Views.FtpFolderBrowserDialog(settings, Password, initialPath)
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
            DownloadStatus = $"Failed to show folder browser: {ex.Message}";
            return;
        }

        if (!string.IsNullOrEmpty(selectedPath))
        {
            RemotePath = selectedPath;
            DownloadStatus = "Checking remote folder for save files...";

            // Check remote for AutoSave0.sav and AutoSave0.met
            try
            {
                var listing = await _ftpService.ListRemoteDirectoryAsync(RemotePath, settings, Password).ConfigureAwait(false);
                bool hasSav = listing.Any(i => string.Equals(i.Name, "AutoSave0.sav", StringComparison.OrdinalIgnoreCase));
                bool hasMet = listing.Any(i => string.Equals(i.Name, "AutoSave0.met", StringComparison.OrdinalIgnoreCase));

                if (hasSav && hasMet)
                {
                    RemoteHasSave = true;
                    DownloadStatus = $"Remote session valid: {RemotePath}";
                }
                else
                {
                    RemoteHasSave = false;
                    DownloadStatus = "Remote path does not contain AutoSave0.sav and AutoSave0.met.";
                }
            }
            catch (Exception ex)
            {
                RemoteHasSave = false;
                DownloadStatus = $"Error checking remote path: {ex.Message}";
            }
        }
    }

    private string CombineRemotePathLocal(string basePath, string fileName)
    {
        if (string.IsNullOrEmpty(basePath) || basePath == "/")
            return "/" + fileName;

        basePath = basePath.TrimEnd('/');
        return basePath + "/" + fileName;
    }

    private async Task DownloadAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        if (string.IsNullOrWhiteSpace(RemotePath))
        {
            DownloadStatus = "No remote path selected.";
            return;
        }

        if (!RemoteHasSave)
        {
            DownloadStatus = "Remote path does not contain required save files.";
            return;
        }

        // Require existing local sessions
        var sessions = _sessionManager.GetAllSessions();
        if (sessions.Count == 0)
        {
            DownloadStatus = "No local sessions found. Cannot download.";
            return;
        }

        // Ask user to pick a session and file to overwrite
        SaveFileInfo? targetFile = null;
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null)
        {
            DownloadStatus = "UI dispatcher not available.";
            return;
        }

        dispatcher.Invoke(() =>
        {
            var dialog = new SelectLocalSaveDialog(sessions)
            {
                Owner = Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true)
            {
                targetFile = dialog.SelectedFile;
            }
        });

        if (targetFile == null)
        {
            DownloadStatus = "Download cancelled or no target selected.";
            return;
        }

        // Confirm overwrite
        var confirm = dispatcher.Invoke(() => MessageBox.Show(
            $"Are you sure you want to overwrite '{targetFile.FileName}' in session '{targetFile.SessionName}' with the remote AutoSave0.sav?",
            "Confirm Overwrite",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning));

        if (confirm != MessageBoxResult.Yes)
        {
            DownloadStatus = "Download cancelled by user.";
            return;
        }

        // Proceed to download remote AutoSave0.sav and overwrite targetFile.FullPath
        var remoteAutoSave = CombineRemotePathLocal(RemotePath, "AutoSave0.sav");

        try
        {
            IsDownloading = true;
            DownloadProgress = 0;
            DownloadStatus = "Downloading AutoSave0.sav...";

            var settings = CreateFtpSettings();
            var progress = new Progress<(int Percent, string Status)>(p =>
            {
                if (!token.IsCancellationRequested)
                {
                    DownloadProgress = p.Percent;
                    DownloadStatus = p.Status;
                }
            });

            var success = await _ftpService.DownloadFileAsync(remoteAutoSave, targetFile.FullPath, settings, Password, progress, token).ConfigureAwait(false);

            if (!token.IsCancellationRequested)
            {
                if (success)
                {
                    // Refresh main sessions list
                    var disp = Application.Current?.Dispatcher;
                    if (disp != null)
                    {
                        disp.Invoke(() =>
                        {
                            if (Application.Current.MainWindow?.DataContext is MainViewModel main)
                            {
                                main.RefreshAll();
                            }
                        });
                    }

                    DownloadStatus = $"Successfully overwrote {targetFile.FileName}";
                    DownloadProgress = 100;
                }
                else
                {
                    DownloadStatus = "Download failed.";
                }
            }
        }
        catch (OperationCanceledException)
        {
            DownloadStatus = "Download cancelled.";
            DownloadProgress = 0;
        }
        catch (Exception ex)
        {
            DownloadStatus = $"Download failed: {ex.Message}";
            DownloadProgress = 0;
        }
        finally
        {
            IsDownloading = false;
        }
    }

    private void LoadSettings()
    {
        var appSettings = _settings_service.LoadSettings();
        var ftpSettings = appSettings.FtpSettings;

        Host = ftpSettings.Host;
        Port = ftpSettings.Port;
        Username = ftpSettings.Username;
        Password = _settings_service.DecryptPassword(ftpSettings.EncryptedPassword);
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
