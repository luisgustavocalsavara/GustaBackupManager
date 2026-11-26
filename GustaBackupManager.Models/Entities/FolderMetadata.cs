using System.Text.Json.Serialization;

namespace GustaBackupManager.Models.Entities;

public enum FolderGroupType
{
    /// <summary>
    /// Pasta raiz do repositorio (pode haver mais de uma fabrica na raiz; proibida fora da raiz).
    /// </summary>
    Fabrica = 0,

    /// <summary>
    /// Agrupador de localizacao fisica/funcional (exige escolha de uma subcategoria de Localizacao).
    /// </summary>
    Localizacao = 1,

    /// <summary>
    /// Equipamento ou maquina (criada diretamente, sem subcategoria).
    /// </summary>
    Equipamento = 2,

    /// <summary>
    /// Agrupador de componente de automacao (exige subcategoria; permite armazenar arquivos).
    /// </summary>
    Componente = 4,

    /// <summary>
    /// Agrupador de documentacao tecnica (exige subcategoria; permite armazenar arquivos).
    /// </summary>
    Documentacao = 5,

    /// <summary>
    /// Valor legado mantido para interpretar metadados existentes; não é oferecido para novas pastas.
    /// </summary>
    Diversas = 6,
    /// <summary>Projeto de software/engenharia associado à pasta e suas extensões.</summary>
    Projeto = 7
}

public enum LocationSubCategory
{
    Manufatura = 0,
    Processo = 1,
    Paletizacao = 2,
    Utilidades = 3,
    Estocagem = 4,
    Logistica = 5,
    AreaExterna = 6,
    SubestacaoCabineEntrada = 7,
    LinhaProducao = 8
}

public enum ComponentSubCategory
{
    DispositivoDedicado = 0,
    Clp = 1,
    Ihm = 2,
    Inversor = 3,
    Motion = 4,
    Instrumento = 5,
    Safe = 6,
    Rede = 7,
    Switch = 8,
    Robo = 9
}

public enum ProjectCategory
{
    Clp = 0,
    Ihm = 1,
    Rede = 2,
    Motion = 3,
    Robot = 4,
    Safety = 5,
    Drives = 6,
    Outros = 7
}

public static class ProjectCategoryExtensions
{
    public static string GetLabel(this ProjectCategory category) => category switch
    {
        ProjectCategory.Clp => "Projeto CLP",
        ProjectCategory.Ihm => "Projeto IHM",
        ProjectCategory.Rede => "Projeto Rede",
        ProjectCategory.Motion => "Projeto Motion",
        ProjectCategory.Robot => "Projeto Robô",
        ProjectCategory.Safety => "Projeto Safety",
        ProjectCategory.Drives => "Projeto Drives",
        _ => "Projeto Outros"
    };

    public static string GetName(this ProjectCategory category) => category switch
    {
        ProjectCategory.Clp => "CLP",
        ProjectCategory.Ihm => "IHM",
        ProjectCategory.Rede => "Rede",
        ProjectCategory.Motion => "Motion",
        ProjectCategory.Robot => "Robô",
        ProjectCategory.Safety => "Safety",
        ProjectCategory.Drives => "Drives",
        _ => "Outros"
    };

    }

    public enum DocumentationSubCategory
{
    DocumentacaoDiversas = 0,
    DiagramaEletrico = 1,
    DiagramaPneumatico = 2,
    ManualOperacoes = 3,
    ManualTecnico = 4,
    FolhetimInformacoes = 5,
    ListaSpareParts = 6,
    Midias = 7
}

public class FactoryMetadataDetails
{
    public string RazaoSocial { get; set; } = string.Empty;
    public string Cnpj { get; set; } = string.Empty;
    public string Endereco { get; set; } = string.Empty;
    public string Informacoes { get; set; } = string.Empty;
}

public class EquipmentMetadataDetails
{
    public string Fabricante { get; set; } = string.Empty;
    public string Modelo { get; set; } = string.Empty;
    public string AnoFabricacao { get; set; } = string.Empty;
    public string NumeroSerie { get; set; } = string.Empty;
}

public class ProjectMetadataDetails
{
    public int? FileTypeId { get; set; }

    public ProjectCategory? Category { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string AllowedExtensions { get; set; } = string.Empty;
}

public class ComponentMetadataDetails
{
    public string Observations { get; set; } = string.Empty;
}

/// <summary>
/// Estrutura serializada no arquivo "folder.meta.json" presente em cada pasta no servidor SMB.
/// </summary>
public class FolderMetadata
{
    public const string MetadataFileName = "folder.meta.json";

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Nome resumido opcional usado para compor o nome padronizado dos arquivos armazenados
    /// (ex.: "Vin", "Estoc", "S01", "CLP"). Se vazio, utiliza o nome inteiro da pasta.
    /// </summary>
    public string ShortName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public FolderGroupType Group { get; set; } = FolderGroupType.Localizacao;
    public LocationSubCategory? LocationCategory { get; set; }
    public ComponentSubCategory? ComponentCategory { get; set; }
    public DocumentationSubCategory? DocumentationCategory { get; set; }

    public string IconKey { get; set; } = "Factory";
    public string IconColor { get; set; } = "Primary";

    // Informacoes especificas conforme o tipo da pasta
    public FactoryMetadataDetails? FactoryInfo { get; set; }
    public EquipmentMetadataDetails? EquipmentInfo { get; set; }
    public ComponentMetadataDetails? ComponentInfo { get; set; }
    public ProjectMetadataDetails? ProjectInfo { get; set; }

    /// <summary>
    /// Somente pastas das categorias "Componente" ou "Documentacao" podem armazenar arquivos.
    /// </summary>
    [JsonIgnore]
    public bool AllowFileStorage => Group is FolderGroupType.Componente or FolderGroupType.Projeto or FolderGroupType.Documentacao;

    [JsonIgnore]
    public List<int> AllowedFileTypeIds { get; set; } = [];

    [JsonIgnore]
    public List<string> AllowedExtensions { get; set; } = [];

    [JsonPropertyName(nameof(AllowedFileTypeIds))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<int>? SerializedAllowedFileTypeIds
    {
        get => AllowFileStorage ? AllowedFileTypeIds : null;
        set => AllowedFileTypeIds = value ?? [];
    }

    [JsonPropertyName(nameof(AllowedExtensions))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? SerializedAllowedExtensions
    {
        get => AllowFileStorage ? AllowedExtensions : null;
        set => AllowedExtensions = value ?? [];
    }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime ModifiedAt { get; set; } = DateTime.Now;
    public string CreatedBy { get; set; } = string.Empty;
    public string ModifiedBy { get; set; } = string.Empty;

    [JsonIgnore]
    public List<BackupFileMetadataEntry> Files { get; set; } = [];

    [JsonPropertyName(nameof(Files))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<BackupFileMetadataEntry>? SerializedFiles
    {
        get => AllowFileStorage ? Files : null;
        set => Files = value ?? [];
    }
}

public class BackupFileMetadataEntry
{
    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string OriginalFullPath { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsRoutineBackup { get; set; }
    public string FileTypeName { get; set; } = string.Empty;
    public string UploadedBy { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public DateTime ArchivedAt { get; set; }
    public DateTime OriginalModifiedAt { get; set; }
    public long SizeBytes { get; set; }
}
