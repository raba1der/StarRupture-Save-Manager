using StarRuptureSaveFixer.Models;
using StarRuptureSaveFixer.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace StarRuptureSaveFixer.AvaloniaApp.ViewModels;

public sealed class FtpSyncViewModel : ViewModelBase
{
    private readonly SessionManager _sessionManager = new();
    private readonly FtpService _ftpService = new();
    private readonly SettingsService _settingsService = new();

    private string _host = "";
    private int _port = 21;
    private string _username = "";
    private string _password = "";
    private string _remotePath = "/StarRupture/Saved/SaveGames";
    private string _remoteFileName = "AutoSave0.sav";
    private FileTransferProtocol _protocol = FileTransferProtocol.FTP;
    private bool _passiveMode = true;

    private SaveSession? _selectedSession;
    private SaveFileInfo? _selectedFile;
    private int _progressPercent;
    private string _statusMessage = "Ready.";
    private bool _isBusy;

    public FtpSyncViewModel()
    {
        Sessions = new ObservableCollection<SaveSession>();
        ProtocolOptions = new ObservableCollection<FileTransferProtocol>(Enum.GetValues<FileTransferProtocol>());

        RefreshCommand = new RelayCommand(Refresh);
        TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync, () => CanExecuteActions);
        UploadCommand = new AsyncRelayCommand(UploadAsync, () => CanUpload);
        DownloadCommand = new AsyncRelayCommand(DownloadAsync, () => CanDownload);
        SaveFtpSettingsCommand = new RelayCommand(SaveFtpSettings);
        LoadFtpSettingsCommand = new RelayCommand(LoadFtpSettings);

        LoadFtpSettings();
        Refresh();
    }

    public void UpdateCustomPath(string? customPath)
    {
        _sessionManager.CustomSavePath = customPath;
        Refresh();
    }

    public ObservableCollection<SaveSession> Sessions { get; }
    public ObservableCollection<FileTransferProtocol> ProtocolOptions { get; }

    public string Host
    {
        get => _host;
        set => SetProperty(ref _host, value);
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

    public string RemoteFileName
    {
        get => _remoteFileName;
        set => SetProperty(ref _remoteFileName, value);
    }

    public FileTransferProtocol Protocol
    {
        get => _protocol;
        set
        {
            if (SetProperty(ref _protocol, value))
            {
                if (value == FileTransferProtocol.SFTP && Port == 21)
                    Port = 22;
                else if (value != FileTransferProtocol.SFTP && Port == 22)
                    Port = 21;
            }
        }
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
                SelectedFile = null;
                OnPropertyChanged(nameof(FilesInSession));
                RaiseActionCanExecuteChanged();
            }
        }
    }

    public SaveFileInfo? SelectedFile
    {
        get => _selectedFile;
        set
        {
            if (SetProperty(ref _selectedFile, value))
                RaiseActionCanExecuteChanged();
        }
    }

    public IEnumerable<SaveFileInfo> FilesInSession =>
        SelectedSession?.SaveFiles ?? Enumerable.Empty<SaveFileInfo>();

    public int ProgressPercent
    {
        get => _progressPercent;
        set => SetProperty(ref _progressPercent, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
                RaiseActionCanExecuteChanged();
        }
    }

    public bool CanExecuteActions =>
        !IsBusy &&
        !string.IsNullOrWhiteSpace(Host) &&
        !string.IsNullOrWhiteSpace(Username) &&
        !string.IsNullOrWhiteSpace(Password);

    public bool CanUpload => CanExecuteActions && SelectedFile != null;
    public bool CanDownload => CanExecuteActions && SelectedSession != null;

    public ICommand RefreshCommand { get; }
    public ICommand TestConnectionCommand { get; }
    public ICommand UploadCommand { get; }
    public ICommand DownloadCommand { get; }
    public ICommand SaveFtpSettingsCommand { get; }
    public ICommand LoadFtpSettingsCommand { get; }

    public void Refresh()
    {
        var previous = SelectedSession?.FullPath;

        Sessions.Clear();
        foreach (var session in _sessionManager.GetAllSessions())
            Sessions.Add(session);

        if (!string.IsNullOrWhiteSpace(previous))
            SelectedSession = Sessions.FirstOrDefault(s => s.FullPath == previous);
        else
            SelectedSession = Sessions.FirstOrDefault();
    }

    private async Task TestConnectionAsync()
    {
        await RunWithProgress(async () =>
        {
            StatusMessage = "Testing connection...";
            var result = await _ftpService.TestConnectionAsync(CreateFtpSettings(), Password);
            ProgressPercent = result.Success ? 100 : 0;
            StatusMessage = result.Message;
        });
    }

    private async Task UploadAsync()
    {
        if (SelectedFile == null)
            return;

        await RunWithProgress(async () =>
        {
            var progress = new Progress<(int Percent, string Status)>(p =>
            {
                ProgressPercent = p.Percent;
                StatusMessage = p.Status;
            });

            var ok = await _ftpService.UploadFileAsAsync(
                SelectedFile.FullPath,
                RemotePath,
                RemoteFileName,
                CreateFtpSettings(),
                Password,
                progress);

            if (!ok)
                StatusMessage = "Upload failed.";
        });
    }

    private async Task DownloadAsync()
    {
        if (SelectedSession == null)
            return;

        await RunWithProgress(async () =>
        {
            var localFile = Path.Combine(SelectedSession.FullPath, RemoteFileName);
            var remoteFile = $"{RemotePath.TrimEnd('/')}/{RemoteFileName}";

            var progress = new Progress<(int Percent, string Status)>(p =>
            {
                ProgressPercent = p.Percent;
                StatusMessage = p.Status;
            });

            var ok = await _ftpService.DownloadFileAsync(
                remoteFile,
                localFile,
                CreateFtpSettings(),
                Password,
                progress);

            if (!ok)
            {
                StatusMessage = "Download failed.";
                return;
            }

            StatusMessage = $"Downloaded to {localFile}.";
            Refresh();
        });
    }

    private async Task RunWithProgress(Func<Task> action)
    {
        IsBusy = true;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            ProgressPercent = 0;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void SaveFtpSettings()
    {
        var appSettings = _settingsService.LoadSettings();
        appSettings.FtpSettings = CreateFtpSettings();
        appSettings.FtpSettings.EncryptedPassword = _settingsService.EncryptPassword(Password);
        _settingsService.SaveSettings(appSettings);
        StatusMessage = "FTP settings saved.";
    }

    private void LoadFtpSettings()
    {
        var appSettings = _settingsService.LoadSettings();
        var ftp = appSettings.FtpSettings;

        Host = ftp.Host;
        Port = ftp.Port;
        Username = ftp.Username;
        Protocol = ftp.Protocol;
        PassiveMode = ftp.PassiveMode;
        Password = _settingsService.DecryptPassword(ftp.EncryptedPassword);
    }

    private FtpSettings CreateFtpSettings()
    {
        return new FtpSettings
        {
            Host = Host.Trim(),
            Port = Port,
            Username = Username.Trim(),
            Protocol = Protocol,
            UseFtps = Protocol == FileTransferProtocol.FTPS,
            PassiveMode = PassiveMode
        };
    }

    private void RaiseActionCanExecuteChanged()
    {
        ((AsyncRelayCommand)TestConnectionCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)UploadCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)DownloadCommand).RaiseCanExecuteChanged();
    }
}
