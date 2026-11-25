using System.Security;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using GustaBackupManager.Models.Entities;
using GustaBackupManager.Models.Interfaces;
using GustaBackupManager.Services.Data;
using Microsoft.EntityFrameworkCore;

namespace GustaBackupManager.Services;

public partial class CustomIconService(IDbContextFactory<AppDbContext> dbFactory) : ICustomIconService
{
    private const long MaxFileSizeBytes = 512 * 1024; // 512 KB

    private static readonly HashSet<string> AllowedSvgElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "svg", "g", "path", "rect", "circle", "ellipse", "line", "polyline", "polygon",
        "defs", "linearGradient", "radialGradient", "stop", "clipPath", "mask", "title", "desc", "image"
    };

    private static readonly HashSet<string> AllowedSvgAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "viewBox", "width", "height", "x", "y", "cx", "cy", "r", "rx", "ry", "d", "points",
        "fill", "stroke", "stroke-width", "stroke-linecap", "stroke-linejoin", "stroke-dasharray",
        "stroke-dashoffset", "opacity", "fill-opacity", "stroke-opacity", "fill-rule", "clip-rule",
        "transform", "id", "offset", "stop-color", "stop-opacity", "clip-path", "mask",
        "href", "preserveAspectRatio", "xmlns"
    };

    public async Task<List<CustomIconDefinition>> GetAllAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.CustomIcons
            .AsNoTracking()
            .OrderByDescending(i => i.Id)
            .ToListAsync();
    }

    public async Task<(bool Success, string Message, string SvgMarkup)> BuildPreviewFromFileAsync(string localFilePath)
    {
        if (string.IsNullOrWhiteSpace(localFilePath) || !File.Exists(localFilePath))
            return (false, "Selecione um arquivo de ícone válido.", string.Empty);

        var info = new FileInfo(localFilePath);
        if (info.Length == 0)
            return (false, "O arquivo selecionado está vazio.", string.Empty);

        if (info.Length > MaxFileSizeBytes)
            return (false, "O arquivo do ícone deve ter no máximo 512 KB.", string.Empty);

        var ext = info.Extension.ToLowerInvariant();
        if (ext == ".svg")
        {
            var rawSvg = await File.ReadAllTextAsync(localFilePath);
            return NormalizeSvgMarkup(rawSvg);
        }

        if (ext is ".png" or ".ico" or ".jpg" or ".jpeg" or ".webp")
        {
            var mime = ext switch
            {
                ".png" => "image/png",
                ".ico" => "image/x-icon",
                ".webp" => "image/webp",
                _ => "image/jpeg"
            };

            var bytes = await File.ReadAllBytesAsync(localFilePath);
            var b64 = Convert.ToBase64String(bytes);
            var dataUrl = SecurityElement.Escape($"data:{mime};base64,{b64}");
            var markup = $@"<image href=""{dataUrl}"" width=""24"" height=""24"" preserveAspectRatio=""xMidYMid meet"" />";
            return (true, string.Empty, markup);
        }

        return (false, "Formato não suportado. Utilize .svg, .ico, .png, .jpg ou .webp.", string.Empty);
    }

    public (bool Success, string Message, string SvgMarkup) BuildPreviewFromSvgCode(string? rawSvg) =>
        NormalizeSvgMarkup(rawSvg);

    public async Task<(bool Success, string Message, CustomIconDefinition? Created)> AddFromFileAsync(string label, string localFilePath)
    {
        if (string.IsNullOrWhiteSpace(label))
            return (false, "Informe um nome para o novo ícone.", null);

        var parsed = await BuildPreviewFromFileAsync(localFilePath);
        if (!parsed.Success)
            return (false, parsed.Message, null);

        return await SaveCustomIconAsync(label.Trim(), parsed.SvgMarkup, "Arquivo");
    }

    public async Task<(bool Success, string Message, CustomIconDefinition? Created)> AddFromSvgCodeAsync(string label, string svgCode)
    {
        if (string.IsNullOrWhiteSpace(label))
            return (false, "Informe um nome para o novo ícone.", null);

        var parsed = NormalizeSvgMarkup(svgCode);
        if (!parsed.Success)
            return (false, parsed.Message, null);

        return await SaveCustomIconAsync(label.Trim(), parsed.SvgMarkup, "SVG");
    }

    public async Task<(bool Success, string Message)> DeleteAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var entity = await db.CustomIcons.FindAsync(id);
        if (entity is null)
            return (false, "Ícone personalizado não encontrado.");

        db.CustomIcons.Remove(entity);
        await db.SaveChangesAsync();
        return (true, $"Ícone '{entity.Label}' removido.");
    }

    private async Task<(bool Success, string Message, CustomIconDefinition? Created)> SaveCustomIconAsync(
        string label,
        string svgMarkup,
        string sourceType)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var duplicate = await db.CustomIcons.AnyAsync(i => i.Label.ToLower() == label.ToLower());
        if (duplicate)
            return (false, $"Já existe um ícone personalizado chamado '{label}'.", null);

        var entity = new CustomIconDefinition
        {
            Key = $"Custom_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..6]}",
            Label = label,
            SvgMarkup = svgMarkup,
            SourceType = sourceType,
            CreatedAt = DateTime.Now
        };

        db.CustomIcons.Add(entity);
        await db.SaveChangesAsync();

        return (true, $"Ícone '{label}' cadastrado com sucesso!", entity);
    }

    public static (bool Success, string Message, string SvgMarkup) NormalizeSvgMarkup(string? rawInput)
    {
        if (string.IsNullOrWhiteSpace(rawInput))
            return (false, "Cole o código SVG ou selecione um arquivo .svg.", string.Empty);

        var input = rawInput.Trim();
        if (input.Length > MaxFileSizeBytes)
            return (false, "O SVG deve ter no máximo 512 KB.", string.Empty);

        try
        {
            // Parse as XML with DTD and external entities disabled. Sanitization is based
            // on an allow-list instead of attempting to remove dangerous text by regex.
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MaxFileSizeBytes,
                MaxCharactersFromEntities = 0
            };

            var wrapped = input.StartsWith("<svg", StringComparison.OrdinalIgnoreCase)
                ? input
                : $"<svg viewBox=\"0 0 24 24\">{input}</svg>";

            using var stringReader = new StringReader(wrapped);
            using var reader = XmlReader.Create(stringReader, settings);
            var document = XDocument.Load(reader, LoadOptions.None);
            var root = document.Root;
            if (root is null || !string.Equals(root.Name.LocalName, "svg", StringComparison.OrdinalIgnoreCase))
                return (false, "O conteúdo não possui um elemento SVG válido.", string.Empty);

            SanitizeSvgElement(root);
            var viewBox = (string?)root.Attribute("viewBox");
            if (string.IsNullOrWhiteSpace(viewBox))
                viewBox = "0 0 24 24";

            // Keep the normalized root consistent with the application's icon sizing.
            var normalized = new XElement("svg",
                new XAttribute("viewBox", viewBox),
                new XAttribute("width", "24"),
                new XAttribute("height", "24"),
                root.Nodes());

            if (!normalized.HasElements && string.IsNullOrWhiteSpace(normalized.Value))
                return (false, "O elemento SVG está vazio.", string.Empty);

            return (true, string.Empty, normalized.ToString(SaveOptions.DisableFormatting));
        }
        catch (XmlException)
        {
            return (false, "O SVG informado não é um XML válido.", string.Empty);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return (false, "Não foi possível validar o SVG informado.", string.Empty);
        }
    }

    private static void SanitizeSvgElement(XElement element)
    {
        foreach (var child in element.Elements().ToList())
        {
            if (!AllowedSvgElements.Contains(child.Name.LocalName))
            {
                child.Remove();
                continue;
            }

            SanitizeSvgElement(child);
        }

        foreach (var attribute in element.Attributes().ToList())
        {
            if (attribute.IsNamespaceDeclaration)
            {
                // Preserve only the SVG namespace declaration, if present.
                if (!string.Equals(attribute.Value, "http://www.w3.org/2000/svg", StringComparison.Ordinal))
                    attribute.Remove();
                continue;
            }

            if (!AllowedSvgAttributes.Contains(attribute.Name.LocalName))
            {
                attribute.Remove();
                continue;
            }

            var value = attribute.Value.Trim();
            if (value.Contains("javascript:", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("<", StringComparison.Ordinal) ||
                value.Contains(">", StringComparison.Ordinal))
            {
                attribute.Remove();
                continue;
            }

            // CSS URL references may only target local SVG IDs. External resources are rejected.
            if (value.Contains("url(", StringComparison.OrdinalIgnoreCase) &&
                !Regex.IsMatch(value, @"url\(\s*['""']?#[-A-Za-z0-9_:.]+['""']?\s*\)", RegexOptions.IgnoreCase))
            {
                attribute.Remove();
                continue;
            }

            if (attribute.Name.LocalName.Equals("href", StringComparison.OrdinalIgnoreCase) &&
                !value.StartsWith("#", StringComparison.Ordinal) &&
                !Regex.IsMatch(value, @"^data:image/(png|jpeg|gif|webp|x-icon);base64,[A-Za-z0-9+/]+={0,2}$", RegexOptions.IgnoreCase))
            {
                attribute.Remove();
            }
        }
    }

}
