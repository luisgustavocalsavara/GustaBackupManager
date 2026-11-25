using GustaBackupManager.Models.Entities;

namespace GustaBackupManager.Models.DTOs;

public class FolderTreeNode
{
    public string FullPath { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Prefixo consolidado a partir da hierarquia de pastas usando ShortName (ou Name quando vazio),
    /// ex.: "Vin_Estoc_S01_Encaixotador_CLP".
    /// </summary>
    public string NamingPrefix { get; set; } = string.Empty;

    public bool HasMetadataFile { get; set; }
    public FolderMetadata Metadata { get; set; } = new();
    public int FileCount { get; set; }
    public List<FolderTreeNode> Children { get; set; } = [];
}
