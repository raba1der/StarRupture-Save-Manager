namespace StarRuptureSaveFixer.Models;

public class SaveFileInfo
{
    public string FileName { get; set; } = "";
    public string FullPath { get; set; } = "";
    public string SessionName { get; set; } = "";
    public DateTime LastModified { get; set; }
    public long FileSizeBytes { get; set; }
    public bool IsBackup { get; set; }

    public string FileSizeDisplay
    {
        get
        {
            if (FileSizeBytes < 1024)
                return $"{FileSizeBytes} B";
            if (FileSizeBytes < 1024 * 1024)
                return $"{FileSizeBytes / 1024.0:F1} KB";
            return $"{FileSizeBytes / (1024.0 * 1024.0):F2} MB";
        }
    }

    public string LastModifiedDisplay => LastModified.ToString("yyyy-MM-dd HH:mm:ss");
}
