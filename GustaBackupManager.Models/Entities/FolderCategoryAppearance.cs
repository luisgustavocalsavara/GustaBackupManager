namespace GustaBackupManager.Models.Entities;

public class FolderCategoryAppearance
{
    public int Id { get; set; }

    /// <summary>
    /// Chave unica da categoria (ex.: "Fabrica", "Localizacao:Manufatura", "Projeto", "Equipamento").
    /// </summary>
    public string CategoryKey { get; set; } = string.Empty;

    public FolderGroupType Group { get; set; }

    /// <summary>
    /// Nome da subcategoria (ex.: "CLP", "IHM", "Diagrama Elétrico") ou vazio para grupos diretos.
    /// </summary>
    public string SubCategoryName { get; set; } = string.Empty;

    /// <summary>
    /// Nome de exibicao completo (ex.: "Projeto", "Fábrica").
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    public string IconKey { get; set; } = "Folder";
    public string IconColor { get; set; } = "Primary";
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
