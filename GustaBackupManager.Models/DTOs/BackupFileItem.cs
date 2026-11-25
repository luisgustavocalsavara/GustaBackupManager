namespace GustaBackupManager.Models.DTOs;

public class BackupFileItem
{
    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public string Version { get; set; } = "-";
    public string Description { get; set; } = string.Empty;
    public bool IsRoutineBackup { get; set; }
    public string FileTypeName { get; set; } = string.Empty;
    public string UploadedBy { get; set; } = "-";
    public bool IsRegisteredInJson { get; set; }
    public bool IsAllowedByFolderPolicy { get; set; } = true;

    public string FormattedSize => FormatBytes(SizeBytes);

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double value = bytes;
        var unitIndex = 0;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }
        return $"{value:0.##} {units[unitIndex]}";
    }
}
