using System.Text.RegularExpressions;
using GustaBackupManager.Models.Entities;
using GustaBackupManager.Models.Interfaces;
using GustaBackupManager.Services.Data;
using Microsoft.EntityFrameworkCore;

namespace GustaBackupManager.Services;

public partial class FileTypeService(IDbContextFactory<AppDbContext> dbFactory) : IFileTypeService
{
    [GeneratedRegex(@"^\.[a-z0-9][a-z0-9_-]{0,15}$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ValidExtensionRegex();

    public async Task<List<FileTypeDefinition>> GetAllAsync(bool onlyActive = false)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var query = db.FileTypes.AsNoTracking();
        if (onlyActive)
            query = query.Where(f => f.IsActive);

        return await query
            .OrderBy(f => f.Name)
            .ToListAsync();
    }

    public async Task<(bool Success, string Message, int SavedId)> SaveAsync(FileTypeDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.Name))
            return (false, "Informe o nome do tipo de arquivo.", 0);

        var normalizedExts = definition.GetNormalizedExtensions();
        if (normalizedExts.Count == 0)
            return (false, "Informe ao menos uma extensão válida (ex.: .acd; .mer; .pdf).", 0);

        var invalidExt = normalizedExts.FirstOrDefault(ext => !ValidExtensionRegex().IsMatch(ext));
        if (invalidExt is not null)
            return (false, $"Extensão inválida: '{invalidExt}'. Use apenas letras, números, '_' ou '-' (ex.: .acd, .mer, .zap19).", 0);

        var formattedExtensions = string.Join(";", normalizedExts);
        var trimmedName = definition.Name.Trim();

        await using var db = await dbFactory.CreateDbContextAsync();
        var duplicate = await db.FileTypes.AnyAsync(f =>
            f.Id != definition.Id && f.Name.ToLower() == trimmedName.ToLower());

        if (duplicate)
            return (false, "Já existe um tipo de arquivo cadastrado com este nome.", 0);

        int savedId;
        if (definition.Id == 0)
        {
            var entity = new FileTypeDefinition
            {
                Name = trimmedName,
                Extensions = formattedExtensions,
                Description = definition.Description?.Trim() ?? string.Empty,
                IconKey = string.IsNullOrWhiteSpace(definition.IconKey) ? "Description" : definition.IconKey.Trim(),
                IsActive = definition.IsActive,
                CreatedAt = DateTime.Now
            };
            db.FileTypes.Add(entity);
            await db.SaveChangesAsync();
            savedId = entity.Id;
        }
        else
        {
            var existing = await db.FileTypes.FindAsync(definition.Id);
            if (existing is null)
                return (false, "Tipo de arquivo não encontrado.", 0);

            existing.Name = trimmedName;
            existing.Extensions = formattedExtensions;
            existing.Description = definition.Description?.Trim() ?? string.Empty;
            existing.IconKey = string.IsNullOrWhiteSpace(definition.IconKey) ? "Description" : definition.IconKey.Trim();
            existing.IsActive = definition.IsActive;
            await db.SaveChangesAsync();
            savedId = existing.Id;
        }

        return (true, "Tipo de arquivo salvo com sucesso.", savedId);
    }

    public async Task<(bool Success, string Message)> ToggleActiveAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await db.FileTypes.FindAsync(id);
        if (existing is null)
            return (false, "Tipo de arquivo não encontrado.");

        existing.IsActive = !existing.IsActive;
        await db.SaveChangesAsync();
        return (true, existing.IsActive
            ? $"Tipo '{existing.Name}' ativado para pastas de repositório."
            : $"Tipo '{existing.Name}' inativado para novas pastas.");
    }

    public async Task<(bool Success, string Message)> DeleteAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await db.FileTypes.FindAsync(id);
        if (existing is null)
            return (false, "Tipo de arquivo não encontrado.");

        db.FileTypes.Remove(existing);
        await db.SaveChangesAsync();
        return (true, "Tipo de arquivo excluído com sucesso.");
    }
}
