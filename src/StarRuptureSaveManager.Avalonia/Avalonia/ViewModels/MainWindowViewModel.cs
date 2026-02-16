using System.Reflection;

namespace StarRuptureSaveFixer.AvaloniaApp.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    public SaveBrowserViewModel SaveBrowser { get; }
    public SessionManagerViewModel SessionManager { get; }
    public FtpSyncViewModel FtpSync { get; }
    public SettingsViewModel Settings { get; }

    public MainWindowViewModel()
    {
        SaveBrowser = new SaveBrowserViewModel();
        SessionManager = new SessionManagerViewModel();
        FtpSync = new FtpSyncViewModel();
        Settings = new SettingsViewModel(OnSettingsChanged);

        OnSettingsChanged();
    }

    public string StatusMessage =>
        $"Avalonia UI Preview | Version {Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0"}";

    private void OnSettingsChanged()
    {
        var settingsService = new StarRuptureSaveFixer.Services.SettingsService();
        var settings = settingsService.LoadSettings();
        var customPath = settings.CustomSavePath;

        SaveBrowser.UpdateCustomPath(customPath);
        SessionManager.UpdateCustomPath(customPath);
        FtpSync.UpdateCustomPath(customPath);

        SaveBrowser.Refresh();
        SessionManager.Refresh();
    }
}
