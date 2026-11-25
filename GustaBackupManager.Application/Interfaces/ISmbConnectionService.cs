using GustaBackupManager.App.Models;
using GustaBackupManager.Models.Entities;

namespace GustaBackupManager.App.Interfaces;

public interface ISmbConnectionService
{
    SmbConnectionStatus LastStatus { get; }
    event Action? StatusChanged;

    string ProtectSecret(string plainText);
    string UnprotectSecret(string protectedBase64);

    Task<SmbConnectionStatus> ConnectAndVerifyAsync(SmbConnectionSettings settings, string? plainPasswordOverride = null);
    Task<SmbConnectionStatus> EnsureConnectedAsync();
    Task<OperationResult> DisconnectAsync(string? rootPath = null);
}
