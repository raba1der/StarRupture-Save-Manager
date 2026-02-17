using StarRuptureSaveFixer.Services;
using StarRuptureSaveManager.Core.Tests.TestSupport;

namespace StarRuptureSaveManager.Core.Tests;

public sealed class SessionManagerTests
{
    [Fact]
    public void GetAllSessions_ReturnsRootAndNamedSessions()
    {
        using var temp = new TemporaryDirectory();
        File.WriteAllText(System.IO.Path.Combine(temp.Path, "AutoSave0.sav"), "root");

        var sessionA = System.IO.Path.Combine(temp.Path, "SessionA");
        Directory.CreateDirectory(sessionA);
        File.WriteAllText(System.IO.Path.Combine(sessionA, "A.sav"), "A");

        var manager = new SessionManager { CustomSavePath = temp.Path };
        var sessions = manager.GetAllSessions();

        Assert.Contains(sessions, s => s.Name == "");
        Assert.Contains(sessions, s => s.Name == "SessionA");
    }

    [Fact]
    public void CopySaveToSession_WhenDestinationExists_CreatesTimestampedCopy()
    {
        using var temp = new TemporaryDirectory();
        var sourceDir = System.IO.Path.Combine(temp.Path, "Source");
        var targetDir = System.IO.Path.Combine(temp.Path, "Target");
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(targetDir);

        var sourceFile = System.IO.Path.Combine(sourceDir, "AutoSave0.sav");
        File.WriteAllText(sourceFile, "source");
        File.WriteAllText(System.IO.Path.Combine(targetDir, "AutoSave0.sav"), "existing");

        var manager = new SessionManager { CustomSavePath = temp.Path };
        var ok = manager.CopySaveToSession(sourceFile, targetDir);
        var files = Directory.GetFiles(targetDir, "*.sav");

        Assert.True(ok);
        Assert.True(files.Length >= 2);
        Assert.Contains(files, f => System.IO.Path.GetFileName(f).StartsWith("AutoSave0_"));
    }

    [Fact]
    public void DeleteSession_DoesNotDeleteRootPath()
    {
        using var temp = new TemporaryDirectory();
        var manager = new SessionManager { CustomSavePath = temp.Path };

        var ok = manager.DeleteSession(temp.Path);

        Assert.False(ok);
        Assert.True(Directory.Exists(temp.Path));
    }
}
