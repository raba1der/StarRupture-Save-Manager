using StarRuptureSaveFixer.Models;
using System.IO;

namespace StarRuptureSaveFixer.Services;

public class SessionManager
{
    private const string STEAM_USERDATA_PATH = @"C:\Program Files (x86)\Steam\userdata";
    private const string SAVE_GAME_SUBPATH = @"1631270\remote\Saved\SaveGames";

    private string? _customSavePath;
    private string? _cachedSaveGamesRoot;

    public string? CustomSavePath
    {
        get => _customSavePath;
        set
        {
            _customSavePath = value;
            _cachedSaveGamesRoot = null; // Clear cache when custom path changes
        }
    }

    public string? GetSaveGamesRoot()
    {
        // If custom path is set and valid, use it
        if (!string.IsNullOrEmpty(_customSavePath) && Directory.Exists(_customSavePath))
        {
            return _customSavePath;
        }

        if (_cachedSaveGamesRoot != null)
            return _cachedSaveGamesRoot;

        if (!Directory.Exists(STEAM_USERDATA_PATH))
            return null;

        try
        {
            var profileDirectories = Directory.GetDirectories(STEAM_USERDATA_PATH);

            foreach (var profileDir in profileDirectories)
            {
                string savePath = Path.Combine(profileDir, SAVE_GAME_SUBPATH);
                if (Directory.Exists(savePath))
                {
                    _cachedSaveGamesRoot = savePath;
                    return savePath;
                }
            }
        }
        catch
        {
            // Ignore errors during directory scanning
        }

        return null;
    }

    public string? GetAutoDetectedPath()
    {
        if (!Directory.Exists(STEAM_USERDATA_PATH))
            return null;

        try
        {
            var profileDirectories = Directory.GetDirectories(STEAM_USERDATA_PATH);

            foreach (var profileDir in profileDirectories)
            {
                string savePath = Path.Combine(profileDir, SAVE_GAME_SUBPATH);
                if (Directory.Exists(savePath))
                {
                    return savePath;
                }
            }
        }
        catch
        {
            // Ignore errors
        }

        return null;
    }

    public List<SaveSession> GetAllSessions()
    {
        var sessions = new List<SaveSession>();
        var root = GetSaveGamesRoot();

        if (root == null || !Directory.Exists(root))
            return sessions;

        try
        {
            // First, check for saves directly in the root (no session folder)
            var rootSaves = GetSaveFilesInPath(root);
            if (rootSaves.Count > 0)
            {
                var rootInfo = new DirectoryInfo(root);
                sessions.Add(new SaveSession
                {
                    Name = "",
                    FullPath = root,
                    CreatedDate = rootInfo.CreationTime,
                    ModifiedDate = rootInfo.LastWriteTime,
                    SaveCount = rootSaves.Count,
                    SaveFiles = rootSaves
                });
            }

            // Then get all session subdirectories
            foreach (var dir in Directory.GetDirectories(root))
            {
                var dirInfo = new DirectoryInfo(dir);
                var saves = GetSaveFilesInPath(dir);

                sessions.Add(new SaveSession
                {
                    Name = dirInfo.Name,
                    FullPath = dir,
                    CreatedDate = dirInfo.CreationTime,
                    ModifiedDate = dirInfo.LastWriteTime,
                    SaveCount = saves.Count,
                    SaveFiles = saves
                });
            }
        }
        catch
        {
            // Ignore errors during enumeration
        }

        return sessions.OrderByDescending(s => s.ModifiedDate).ToList();
    }

    public List<SaveFileInfo> GetSaveFilesInPath(string path)
    {
        var files = new List<SaveFileInfo>();

        if (!Directory.Exists(path))
            return files;

        try
        {
            var dirInfo = new DirectoryInfo(path);
            var sessionName = dirInfo.Parent?.FullName == GetSaveGamesRoot() ? "" : dirInfo.Name;

            foreach (var file in Directory.GetFiles(path, "*.sav"))
            {
                var fileInfo = new FileInfo(file);
                files.Add(new SaveFileInfo
                {
                    FileName = fileInfo.Name,
                    FullPath = file,
                    SessionName = sessionName,
                    LastModified = fileInfo.LastWriteTime,
                    FileSizeBytes = fileInfo.Length,
                    IsBackup = fileInfo.Name.Contains("_original", StringComparison.OrdinalIgnoreCase)
                });
            }
        }
        catch
        {
            // Ignore errors during file enumeration
        }

        return files.OrderByDescending(f => f.LastModified).ToList();
    }

    public SaveSession? CreateSession(string sessionName)
    {
        var root = GetSaveGamesRoot();
        if (root == null)
            return null;

        var sessionPath = Path.Combine(root, sessionName);

        if (Directory.Exists(sessionPath))
            return null; // Session already exists

        try
        {
            var dirInfo = Directory.CreateDirectory(sessionPath);
            return new SaveSession
            {
                Name = sessionName,
                FullPath = sessionPath,
                CreatedDate = dirInfo.CreationTime,
                ModifiedDate = dirInfo.LastWriteTime,
                SaveCount = 0,
                SaveFiles = new List<SaveFileInfo>()
            };
        }
        catch
        {
            return null;
        }
    }

    public bool DeleteSession(string sessionPath)
    {
        var root = GetSaveGamesRoot();
        if (root == null)
            return false;

        // Safety check: don't delete the root or paths outside it
        if (sessionPath == root || !sessionPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            if (Directory.Exists(sessionPath))
            {
                Directory.Delete(sessionPath, recursive: true);
                return true;
            }
        }
        catch
        {
            // Ignore deletion errors
        }

        return false;
    }

    public bool CopySaveToSession(string sourceFilePath, string targetSessionPath)
    {
        if (!File.Exists(sourceFilePath))
            return false;

        if (!Directory.Exists(targetSessionPath))
            return false;

        try
        {
            var fileName = Path.GetFileName(sourceFilePath);
            var targetPath = Path.Combine(targetSessionPath, fileName);

            // If file already exists, add timestamp to prevent overwrite
            if (File.Exists(targetPath))
            {
                var nameWithoutExt = Path.GetFileNameWithoutExtension(sourceFilePath);
                var ext = Path.GetExtension(sourceFilePath);
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                targetPath = Path.Combine(targetSessionPath, $"{nameWithoutExt}_{timestamp}{ext}");
            }

            File.Copy(sourceFilePath, targetPath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool SessionExists(string sessionName)
    {
        var root = GetSaveGamesRoot();
        if (root == null)
            return false;

        var sessionPath = Path.Combine(root, sessionName);
        return Directory.Exists(sessionPath);
    }
}
