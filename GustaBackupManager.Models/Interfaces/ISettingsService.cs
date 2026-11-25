using GustaBackupManager.Models.Entities;

namespace GustaBackupManager.Models.Interfaces;

public interface ISettingsService
{
    AppSettings Current { get; }
    event Action? SettingsChanged;
    void Load();
    void Save();
    void UpdateDarkMode(bool isDarkMode);
    void UpdateWindowState(AppWindowState windowState);
    void UpdateSmbSettings(SmbConnectionSettings smbSettings);
}
