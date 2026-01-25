namespace StarRuptureSaveFixer.Models;

public class SaveSession
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public DateTime CreatedDate { get; set; }
    public DateTime ModifiedDate { get; set; }
    public int SaveCount { get; set; }
    public List<SaveFileInfo> SaveFiles { get; set; } = new();

    public string DisplayName => string.IsNullOrEmpty(Name) ? "(Root)" : Name;
}
