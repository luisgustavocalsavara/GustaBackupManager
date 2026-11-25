using GustaBackupManager.Models.Entities;

namespace GustaBackupManager.Models.Interfaces;

public interface IFileTypeService
{
    Task<List<FileTypeDefinition>> GetAllAsync(bool onlyActive = false);
    Task<(bool Success, string Message, int SavedId)> SaveAsync(FileTypeDefinition definition);
    Task<(bool Success, string Message)> ToggleActiveAsync(int id);
    Task<(bool Success, string Message)> DeleteAsync(int id);
}
