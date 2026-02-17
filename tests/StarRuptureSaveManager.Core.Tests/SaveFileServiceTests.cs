using StarRuptureSaveFixer.Models;
using StarRuptureSaveFixer.Services;
using StarRuptureSaveManager.Core.Tests.TestSupport;

namespace StarRuptureSaveManager.Core.Tests;

public sealed class SaveFileServiceTests
{
    [Fact]
    public void SaveAndLoad_RoundTrip_PreservesJson()
    {
        using var temp = new TemporaryDirectory();
        var service = new SaveFileService();
        var path = System.IO.Path.Combine(temp.Path, "sample.sav");

        var input = new SaveFile
        {
            FilePath = path,
            JsonContent = "{\"itemData\":{\"Mass\":{\"entities\":{\"(ID=1)\":{\"name\":\"entity\"}}}}}"
        };

        service.SaveSaveFile(input, path);
        var loaded = service.LoadSaveFile(path);

        Assert.Equal(input.JsonContent, loaded.JsonContent);
    }

    [Fact]
    public void LoadSaveFile_WhenMissing_ThrowsFileNotFound()
    {
        using var temp = new TemporaryDirectory();
        var service = new SaveFileService();

        var missing = System.IO.Path.Combine(temp.Path, "missing.sav");
        Assert.Throws<FileNotFoundException>(() => service.LoadSaveFile(missing));
    }

    [Fact]
    public void LoadSaveFile_WhenTooSmall_ThrowsInvalidData()
    {
        using var temp = new TemporaryDirectory();
        var service = new SaveFileService();
        var file = System.IO.Path.Combine(temp.Path, "bad.sav");
        File.WriteAllBytes(file, [1, 2, 3]);

        Assert.Throws<InvalidDataException>(() => service.LoadSaveFile(file));
    }
}
