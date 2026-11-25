namespace GustaBackupManager.Models.Entities;

public class FileTypeDefinition
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Extensoes separadas por ponto e virgula, com ponto inicial (ex.: ".acd;.l5x").
    /// </summary>
    public string Extensions { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
    public string IconKey { get; set; } = "Description";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public List<string> GetNormalizedExtensions()
    {
        if (string.IsNullOrWhiteSpace(Extensions))
            return [];

        return Extensions
            .Split([';', ',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ext => ext.StartsWith('.') ? ext.ToLowerInvariant() : "." + ext.ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(ext => ext, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
