using Microsoft.Win32;
using StarRuptureSaveFixer.Services;
using System.Windows.Input;

namespace StarRuptureSaveFixer.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly Action _onSettingsChanged;

    private string _customSavePath = "";
    private string _autoDetectedPath = "";
    private bool _useCustomPath;
    private string _statusMessage = "";

    public SettingsViewModel(Action onSettingsChanged)
    {
        _settingsService = new SettingsService();
        _onSettingsChanged = onSettingsChanged;

        BrowseCommand = new RelayCommand(Browse);
        SaveCommand = new RelayCommand(Save);
        ResetToAutoCommand = new RelayCommand(ResetToAuto);

        LoadSettings();
    }

    public string CustomSavePath
    {
        get => _customSavePath;
        set
        {
            if (SetProperty(ref _customSavePath, value))
            {
                OnPropertyChanged(nameof(EffectivePath));
            }
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
            {
                OnPropertyChanged(nameof(EffectivePath));
            }
        }
    }

    public string EffectivePath => UseCustomPath && !string.IsNullOrEmpty(CustomSavePath)
        ? CustomSavePath
        : (AutoDetectedPath ?? "(Not found)");

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ICommand BrowseCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand ResetToAutoCommand { get; }

    private void LoadSettings()
    {
        var settings = _settingsService.LoadSettings();
        var sessionManager = new SessionManager();

        AutoDetectedPath = sessionManager.GetAutoDetectedPath() ?? "(Not found - Steam not detected)";
        CustomSavePath = settings.CustomSavePath ?? "";
        UseCustomPath = !string.IsNullOrEmpty(settings.CustomSavePath);
    }

    private void Browse()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Save Games Folder",
            InitialDirectory = !string.IsNullOrEmpty(CustomSavePath) && System.IO.Directory.Exists(CustomSavePath)
                ? CustomSavePath
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (dialog.ShowDialog() == true)
        {
            CustomSavePath = dialog.FolderName;
            UseCustomPath = true;
            StatusMessage = "Path selected. Click Save to apply.";
        }
    }

    private void Save()
    {
        var settings = _settingsService.LoadSettings();

        settings.CustomSavePath = UseCustomPath ? CustomSavePath : null;

        _settingsService.SaveSettings(settings);
        StatusMessage = "Settings saved!";

        // Notify other ViewModels to refresh
        _onSettingsChanged?.Invoke();
    }

    private void ResetToAuto()
    {
        UseCustomPath = false;
        CustomSavePath = "";
        StatusMessage = "Reset to auto-detect. Click Save to apply.";
    }
}
