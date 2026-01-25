namespace StarRuptureSaveFixer.Models;

public class AppSettings
{
    public string? CustomSavePath { get; set; }
    public FtpSettings FtpSettings { get; set; } = new();
    public bool AutoBackupBeforeFix { get; set; } = true;
    public string? LastSelectedSession { get; set; }
}
