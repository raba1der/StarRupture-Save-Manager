using System.IO;

namespace StarRuptureSaveFixer.Utils;

public static class SavePathHelper
{
    /// <summary>
    /// Finds the best default path for the save file dialog.
    /// Priority:
    /// 1. First valid Steam profile path containing the save game directory
    /// 2. Steam userdata root if it exists
    /// 3. null (system default)
    /// </summary>
    public static string? GetDefaultSavePath()
    {
        foreach (var userDataPath in SteamPathResolver.GetSteamUserDataCandidates())
        {
            if (!Directory.Exists(userDataPath))
                continue;

            try
            {
                var profileDirectories = Directory.GetDirectories(userDataPath);
                foreach (var profileDir in profileDirectories)
                {
                    var savePath = SteamPathResolver.BuildSaveGamePath(profileDir);
                    if (Directory.Exists(savePath))
                        return savePath;
                }

                return userDataPath;
            }
            catch
            {
                return userDataPath;
            }
        }

        return null;
    }
}
