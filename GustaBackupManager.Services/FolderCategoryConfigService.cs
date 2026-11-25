using GustaBackupManager.Models.Entities;
using GustaBackupManager.Models.Interfaces;
using GustaBackupManager.Services.Data;
using Microsoft.EntityFrameworkCore;

namespace GustaBackupManager.Services;

public class FolderCategoryConfigService(IDbContextFactory<AppDbContext> dbFactory) : IFolderCategoryConfigService
{
    public async Task<List<FolderCategoryAppearance>> GetAllAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.CategoryAppearances
            .AsNoTracking()
            .OrderBy(c => c.Id)
            .ToListAsync();
    }

    public async Task<(string IconKey, string IconColor)> ResolveAppearanceAsync(
        FolderGroupType group,
        LocationSubCategory? locationCategory = null,
        ComponentSubCategory? componentCategory = null,
        DocumentationSubCategory? documentationCategory = null)
    {
        var key = BuildCategoryKey(group, locationCategory, componentCategory, documentationCategory);

        await using var db = await dbFactory.CreateDbContextAsync();
        var item = await db.CategoryAppearances
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CategoryKey == key);

        if (item is not null && !string.IsNullOrWhiteSpace(item.IconKey))
        {
            return (item.IconKey, string.IsNullOrWhiteSpace(item.IconColor) ? "Default" : item.IconColor);
        }

        return GetDefaultSuggestion(group, locationCategory, componentCategory, documentationCategory);
    }

    public async Task<(bool Success, string Message)> SaveAsync(int id, string iconKey, string iconColor)
    {
        if (string.IsNullOrWhiteSpace(iconKey))
            return (false, "Selecione um ícone válido para a categoria.");

        await using var db = await dbFactory.CreateDbContextAsync();
        var entity = await db.CategoryAppearances.FindAsync(id);
        if (entity is null)
            return (false, "Categoria não encontrada.");

        entity.IconKey = iconKey.Trim();
        entity.IconColor = string.IsNullOrWhiteSpace(iconColor) ? "Primary" : iconColor.Trim();
        entity.UpdatedAt = DateTime.Now;

        await db.SaveChangesAsync();
        return (true, $"Ícone e cor de '{entity.DisplayName}' atualizados com sucesso.");
    }

    public async Task<(bool Success, string Message)> ResetToDefaultAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var entity = await db.CategoryAppearances.FindAsync(id);
        if (entity is null)
            return (false, "Categoria não encontrada.");

        var defaults = BuildDefaultDefinitions().FirstOrDefault(d => d.CategoryKey == entity.CategoryKey);
        if (defaults is null)
            return (false, "Sugestão padrão não encontrada para esta categoria.");

        entity.IconKey = defaults.IconKey;
        entity.IconColor = defaults.IconColor;
        entity.UpdatedAt = DateTime.Now;

        await db.SaveChangesAsync();
        return (true, $"Sugestão padrão restaurada para '{entity.DisplayName}'.");
    }

    public static string BuildCategoryKey(
        FolderGroupType group,
        LocationSubCategory? locationCategory = null,
        ComponentSubCategory? componentCategory = null,
        DocumentationSubCategory? documentationCategory = null) =>
        group switch
        {
            FolderGroupType.Localizacao => $"Localizacao:{locationCategory ?? LocationSubCategory.Manufatura}",
            FolderGroupType.Componente => $"Componente:{componentCategory ?? ComponentSubCategory.Clp}",
            FolderGroupType.Projeto => "Projeto",
            FolderGroupType.Documentacao => $"Documentacao:{documentationCategory ?? DocumentationSubCategory.ManualTecnico}",
            _ => group.ToString()
        };

    public static (string IconKey, string IconColor) GetDefaultSuggestion(
        FolderGroupType group,
        LocationSubCategory? locationCategory = null,
        ComponentSubCategory? componentCategory = null,
        DocumentationSubCategory? documentationCategory = null)
    {
        var key = BuildCategoryKey(group, locationCategory, componentCategory, documentationCategory);
        var match = BuildDefaultDefinitions().FirstOrDefault(d => d.CategoryKey == key);
        return match is not null
            ? (match.IconKey, match.IconColor)
            : ("Folder", "Default");
    }

    public static List<FolderCategoryAppearance> BuildDefaultDefinitions()
    {
        var now = DateTime.Now;
        return
        [
            // 1. Fábrica (Raiz)
            new()
            {
                CategoryKey = "Fabrica",
                Group = FolderGroupType.Fabrica,
                SubCategoryName = string.Empty,
                DisplayName = "Fábrica (Raiz)",
                IconKey = "Domain",
                IconColor = "Primary",
                UpdatedAt = now
            },

            // 2. Localização (9 categorias)
            new()
            {
                CategoryKey = "Localizacao:Manufatura",
                Group = FolderGroupType.Localizacao,
                SubCategoryName = "Manufatura",
                DisplayName = "Localização - Manufatura",
                IconKey = "Factory",
                IconColor = "Primary",
                UpdatedAt = now
            },
            new()
            {
                CategoryKey = "Localizacao:Processo",
                Group = FolderGroupType.Localizacao,
                SubCategoryName = "Processo",
                DisplayName = "Localização - Processo",
                IconKey = "LinearScale",
                IconColor = "Primary",
                UpdatedAt = now
            },
            new()
            {
                CategoryKey = "Localizacao:Paletizacao",
                Group = FolderGroupType.Localizacao,
                SubCategoryName = "Paletização",
                DisplayName = "Localização - Paletização",
                IconKey = "PrecisionManufacturing",
                IconColor = "Info",
                UpdatedAt = now
            },
            new()
            {
                CategoryKey = "Localizacao:Utilidades",
                Group = FolderGroupType.Localizacao,
                SubCategoryName = "Utilidades",
                DisplayName = "Localização - Utilidades",
                IconKey = "Build",
                IconColor = "Info",
                UpdatedAt = now
            },
            new()
            {
                CategoryKey = "Localizacao:Estocagem",
                Group = FolderGroupType.Localizacao,
                SubCategoryName = "Estocagem",
                DisplayName = "Localização - Estocagem",
                IconKey = "Warehouse",
                IconColor = "Primary",
                UpdatedAt = now
            },
            new()
            {
                CategoryKey = "Localizacao:Logistica",
                Group = FolderGroupType.Localizacao,
                SubCategoryName = "Logística",
                DisplayName = "Localização - Logística",
                IconKey = "LocalShipping",
                IconColor = "Info",
                UpdatedAt = now
            },
            new()
            {
                CategoryKey = "Localizacao:AreaExterna",
                Group = FolderGroupType.Localizacao,
                SubCategoryName = "Área Externa",
                DisplayName = "Localização - Área Externa",
                IconKey = "Landscape",
                IconColor = "Success",
                UpdatedAt = now
            },
            new()
            {
                CategoryKey = "Localizacao:SubestacaoCabineEntrada",
                Group = FolderGroupType.Localizacao,
                SubCategoryName = "Substação/Cabine Entrada",
                DisplayName = "Localização - Substação/Cabine Entrada",
                IconKey = "Bolt",
                IconColor = "Warning",
                UpdatedAt = now
            },
            new()
            {
                CategoryKey = "Localizacao:LinhaProducao",
                Group = FolderGroupType.Localizacao,
                SubCategoryName = "Linha de Produção",
                DisplayName = "Localização - Linha de Produção",
                IconKey = "AccountTree",
                IconColor = "Info",
                UpdatedAt = now
            },

            // 3. Equipamento (criação direta)
            new()
            {
                CategoryKey = "Equipamento",
                Group = FolderGroupType.Equipamento,
                SubCategoryName = string.Empty,
                DisplayName = "Equipamento",
                IconKey = "PrecisionManufacturing",
                IconColor = "Warning",
                UpdatedAt = now
            },

            // 4. Componente (10 categorias)
            new()
            {
                CategoryKey = "Componente:DispositivoDedicado",
                Group = FolderGroupType.Componente,
                SubCategoryName = "Dispositivo Dedicado",
                DisplayName = "Componente - Dispositivo Dedicado",
                IconKey = "DeveloperBoard",
                IconColor = "Success",
                UpdatedAt = now
            },
            new()
            {
                CategoryKey = "Componente:Clp",
                Group = FolderGroupType.Componente,
                SubCategoryName = "CLP",
                DisplayName = "Componente - CLP",
                IconKey = "Memory",
                IconColor = "Success",
                UpdatedAt = now
            },
            new()
            {
                CategoryKey = "Componente:Ihm",
                Group = FolderGroupType.Componente,
                SubCategoryName = "IHM",
                DisplayName = "Componente - IHM",
                IconKey = "DesktopWindows",
                IconColor = "Secondary",
                UpdatedAt = now
            },
            new()
            {
                CategoryKey = "Componente:Inversor",
                Group = FolderGroupType.Componente,
                SubCategoryName = "Inversor",
                DisplayName = "Componente - Inversor",
                IconKey = "Speed",
                IconColor = "Warning",
                UpdatedAt = now
            },
            new()
            {
                CategoryKey = "Componente:Motion",
                Group = FolderGroupType.Componente,
                SubCategoryName = "Motion",
                DisplayName = "Componente - Motion",
                IconKey = "Tune",
                IconColor = "Info",
                UpdatedAt = now
            },
            new()
            {
                CategoryKey = "Componente:Instrumento",
                Group = FolderGroupType.Componente,
                SubCategoryName = "Instrumento",
                DisplayName = "Componente - Instrumento",
                IconKey = "Sensors",
                IconColor = "Info",
                UpdatedAt = now
            },
            new()
            {
                CategoryKey = "Componente:Safe",
                Group = FolderGroupType.Componente,
                SubCategoryName = "Safe",
                DisplayName = "Componente - Safe",
                IconKey = "Shield",
                IconColor = "Error",
                UpdatedAt = now
            },
            new()
            {
                CategoryKey = "Componente:Rede",
                Group = FolderGroupType.Componente,
                SubCategoryName = "Rede",
                DisplayName = "Componente - Rede",
                IconKey = "Hub",
                IconColor = "Info",
                UpdatedAt = now
            },
            new()
            {
                CategoryKey = "Componente:Switch",
                Group = FolderGroupType.Componente,
                SubCategoryName = "Switch",
                DisplayName = "Componente - Switch",
                IconKey = "Router",
                IconColor = "Info",
                UpdatedAt = now
            },
            new()
            {
                CategoryKey = "Componente:Robo",
                Group = FolderGroupType.Componente,
                SubCategoryName = "Robô",
                DisplayName = "Componente - Robô",
                IconKey = "PrecisionManufacturing",
                IconColor = "Error",
                UpdatedAt = now
            },

            // 5. Documentação (8 categorias)
            new()
            {
                CategoryKey = "Documentacao:DocumentacaoDiversas",
                Group = FolderGroupType.Documentacao,
                SubCategoryName = "Documentação Diversas",
                DisplayName = "Documentação - Documentação Diversas",
                IconKey = "Description",
                IconColor = "Info",
                UpdatedAt = now
            },
            new()
            {
                CategoryKey = "Documentacao:DiagramaEletrico",
                Group = FolderGroupType.Documentacao,
                SubCategoryName = "Diagrama Elétrico",
                DisplayName = "Documentação - Diagrama Elétrico",
                IconKey = "Architecture",
                IconColor = "Info",
                UpdatedAt = now
            },
            new()
            {
                CategoryKey = "Documentacao:DiagramaPneumatico",
                Group = FolderGroupType.Documentacao,
                SubCategoryName = "Diagrama Pneumático",
                DisplayName = "Documentação - Diagrama Pneumático",
                IconKey = "Architecture",
                IconColor = "Primary",
                UpdatedAt = now
            },
            new()
            {
                CategoryKey = "Documentacao:ManualOperacoes",
                Group = FolderGroupType.Documentacao,
                SubCategoryName = "Manual de Operações",
                DisplayName = "Documentação - Manual de Operações",
                IconKey = "MenuBook",
                IconColor = "Info",
                UpdatedAt = now
            },
            new()
            {
                CategoryKey = "Documentacao:ManualTecnico",
                Group = FolderGroupType.Documentacao,
                SubCategoryName = "Manual Técnico",
                DisplayName = "Documentação - Manual Técnico",
                IconKey = "MenuBook",
                IconColor = "Primary",
                UpdatedAt = now
            },
            new()
            {
                CategoryKey = "Documentacao:FolhetimInformacoes",
                Group = FolderGroupType.Documentacao,
                SubCategoryName = "Folhetim de Informações",
                DisplayName = "Documentação - Folhetim de Informações",
                IconKey = "Description",
                IconColor = "Secondary",
                UpdatedAt = now
            },
            new()
            {
                CategoryKey = "Documentacao:ListaSpareParts",
                Group = FolderGroupType.Documentacao,
                SubCategoryName = "Lista SpareParts",
                DisplayName = "Documentação - Lista SpareParts",
                IconKey = "Inventory",
                IconColor = "Warning",
                UpdatedAt = now
            },
            new()
            {
                CategoryKey = "Documentacao:Midias",
                Group = FolderGroupType.Documentacao,
                SubCategoryName = "Mídias",
                DisplayName = "Documentação - Mídias",
                IconKey = "PermMedia",
                IconColor = "Secondary",
                UpdatedAt = now
            },

            // Projeto (catalogo de ambientes de engenharia)
            new()
            {
                CategoryKey = "Projeto",
                Group = FolderGroupType.Projeto,
                SubCategoryName = string.Empty,
                DisplayName = "Projeto",
                IconKey = "Memory",
                IconColor = "Info",
                UpdatedAt = now
            }
        ];
    }
}
