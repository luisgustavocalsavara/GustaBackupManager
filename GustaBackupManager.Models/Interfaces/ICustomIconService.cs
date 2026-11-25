using GustaBackupManager.Models.Entities;

namespace GustaBackupManager.Models.Interfaces;

public interface ICustomIconService
{
    Task<List<CustomIconDefinition>> GetAllAsync();
    Task<(bool Success, string Message, string SvgMarkup)> BuildPreviewFromFileAsync(string localFilePath);
    (bool Success, string Message, string SvgMarkup) BuildPreviewFromSvgCode(string? rawSvg);
    Task<(bool Success, string Message, CustomIconDefinition? Created)> AddFromFileAsync(string label, string localFilePath);
    Task<(bool Success, string Message, CustomIconDefinition? Created)> AddFromSvgCodeAsync(string label, string svgCode);
    Task<(bool Success, string Message)> DeleteAsync(int id);
}
