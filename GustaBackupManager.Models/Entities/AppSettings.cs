namespace GustaBackupManager.Models.Entities;

public class AppSettings
{
    public bool IsDarkMode { get; set; } = true;
    public AppWindowState WindowState { get; set; } = AppWindowState.Normal;
    public SmbConnectionSettings Smb { get; set; } = new();
}

public class SmbConnectionSettings
{
    /// <summary>
    /// Caminho UNC do compartilhamento SMB (ex.: \\192.168.1.50\BackupsAutomacao)
    /// ou caminho local para testes/desenvolvimento (ex.: C:\BackupsAutomacao).
    /// </summary>
    public string RootPath { get; set; } = string.Empty;

    public string Domain { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Senha protegida via Windows DPAPI (CurrentUser) em Base64.
    /// </summary>
    public string ProtectedPassword { get; set; } = string.Empty;

    public bool UseIntegratedAuth { get; set; } = true;
}

public enum AppWindowState
{
    Normal,
    Minimized,
    Maximized
}
