using System.Collections.Concurrent;
using GustaBackupManager.Models.Entities;
using MudBlazor;

namespace GustaBackupManager.Views.Helpers;

public sealed record IconOption(string Key, string Label, string Svg, bool IsCustom = false, int? CustomId = null);

public sealed record ColorOption(string Key, string Label, Color MudColor);

public static class FolderIconCatalog
{
    private static readonly ConcurrentDictionary<string, IconOption> CustomIconsByKey = new(StringComparer.OrdinalIgnoreCase);

    public static readonly IReadOnlyList<IconOption> BuiltInIcons =
    [
        new("Domain", "Fábrica / Planta", Icons.Material.Filled.Domain),
        new("Factory", "Indústria / Área", Icons.Material.Filled.Factory),
        new("Apartment", "Prédio / Galpão", Icons.Material.Filled.Apartment),
        new("AccountTree", "Linha / Hierarquia", Icons.Material.Filled.AccountTree),
        new("LinearScale", "Processo / Fluxo", Icons.Material.Filled.LinearScale),
        new("Warehouse", "Estocagem / Armazém", Icons.Material.Filled.Warehouse),
        new("LocalShipping", "Logística / Transporte", Icons.Material.Filled.LocalShipping),
        new("Landscape", "Área Externa", Icons.Material.Filled.Landscape),
        new("Bolt", "Energia / Substação", Icons.Material.Filled.Bolt),
        new("ElectricBolt", "Alta Tensão", Icons.Material.Filled.ElectricBolt),
        new("Power", "Alimentação / Tomada", Icons.Material.Filled.Power),
        new("ElectricalServices", "Painel Elétrico", Icons.Material.Filled.ElectricalServices),
        new("PrecisionManufacturing", "Equipamento / Robô", Icons.Material.Filled.PrecisionManufacturing),
        new("Settings", "Máquina / Engrenagem", Icons.Material.Filled.Settings),
        new("Build", "Utilidades / Ferramenta", Icons.Material.Filled.Build),
        new("Engineering", "Engenharia", Icons.Material.Filled.Engineering),
        new("Handyman", "Manutenção", Icons.Material.Filled.Handyman),
        new("Memory", "CLP / Processador", Icons.Material.Filled.Memory),
        new("DeveloperBoard", "Placa / Módulo I/O", Icons.Material.Filled.DeveloperBoard),
        new("Dns", "Servidor / Rack", Icons.Material.Filled.Dns),
        new("DesktopWindows", "IHM / Tela", Icons.Material.Filled.DesktopWindows),
        new("TouchApp", "Painel Touch", Icons.Material.Filled.TouchApp),
        new("DisplaySettings", "Config. Display", Icons.Material.Filled.DisplaySettings),
        new("Speed", "Inversor / Drive", Icons.Material.Filled.Speed),
        new("Tune", "Motion / Servo", Icons.Material.Filled.Tune),
        new("Sensors", "Sensor / Instrumento", Icons.Material.Filled.Sensors),
        new("Thermostat", "Temperatura", Icons.Material.Filled.Thermostat),
        new("WaterDrop", "Fluido / Hidráulica", Icons.Material.Filled.WaterDrop),
        new("Air", "Pneumática / Ar", Icons.Material.Filled.Air),
        new("Plumbing", "Válvula / Tubulação", Icons.Material.Filled.Plumbing),
        new("Shield", "Segurança / Safe", Icons.Material.Filled.Shield),
        new("VerifiedUser", "Safety Certificado", Icons.Material.Filled.VerifiedUser),
        new("Router", "Switch / Roteador", Icons.Material.Filled.Router),
        new("Hub", "Rede Industrial", Icons.Material.Filled.Hub),
        new("Lan", "Ethernet / Fieldbus", Icons.Material.Filled.Lan),
        new("Cable", "Cabeamento", Icons.Material.Filled.Cable),
        new("MenuBook", "Manual Técnico", Icons.Material.Filled.MenuBook),
        new("Architecture", "Diagrama / CAD", Icons.Material.Filled.Architecture),
        new("Description", "Documento / PDF", Icons.Material.Filled.Description),
        new("Inventory", "Lista SpareParts", Icons.Material.Filled.Inventory),
        new("PermMedia", "Mídias / Imagens", Icons.Material.Filled.PermMedia),
        new("Folder", "Pasta Padrão", Icons.Material.Filled.Folder),
        new("FolderSpecial", "Pasta Especial", Icons.Material.Filled.FolderSpecial),
        new("Storage", "Repositório / Backup", Icons.Material.Filled.Storage),
        new("SdStorage", "Cartão Memória", Icons.Material.Filled.SdStorage),
    ];

