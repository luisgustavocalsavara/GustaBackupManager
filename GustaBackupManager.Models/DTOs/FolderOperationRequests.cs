using GustaBackupManager.Models.Entities;

namespace GustaBackupManager.Models.DTOs;

public class FolderEditRequest
{
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public FolderGroupType Group { get; set; } = FolderGroupType.Localizacao;
    public LocationSubCategory? LocationCategory { get; set; } = LocationSubCategory.Manufatura;
    public DocumentationSubCategory? DocumentationCategory { get; set; } = DocumentationSubCategory.ManualTecnico;

    public string IconKey { get; set; } = "Factory";
    public string IconColor { get; set; } = "Primary";

    // Informacoes especificas por tipo de pasta
    public FactoryMetadataDetails FactoryInfo { get; set; } = new();
    public EquipmentMetadataDetails EquipmentInfo { get; set; } = new();
    public ProjectMetadataDetails ProjectInfo { get; set; } = new();

    public List<int> AllowedFileTypeIds { get; set; } = [];
}

public class BackupUploadRequest
{
    public string TargetFolderPath { get; set; } = string.Empty;
    public string SourceLocalFilePath { get; set; } = string.Empty;
    public DateTime? CustomFileTimestamp { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool IsRoutineBackup { get; set; }
}
