using StarRuptureSaveFixer.Services;
using System.Text;
using System.Windows.Input;

namespace StarRuptureSaveFixer.AvaloniaApp.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService = new();
    private readonly Action _onSettingsChanged;

    private string _customSavePath = "";
    private string _autoDetectedPath = "";
    private bool _useCustomPath;
    private string _statusMessage = "";
    private string _statusLog = "";

    public SettingsViewModel(Action onSettingsChanged)
    {
        _onSettingsChanged = onSettingsChanged;

        SaveCommand = new RelayCommand(Save);
        ResetToAutoCommand = new RelayCommand(ResetToAuto);
        ReloadCommand = new RelayCommand(LoadSettings);

        LoadSettings();
    }

    public string CustomSavePath
    {
        get => _customSavePath;
        set
        {
            if (SetProperty(ref _customSavePath, value))
                OnPropertyChanged(nameof(EffectivePath));
        }
    }

    public string AutoDetectedPath
    {
        get => _autoDetectedPath;
        set => SetProperty(ref _autoDetectedPath, value);
    }

    public bool UseCustomPath
    {
        get => _useCustomPath;
        set
        {
            if (SetProperty(ref _useCustomPath, value))
                OnPropertyChanged(nameof(EffectivePath));
        }
    }

    public string EffectivePath => UseCustomPath && !string.IsNullOrWhiteSpace(CustomSavePath)
        ? CustomSavePath
        : AutoDetectedPath;

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string StatusLog
    {
        get => _statusLog;
        private set => SetProperty(ref _statusLog, value);
    }

    public ICommand SaveCommand { get; }
    public ICommand ResetToAutoCommand { get; }
    public ICommand ReloadCommand { get; }

    public void LoadSettings()
    {
        var settings = _settingsService.LoadSettings();
        var sessionManager = new SessionManager();

        AutoDetectedPath = sessionManager.GetAutoDetectedPath() ?? "(Not found - Steam path not detected)";
        CustomSavePath = settings.CustomSavePath ?? "";
        UseCustomPath = !string.IsNullOrWhiteSpace(settings.CustomSavePath);
        SetStatus("Settings loaded.");
    }

    private void Save()
    {
        var trimmedCustomPath = CustomSavePath.Trim();
        if (UseCustomPath)
        {
            if (string.IsNullOrWhiteSpace(trimmedCustomPath))
            {
                SetStatus("Custom path is enabled but empty.", true);
                return;
            }

            if (!Directory.Exists(trimmedCustomPath))
            {
                SetStatus("Custom path does not exist.", true);
                return;
            }
        }

        var settings = _settingsService.LoadSettings();
        settings.CustomSavePath = UseCustomPath
            ? trimmedCustomPath
            : null;

        _settingsService.SaveSettings(settings);
        SetStatus("Settings saved.");
        _onSettingsChanged.Invoke();
    }

    private void ResetToAuto()
    {
        UseCustomPath = false;
        CustomSavePath = "";
        SetStatus("Reset to auto-detect. Save to apply.");
    }

    private void SetStatus(string message, bool isError = false)
    {
        StatusMessage = message;
        var builder = new StringBuilder(StatusLog);
        builder.AppendLine($"[{DateTime.Now:HH:mm:ss}] {(isError ? "ERROR" : "INFO ")} {message}");
        StatusLog = builder.ToString();
    }
}
