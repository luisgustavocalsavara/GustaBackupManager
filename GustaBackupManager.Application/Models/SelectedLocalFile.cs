namespace GustaBackupManager.App.Models;

public sealed record SelectedLocalFile(
    string FullPath,
    string FileName,
    string Extension,
    long SizeBytes);
