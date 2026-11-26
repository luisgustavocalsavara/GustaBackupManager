using GustaBackupManager.App.Models;
using GustaBackupManager.Models.DTOs;
using GustaBackupManager.Models.Entities;

namespace GustaBackupManager.App.Interfaces;

public interface IBackupRepositoryService
{
    Task<OperationResult<List<FolderTreeNode>>> GetFolderTreeAsync();
    Task<OperationResult<FolderMetadata>> GetFolderMetadataAsync(string folderPath);
    Task<OperationResult<string>> BuildFileNamePrefixAsync(string folderPath);
    Task<OperationResult<FolderTreeNode>> CreateFolderAsync(string? parentFolderPath, FolderEditRequest request);
    Task<OperationResult<FolderMetadata>> UpdateFolderAsync(string folderPath, FolderEditRequest request);
    Task<OperationResult> DeleteFolderAsync(string folderPath);

    Task<OperationResult<List<BackupFileItem>>> GetFilesInFolderAsync(string folderPath);
    Task<OperationResult<BackupFileItem>> UploadBackupFileAsync(BackupUploadRequest request);
    Task<OperationResult> DownloadBackupFileAsync(string remoteFilePath, string destinationLocalPath);
    Task<OperationResult> DeleteBackupFileAsync(string remoteFilePath);
    Task<OperationResult<int>> SyncFileTypePolicyAcrossFoldersAsync(int fileTypeId);
    Task<OperationResult<int>> SyncCategoryAppearanceAcrossFoldersAsync(
        FolderGroupType group,
        LocationSubCategory? locationCategory,
        ProjectCategory? projectCategory,
        DocumentationSubCategory? documentationCategory,
        string iconKey,
        string iconColor);
    Task<OperationResult> SeedExamplePlantStructureAsync();
}
