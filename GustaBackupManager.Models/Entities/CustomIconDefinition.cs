namespace GustaBackupManager.Models.Entities;

public class CustomIconDefinition
{
    public int Id { get; set; }

    /// <summary>
    /// Chave unica do icone (ex.: "Custom_20261124134500_a1b2c3").
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Nome de exibicao do icone cadastrado pelo usuario.
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Marcacao SVG interna (ex.: &lt;path .../&gt;, &lt;svg ...&gt; ou &lt;image href="data:..." /&gt;)
    /// compativel com MudIcon e MudTreeViewItem.
    /// </summary>
    public string SvgMarkup { get; set; } = string.Empty;

    /// <summary>
    /// Origem do icone ("Arquivo" ou "SVG").
    /// </summary>
    public string SourceType { get; set; } = "Arquivo";

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
