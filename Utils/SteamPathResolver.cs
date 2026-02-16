using System.IO;

namespace StarRuptureSaveFixer.Utils;

public static class SteamPathResolver
{
    private static readonly string[] SaveGameSubpathSegments = ["1631270", "remote", "Saved", "SaveGames"];

    public static IEnumerable<string> GetSteamUserDataCandidates()
    {
        if (OperatingSystem.IsWindows())
        {
            var candidates = new List<string>();

            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (!string.IsNullOrWhiteSpace(programFilesX86))
                candidates.Add(Path.Combine(programFilesX86, "Steam", "userdata"));

            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrWhiteSpace(programFiles))
                candidates.Add(Path.Combine(programFiles, "Steam", "userdata"));

            candidates.Add(Path.Combine("C:", "Program Files (x86)", "Steam", "userdata"));

            return candidates.Distinct(StringComparer.OrdinalIgnoreCase);
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
            return [];

        return
        [
            Path.Combine(home, ".steam", "steam", "userdata"),
            Path.Combine(home, ".local", "share", "Steam", "userdata"),
            Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".steam", "steam", "userdata")
        ];
    }

    public static string BuildSaveGamePath(string steamProfilePath)
    {
        return Path.Combine([steamProfilePath, .. SaveGameSubpathSegments]);
    }
}