    public static IReadOnlyList<IconOption> AllIcons =>
        [.. CustomIconsByKey.Values.OrderByDescending(c => c.CustomId), .. BuiltInIcons];

    public static void SetCustomIcons(IEnumerable<CustomIconDefinition> customIcons)
    {
        CustomIconsByKey.Clear();
        foreach (var item in customIcons)
        {
            if (string.IsNullOrWhiteSpace(item.Key) || string.IsNullOrWhiteSpace(item.SvgMarkup))
                continue;

            CustomIconsByKey[item.Key] = new IconOption(
                item.Key,
                item.Label,
                item.SvgMarkup,
                IsCustom: true,
                CustomId: item.Id);
        }
    }

    public static readonly IReadOnlyList<ColorOption> ColorsList =
    [
        new("Primary", "Roxo (Primária)", Color.Primary),
        new("Info", "Azul (Info)", Color.Info),
        new("Success", "Verde (Sucesso)", Color.Success),
        new("Warning", "Amarelo (Alerta)", Color.Warning),
        new("Secondary", "Rosa/Destaque", Color.Secondary),
        new("Error", "Vermelho", Color.Error),
        new("Default", "Padrão", Color.Default)
    ];

    public static string ResolveIcon(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return Icons.Material.Filled.Folder;

        if (CustomIconsByKey.TryGetValue(key, out var custom))
            return custom.Svg;

        var found = BuiltInIcons.FirstOrDefault(i =>
            string.Equals(i.Key, key, StringComparison.OrdinalIgnoreCase));

        return found?.Svg ?? Icons.Material.Filled.Folder;
    }

    public static Color ResolveColor(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return Color.Default;

        var found = ColorsList.FirstOrDefault(c =>
            string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase));

        return found?.MudColor ?? Color.Default;
    }

    public static string GetGroupLabel(FolderGroupType group) =>
        group switch
        {
            FolderGroupType.Fabrica => "Fábrica",
            FolderGroupType.Localizacao => "Localização",
            FolderGroupType.Equipamento => "Equipamento",
            FolderGroupType.Projeto => "Projeto",
            FolderGroupType.Documentacao => "Documentação",
            FolderGroupType.Diversas => "Pasta legada",
            _ => group.ToString()
        };

    public static string GetLocationCategoryLabel(LocationSubCategory category) =>
        category switch
        {
            LocationSubCategory.Manufatura => "Manufatura",
            LocationSubCategory.Processo => "Processo",
            LocationSubCategory.Paletizacao => "Paletização",
            LocationSubCategory.Utilidades => "Utilidades",
            LocationSubCategory.Estocagem => "Estocagem",
            LocationSubCategory.Logistica => "Logística",
            LocationSubCategory.AreaExterna => "Área Externa",
            LocationSubCategory.SubestacaoCabineEntrada => "Substação/Cabine Entrada",
            LocationSubCategory.LinhaProducao => "Linha de Produção",
            _ => category.ToString()
        };

    public static string GetDocumentationCategoryLabel(DocumentationSubCategory category) =>
        category switch
        {
            DocumentationSubCategory.DocumentacaoDiversas => "Documentação Diversas",
            DocumentationSubCategory.DiagramaEletrico => "Diagrama Elétrico",
            DocumentationSubCategory.DiagramaPneumatico => "Diagrama Pneumático",
            DocumentationSubCategory.ManualOperacoes => "Manual de Operações",
            DocumentationSubCategory.ManualTecnico => "Manual Técnico",
            DocumentationSubCategory.FolhetimInformacoes => "Folhetim de Informações",
            DocumentationSubCategory.ListaSpareParts => "Lista SpareParts",
            DocumentationSubCategory.Midias => "Mídias",
            _ => category.ToString()
        };

    public static string? GetSubCategoryLabel(FolderMetadata metadata) =>
        metadata.Group switch
        {
            FolderGroupType.Localizacao when metadata.LocationCategory.HasValue
                => GetLocationCategoryLabel(metadata.LocationCategory.Value),
            FolderGroupType.Projeto when metadata.ProjectInfo?.Category.HasValue == true
                => metadata.ProjectInfo.Category.Value.GetLabel(),
            FolderGroupType.Documentacao when metadata.DocumentationCategory.HasValue
                => GetDocumentationCategoryLabel(metadata.DocumentationCategory.Value),
            _ => null
        };

}
