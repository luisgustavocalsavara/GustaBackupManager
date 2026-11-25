using GustaBackupManager.App.Models;

namespace GustaBackupManager.App.Interfaces;

public interface INativeFileDialogService
{
    Task<SelectedLocalFile?> PickFileAsync(string title, IReadOnlyCollection<string> allowedExtensions);
    Task<string?> PickSaveLocationAsync(string title, string suggestedFileName, string extension);
    Task<string?> PickFolderAsync(string description, string? initialPath = null);
    OperationResult OpenPathInShell(string path);
}
