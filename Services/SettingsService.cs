using StarRuptureSaveFixer.Models;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StarRuptureSaveFixer.Services;

public class SettingsService
{
    private readonly string _settingsPath;
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("StarRuptureSaveFixer_v1");
    private readonly LoggingService _logger = LoggingService.Instance;

    public SettingsService()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataPath, "SRSM");

        if (!Directory.Exists(appFolder))
            Directory.CreateDirectory(appFolder);

        _settingsPath = Path.Combine(appFolder, "settings.json");
        _logger.LogInfo($"Settings path: {_settingsPath}", "SettingsService");
    }

    public AppSettings LoadSettings()
    {
        _logger.LogInfo("Loading application settings", "SettingsService");

        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                _logger.LogInfo("Settings loaded successfully", "SettingsService");
                return settings ?? new AppSettings();
            }

            _logger.LogInfo("No settings file found, using defaults", "SettingsService");
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to load settings, using defaults", ex, "SettingsService");
            // If loading fails, return default settings
        }

        return new AppSettings();
    }

    public void SaveSettings(AppSettings settings)
    {
        _logger.LogInfo("Saving application settings", "SettingsService");

        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(settings, options);
            File.WriteAllText(_settingsPath, json);
            _logger.LogInfo("Settings saved successfully", "SettingsService");
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to save settings", ex, "SettingsService");
            // Ignore save errors
        }
    }

    public string EncryptPassword(string plainPassword)
    {
        if (string.IsNullOrEmpty(plainPassword))
            return "";

        try
        {
            var plainBytes = Encoding.UTF8.GetBytes(plainPassword);
            var encryptedBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
            // Don't log the encrypted password
            _logger.LogInfo("Password encrypted successfully", "SettingsService");
            return Convert.ToBase64String(encryptedBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to encrypt password", ex, "SettingsService");
            return "";
        }
    }

    public string DecryptPassword(string encryptedPassword)
    {
        if (string.IsNullOrEmpty(encryptedPassword))
            return "";

        try
        {
            var encryptedBytes = Convert.FromBase64String(encryptedPassword);
            var plainBytes = ProtectedData.Unprotect(encryptedBytes, Entropy, DataProtectionScope.CurrentUser);
            // Don't log the decrypted password
            _logger.LogInfo("Password decrypted successfully", "SettingsService");
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to decrypt password", ex, "SettingsService");
            return "";
        }
    }
}
