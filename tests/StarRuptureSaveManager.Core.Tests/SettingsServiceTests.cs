using StarRuptureSaveFixer.Models;
using StarRuptureSaveFixer.Services;
using StarRuptureSaveManager.Core.Tests.TestSupport;

namespace StarRuptureSaveManager.Core.Tests;

public sealed class SettingsServiceTests
{
    [Fact]
    public void EncryptDecryptPassword_RoundTrips()
    {
        using var temp = new TemporaryDirectory();
        using var _ = ScopedTestConfig(temp.Path);
        var service = new SettingsService();

        var encrypted = service.EncryptPassword("p@ssw0rd!");
        var decrypted = service.DecryptPassword(encrypted);

        Assert.NotEmpty(encrypted);
        Assert.Equal("p@ssw0rd!", decrypted);
    }

    [Fact]
    public void DecryptPassword_InvalidInput_ReturnsEmpty()
    {
        using var temp = new TemporaryDirectory();
        using var _ = ScopedTestConfig(temp.Path);
        var service = new SettingsService();

        var decrypted = service.DecryptPassword("not-a-real-payload");
        Assert.Equal("", decrypted);
    }

    [Fact]
    public void SaveAndLoadSettings_RoundTrips()
    {
        using var temp = new TemporaryDirectory();
        using var _ = ScopedTestConfig(temp.Path);
        var service = new SettingsService();

        var input = new AppSettings
        {
            CustomSavePath = "/tmp/test-path",
            AutoBackupBeforeFix = true
        };

        service.SaveSettings(input);
        var loaded = service.LoadSettings();

        Assert.Equal(input.CustomSavePath, loaded.CustomSavePath);
        Assert.Equal(input.AutoBackupBeforeFix, loaded.AutoBackupBeforeFix);
    }

    private static IDisposable ScopedTestConfig(string root)
    {
        Directory.CreateDirectory(root);
        var xdgConfig = new ScopedEnvVar("XDG_CONFIG_HOME", root);
        var xdgData = new ScopedEnvVar("XDG_DATA_HOME", root);
        var home = new ScopedEnvVar("HOME", root);
        var appData = new ScopedEnvVar("APPDATA", root);
        var localAppData = new ScopedEnvVar("LOCALAPPDATA", root);
        return new CompositeDisposable(xdgConfig, xdgData, home, appData, localAppData);
    }

    private sealed class CompositeDisposable : IDisposable
    {
        private readonly IDisposable[] _disposables;

        public CompositeDisposable(params IDisposable[] disposables)
        {
            _disposables = disposables;
        }

        public void Dispose()
        {
            foreach (var disposable in _disposables.Reverse())
                disposable.Dispose();
        }
    }
}
