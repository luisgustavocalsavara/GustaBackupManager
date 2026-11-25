using GustaBackupManager.Models.Entities;

namespace GustaBackupManager.Models.Interfaces;

public interface IFolderCategoryConfigService
{
    Task<List<FolderCategoryAppearance>> GetAllAsync();

    Task<(string IconKey, string IconColor)> ResolveAppearanceAsync(
        FolderGroupType group,
        LocationSubCategory? locationCategory = null,
        ComponentSubCategory? componentCategory = null,
        DocumentationSubCategory? documentationCategory = null);

    Task<(bool Success, string Message)> SaveAsync(int id, string iconKey, string iconColor);

    Task<(bool Success, string Message)> ResetToDefaultAsync(int id);
}
