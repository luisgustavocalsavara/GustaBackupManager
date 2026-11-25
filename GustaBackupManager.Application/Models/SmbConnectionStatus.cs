namespace GustaBackupManager.App.Models;

public sealed record SmbConnectionStatus(
    bool IsConnected,
    bool IsUncPath,
    string RootPath,
    string Message,
    int? Win32ErrorCode = null);
