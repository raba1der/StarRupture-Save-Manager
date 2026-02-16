using StarRuptureSaveFixer.Models;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StarRuptureSaveFixer.Services;

public class SettingsService
{
    private readonly string _settingsPath;
    private readonly string _encryptionKeyPath;
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("StarRuptureSaveFixer_v1");
    private readonly LoggingService _logger = LoggingService.Instance;

    public SettingsService()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataPath, "SRSM");

        if (!Directory.Exists(appFolder))
            Directory.CreateDirectory(appFolder);

        _settingsPath = Path.Combine(appFolder, "settings.json");
        _encryptionKeyPath = Path.Combine(appFolder, "key.bin");
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
            if (TryProtectWithDpapi(plainBytes, out var protectedBytes))
            {
                _logger.LogInfo("Password encrypted with DPAPI", "SettingsService");
                return $"dpapi:{Convert.ToBase64String(protectedBytes)}";
            }

            var key = GetOrCreateCrossPlatformKey();
            var nonce = RandomNumberGenerator.GetBytes(12);
            var cipherText = new byte[plainBytes.Length];
            var tag = new byte[16];

            using var aes = new AesGcm(key, tag.Length);
            aes.Encrypt(nonce, plainBytes, cipherText, tag);

            var payload = new byte[nonce.Length + tag.Length + cipherText.Length];
            Buffer.BlockCopy(nonce, 0, payload, 0, nonce.Length);
            Buffer.BlockCopy(tag, 0, payload, nonce.Length, tag.Length);
            Buffer.BlockCopy(cipherText, 0, payload, nonce.Length + tag.Length, cipherText.Length);

            _logger.LogInfo("Password encrypted with cross-platform key", "SettingsService");
            return $"aes:{Convert.ToBase64String(payload)}";
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
            if (encryptedPassword.StartsWith("dpapi:", StringComparison.Ordinal))
            {
                var protectedPayload = encryptedPassword["dpapi:".Length..];
                var encryptedBytes = Convert.FromBase64String(protectedPayload);
                if (TryUnprotectWithDpapi(encryptedBytes, out var plainBytes))
                {
                    _logger.LogInfo("Password decrypted with DPAPI", "SettingsService");
                    return Encoding.UTF8.GetString(plainBytes);
                }

                return "";
            }

            if (encryptedPassword.StartsWith("aes:", StringComparison.Ordinal))
            {
                var payload = Convert.FromBase64String(encryptedPassword["aes:".Length..]);
                if (payload.Length < 28)
                    return "";

                var nonce = payload[..12];
                var tag = payload[12..28];
                var cipherText = payload[28..];

                var plainBytes = new byte[cipherText.Length];
                var key = GetOrCreateCrossPlatformKey();

                using var aes = new AesGcm(key, tag.Length);
                aes.Decrypt(nonce, cipherText, tag, plainBytes);

                _logger.LogInfo("Password decrypted with cross-platform key", "SettingsService");
                return Encoding.UTF8.GetString(plainBytes);
            }

            if (TryUnprotectWithDpapi(Convert.FromBase64String(encryptedPassword), out var legacyPlainBytes))
            {
                _logger.LogInfo("Password decrypted with legacy DPAPI format", "SettingsService");
                return Encoding.UTF8.GetString(legacyPlainBytes);
            }

            return "";
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to decrypt password", ex, "SettingsService");
            return "";
        }
    }

    private byte[] GetOrCreateCrossPlatformKey()
    {
        if (File.Exists(_encryptionKeyPath))
            return File.ReadAllBytes(_encryptionKeyPath);

        var key = RandomNumberGenerator.GetBytes(32);
        File.WriteAllBytes(_encryptionKeyPath, key);

        if (!OperatingSystem.IsWindows())
        {
            try
            {
                File.SetUnixFileMode(
                    _encryptionKeyPath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            catch
            {
                // Permission hardening is best effort.
            }
        }

        return key;
    }

    private static bool TryProtectWithDpapi(byte[] plainBytes, out byte[] protectedBytes)
    {
        protectedBytes = [];
        return TryInvokeDpapi("Protect", plainBytes, out protectedBytes);
    }

    private static bool TryUnprotectWithDpapi(byte[] encryptedBytes, out byte[] plainBytes)
    {
        plainBytes = [];
        return TryInvokeDpapi("Unprotect", encryptedBytes, out plainBytes);
    }

    private static bool TryInvokeDpapi(string methodName, byte[] input, out byte[] output)
    {
        output = [];

        if (!OperatingSystem.IsWindows())
            return false;

        try
        {
            const string assemblyQualifiedName = "System.Security.Cryptography.ProtectedData, System.Security.Cryptography.ProtectedData";
            const string scopeQualifiedName = "System.Security.Cryptography.DataProtectionScope, System.Security.Cryptography.ProtectedData";

            var protectedDataType = Type.GetType(assemblyQualifiedName, throwOnError: false);
            var scopeType = Type.GetType(scopeQualifiedName, throwOnError: false);
            if (protectedDataType == null || scopeType == null)
                return false;

            var scopeValue = Enum.Parse(scopeType, "CurrentUser");
            var method = protectedDataType.GetMethod(methodName, [typeof(byte[]), typeof(byte[]), scopeType]);
            if (method == null)
                return false;

            var result = method.Invoke(null, [input, Entropy, scopeValue]) as byte[];
            if (result == null || result.Length == 0)
                return false;

            output = result;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
