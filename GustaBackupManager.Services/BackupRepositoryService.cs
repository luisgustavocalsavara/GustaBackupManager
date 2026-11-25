using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GustaBackupManager.App.Interfaces;
using GustaBackupManager.App.Models;
using GustaBackupManager.Models.DTOs;
using GustaBackupManager.Models.Entities;
using GustaBackupManager.Models.Interfaces;

namespace GustaBackupManager.Services;

public class BackupRepositoryService(
    ISettingsService settingsService,
    ISmbConnectionService smbConnectionService,
    IAuthService authService,
    IFileTypeService fileTypeService,
    IFolderCategoryConfigService categoryConfigService) : IBackupRepositoryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private string CurrentActor =>
        authService.CurrentUser is not null
            ? $"{authService.CurrentUser.FullName} ({authService.CurrentUser.Username})"
            : Environment.UserName;

    public async Task<OperationResult<List<FolderTreeNode>>> GetFolderTreeAsync()
    {
        var conn = await smbConnectionService.EnsureConnectedAsync();
        if (!conn.IsConnected)
            return new OperationResult<List<FolderTreeNode>>(false, conn.Message);

        var rootPath = conn.RootPath;

        return await Task.Run(() =>
        {
            try
            {
                if (!Directory.Exists(rootPath))
                    return new OperationResult<List<FolderTreeNode>>(false, $"Diretório raiz não encontrado: {rootPath}");

                var rootDirs = Directory.GetDirectories(rootPath)
                    .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase);

                var nodes = new List<FolderTreeNode>();
                foreach (var dir in rootDirs)
                {
                    nodes.Add(BuildTreeNode(dir, rootPath, string.Empty, isRootLevel: true));
                }

                return new OperationResult<List<FolderTreeNode>>(true, "Estrutura carregada com sucesso.", nodes);
            }
            catch (Exception ex)
            {
                return new OperationResult<List<FolderTreeNode>>(false, $"Erro ao ler pastas do servidor: {ex.Message}");
            }
        });
    }

    public async Task<OperationResult<FolderMetadata>> GetFolderMetadataAsync(string folderPath)
    {
        var conn = await smbConnectionService.EnsureConnectedAsync();
        if (!conn.IsConnected)
            return new OperationResult<FolderMetadata>(false, conn.Message);

        return await Task.Run(() =>
        {
            try
            {
                if (!Directory.Exists(folderPath))
                    return new OperationResult<FolderMetadata>(false, "A pasta selecionada não existe mais no servidor.");

                var isRootChild = IsDirectRootChild(folderPath, conn.RootPath);
                var (metadata, _) = ReadMetadataInternal(folderPath, isRootChild);
                return new OperationResult<FolderMetadata>(true, "Metadados carregados.", metadata);
            }
            catch (Exception ex)
            {
                return new OperationResult<FolderMetadata>(false, $"Erro ao ler metadados da pasta: {ex.Message}");
            }
        });
    }

    public async Task<OperationResult<string>> BuildFileNamePrefixAsync(string folderPath)
    {
        var conn = await smbConnectionService.EnsureConnectedAsync();
        if (!conn.IsConnected)
            return new OperationResult<string>(false, conn.Message, string.Empty);

        if (!IsWithinRoot(folderPath, conn.RootPath))
            return new OperationResult<string>(false, "Pasta fora do diretório raiz configurado.", string.Empty);

        return await Task.Run(() =>
        {
            try
            {
                var prefix = BuildPrefixFromPathInternal(folderPath, conn.RootPath);
                return new OperationResult<string>(true, "Prefixo gerado.", prefix);
            }
            catch (Exception ex)
            {
                return new OperationResult<string>(false, $"Erro ao montar prefixo do nome: {ex.Message}", string.Empty);
            }
        });
    }

    public async Task<OperationResult<FolderTreeNode>> CreateFolderAsync(string? parentFolderPath, FolderEditRequest request)
    {
        var conn = await smbConnectionService.EnsureConnectedAsync();
        if (!conn.IsConnected)
            return new OperationResult<FolderTreeNode>(false, conn.Message);

        var validation = ValidateFolderName(request.Name);
        if (!validation.Success)
            return new OperationResult<FolderTreeNode>(false, validation.Message);

        var baseParent = string.IsNullOrWhiteSpace(parentFolderPath) ? conn.RootPath : parentFolderPath;
        if (!IsWithinRoot(baseParent, conn.RootPath))
            return new OperationResult<FolderTreeNode>(false, "Caminho de destino fora da raiz configurada.");

        var isCreatingAtRoot = string.Equals(NormalizePath(baseParent), NormalizePath(conn.RootPath), StringComparison.OrdinalIgnoreCase);

        if (isCreatingAtRoot && request.Group != FolderGroupType.Fabrica)
            return new OperationResult<FolderTreeNode>(false, "Na raiz do servidor só é permitido cadastrar pastas do tipo 'Fábrica'.");

        if (!isCreatingAtRoot && request.Group == FolderGroupType.Fabrica)
            return new OperationResult<FolderTreeNode>(false, "A pasta 'Fábrica' só pode ser criada na raiz do repositório.");

        if (!isCreatingAtRoot)
        {
            var ancestorPath = NormalizePath(baseParent);
            var normalizedRoot = NormalizePath(conn.RootPath);
            while (!string.Equals(ancestorPath, normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                if (Directory.Exists(ancestorPath))
                {
                    var parentIsRootChild = IsDirectRootChild(ancestorPath, normalizedRoot);
                    var (parentMetadata, _) = ReadMetadataInternal(ancestorPath, parentIsRootChild);
                    if (parentMetadata.Group is FolderGroupType.Projeto or FolderGroupType.Documentacao)
                        return new OperationResult<FolderTreeNode>(false, "Não é permitido criar subpastas dentro de pastas de Projeto ou Documentação.");
                }

                var nextAncestor = Path.GetDirectoryName(ancestorPath);
                if (string.IsNullOrWhiteSpace(nextAncestor) || string.Equals(nextAncestor, ancestorPath, StringComparison.OrdinalIgnoreCase))
                    break;
                ancestorPath = nextAncestor;
            }
        }

        var categoryValidation = ValidateGroupAndSubCategory(request);
        if (!categoryValidation.Success)
            return new OperationResult<FolderTreeNode>(false, categoryValidation.Message);

        var fileTypeValidation = await ValidateFileTypeSelectionsAsync(request);
        if (!fileTypeValidation.Success)
            return new OperationResult<FolderTreeNode>(false, fileTypeValidation.Message);

        var cleanName = request.Name.Trim();
        var targetPath = Path.Combine(baseParent, cleanName);

        if (Directory.Exists(targetPath))
            return new OperationResult<FolderTreeNode>(false, $"Já existe uma pasta chamada '{cleanName}' neste local.");

        var allowsFiles = request.Group is FolderGroupType.Componente or FolderGroupType.Projeto or FolderGroupType.Documentacao;
        var (extensions, labels) = allowsFiles
            ? await ResolveAllowedTypesAsync(request)
            : ([], []);

        if (allowsFiles && extensions.Count == 0)
            return new OperationResult<FolderTreeNode>(false, "Selecione ao menos um tipo de arquivo ou extensão permitida para pastas de Projeto ou Documentação.");

        try
        {
            Directory.CreateDirectory(targetPath);

            var (configuredIcon, configuredColor) = await categoryConfigService.ResolveAppearanceAsync(
                request.Group,
                request.LocationCategory,
                request.ComponentCategory,
                request.DocumentationCategory);

            var now = DateTime.Now;
            var metadata = new FolderMetadata
            {
                Id = Guid.NewGuid(),
                Name = cleanName,
                ShortName = request.ShortName?.Trim() ?? string.Empty,
                IconKey = string.IsNullOrWhiteSpace(request.IconKey) ? configuredIcon : request.IconKey.Trim(),
                IconColor = string.IsNullOrWhiteSpace(request.IconColor) ? configuredColor : request.IconColor.Trim(),
                AllowedFileTypeIds = allowsFiles ? [.. request.AllowedFileTypeIds.Distinct()] : [],
                AllowedExtensions = allowsFiles ? extensions : [],
                AllowedTypeLabels = allowsFiles ? labels : [],
                CreatedAt = now,
                ModifiedAt = now,
                CreatedBy = CurrentActor,
                ModifiedBy = CurrentActor,
                Files = []
            };

            ApplyGroupDetailsToMetadata(metadata, request);

            await WriteMetadataAsync(targetPath, metadata);

            var parentPrefix = isCreatingAtRoot
                ? string.Empty
                : BuildPrefixFromPathInternal(baseParent, conn.RootPath);

            var node = BuildTreeNode(targetPath, conn.RootPath, parentPrefix, isRootLevel: isCreatingAtRoot);
            return new OperationResult<FolderTreeNode>(true, $"Pasta '{cleanName}' criada com arquivo {FolderMetadata.MetadataFileName}.", node);
        }
        catch (Exception ex)
        {
            return new OperationResult<FolderTreeNode>(false, $"Erro ao criar pasta no servidor: {ex.Message}");
        }
    }

    public async Task<OperationResult<FolderMetadata>> UpdateFolderAsync(string folderPath, FolderEditRequest request)
    {
        var conn = await smbConnectionService.EnsureConnectedAsync();
        if (!conn.IsConnected)
            return new OperationResult<FolderMetadata>(false, conn.Message);

        if (!IsWithinRoot(folderPath, conn.RootPath))
            return new OperationResult<FolderMetadata>(false, "Pasta fora do diretório raiz configurado.");

        if (!Directory.Exists(folderPath))
            return new OperationResult<FolderMetadata>(false, "A pasta não foi encontrada no servidor.");

        var validation = ValidateFolderName(request.Name);
        if (!validation.Success)
            return new OperationResult<FolderMetadata>(false, validation.Message);

        var isRootChild = IsDirectRootChild(folderPath, conn.RootPath);

        if (isRootChild && request.Group != FolderGroupType.Fabrica)
            return new OperationResult<FolderMetadata>(false, "Pastas na raiz devem permanecer como 'Fábrica'.");

        if (!isRootChild && request.Group == FolderGroupType.Fabrica)
            return new OperationResult<FolderMetadata>(false, "A pasta 'Fábrica' não pode ser usada fora da raiz.");

        var categoryValidation = ValidateGroupAndSubCategory(request);
        if (!categoryValidation.Success)
            return new OperationResult<FolderMetadata>(false, categoryValidation.Message);

        var fileTypeValidation = await ValidateFileTypeSelectionsAsync(request);
        if (!fileTypeValidation.Success)
            return new OperationResult<FolderMetadata>(false, fileTypeValidation.Message);

        var allowsFiles = request.Group is FolderGroupType.Componente or FolderGroupType.Projeto or FolderGroupType.Documentacao;
        var (extensions, labels) = allowsFiles
            ? await ResolveAllowedTypesAsync(request)
            : ([], []);

        if (allowsFiles && extensions.Count == 0)
            return new OperationResult<FolderMetadata>(false, "Selecione ao menos um tipo de arquivo ou extensão permitida para pastas de Projeto ou Documentação.");

        try
        {
            var (metadata, _) = ReadMetadataInternal(folderPath, isRootChild);
            var cleanName = request.Name.Trim();
            var currentPath = folderPath;

            var parentDir = Path.GetDirectoryName(folderPath);
            var currentDirName = Path.GetFileName(folderPath);
            if (!string.IsNullOrWhiteSpace(parentDir) &&
                !string.Equals(currentDirName, cleanName, StringComparison.OrdinalIgnoreCase))
            {
                var renamedPath = Path.Combine(parentDir, cleanName);
                if (Directory.Exists(renamedPath))
                    return new OperationResult<FolderMetadata>(false, $"Já existe outra pasta com o nome '{cleanName}'.");

                Directory.Move(folderPath, renamedPath);
                currentPath = renamedPath;
            }

            var (configuredIcon, configuredColor) = await categoryConfigService.ResolveAppearanceAsync(
                request.Group,
                request.LocationCategory,
                request.ComponentCategory,
                request.DocumentationCategory);

            metadata.Name = cleanName;
            metadata.ShortName = request.ShortName?.Trim() ?? string.Empty;
            metadata.IconKey = string.IsNullOrWhiteSpace(request.IconKey) ? configuredIcon : request.IconKey.Trim();
            metadata.IconColor = string.IsNullOrWhiteSpace(request.IconColor) ? configuredColor : request.IconColor.Trim();
            metadata.AllowedFileTypeIds = allowsFiles ? [.. request.AllowedFileTypeIds.Distinct()] : [];
            metadata.AllowedExtensions = allowsFiles ? extensions : [];
            metadata.AllowedTypeLabels = allowsFiles ? labels : [];
            metadata.ModifiedAt = DateTime.Now;
            metadata.ModifiedBy = CurrentActor;

            ApplyGroupDetailsToMetadata(metadata, request);

            if (string.IsNullOrWhiteSpace(metadata.CreatedBy))
                metadata.CreatedBy = CurrentActor;

            await WriteMetadataAsync(currentPath, metadata);
            return new OperationResult<FolderMetadata>(true, "Metadados da pasta atualizados com sucesso.", metadata);
        }
        catch (Exception ex)
        {
            return new OperationResult<FolderMetadata>(false, $"Erro ao atualizar a pasta: {ex.Message}");
        }
    }

    public async Task<OperationResult> DeleteFolderAsync(string folderPath)
    {
        var conn = await smbConnectionService.EnsureConnectedAsync();
        if (!conn.IsConnected)
            return new OperationResult(false, conn.Message);

        if (!IsWithinRoot(folderPath, conn.RootPath) ||
            string.Equals(NormalizePath(folderPath), NormalizePath(conn.RootPath), StringComparison.OrdinalIgnoreCase))
            return new OperationResult(false, "Não é permitido excluir o diretório raiz.");

        if (!Directory.Exists(folderPath))
            return new OperationResult(false, "A pasta já não existe no servidor.");

        try
        {
            await Task.Run(() => Directory.Delete(folderPath, recursive: true));
            return new OperationResult(true, "Pasta excluída com sucesso.");
        }
        catch (Exception ex)
        {
            return new OperationResult(false, $"Não foi possível excluir a pasta: {ex.Message}");
        }
    }

    public async Task<OperationResult<List<BackupFileItem>>> GetFilesInFolderAsync(string folderPath)
    {
        var conn = await smbConnectionService.EnsureConnectedAsync();
        if (!conn.IsConnected)
            return new OperationResult<List<BackupFileItem>>(false, conn.Message);

        return await Task.Run(() =>
        {
            try
            {
                if (!IsWithinRoot(folderPath, conn.RootPath))
                    return new OperationResult<List<BackupFileItem>>(false, "Pasta fora do diretório raiz configurado.");

                if (!Directory.Exists(folderPath))
                    return new OperationResult<List<BackupFileItem>>(false, "A pasta não foi encontrada.");

                var isRootChild = IsDirectRootChild(folderPath, conn.RootPath);
                var (metadata, _) = ReadMetadataInternal(folderPath, isRootChild);
                var metadataByName = metadata.Files
                    .GroupBy(f => f.FileName, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                var allowedSet = new HashSet<string>(metadata.AllowedExtensions, StringComparer.OrdinalIgnoreCase);
                var items = new List<BackupFileItem>();

                foreach (var filePath in Directory.GetFiles(folderPath))
                {
                    var fileName = Path.GetFileName(filePath);
                    if (string.Equals(fileName, FolderMetadata.MetadataFileName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var info = new FileInfo(filePath);
                    var ext = info.Extension.ToLowerInvariant();
                    var hasEntry = metadataByName.TryGetValue(fileName, out var entry);

                    items.Add(new BackupFileItem
                    {
                        FileName = fileName,
                        OriginalFileName = entry?.OriginalFileName ?? string.Empty,
                        FullPath = filePath,
                        Extension = ext,
                        SizeBytes = info.Length,
                        CreatedAt = entry?.UploadedAt ?? info.CreationTime,
                        ModifiedAt = info.LastWriteTime,
                        Version = entry?.Version ?? "-",
                        Description = entry?.Description ?? string.Empty,
                        IsRoutineBackup = entry?.IsRoutineBackup ?? false,
                        FileTypeName = entry?.FileTypeName ?? ext.ToUpperInvariant(),
                        UploadedBy = entry?.UploadedBy ?? "Externo / SMB",
                        IsRegisteredInJson = hasEntry,
                        IsAllowedByFolderPolicy = !metadata.AllowFileStorage || allowedSet.Count == 0 || allowedSet.Contains(ext)
                    });
                }

                var ordered = items
                    .OrderByDescending(i => i.ModifiedAt)
                    .ThenBy(i => i.FileName, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return new OperationResult<List<BackupFileItem>>(true, "Arquivos carregados.", ordered);
            }
            catch (Exception ex)
            {
                return new OperationResult<List<BackupFileItem>>(false, $"Erro ao listar arquivos: {ex.Message}");
            }
        });
    }

    public async Task<OperationResult<BackupFileItem>> UploadBackupFileAsync(BackupUploadRequest request)
    {
        var conn = await smbConnectionService.EnsureConnectedAsync();
        if (!conn.IsConnected)
            return new OperationResult<BackupFileItem>(false, conn.Message);

        if (!IsWithinRoot(request.TargetFolderPath, conn.RootPath) || !Directory.Exists(request.TargetFolderPath))
            return new OperationResult<BackupFileItem>(false, "Pasta de destino inválida ou inexistente.");

        if (string.IsNullOrWhiteSpace(request.SourceLocalFilePath) || !File.Exists(request.SourceLocalFilePath))
            return new OperationResult<BackupFileItem>(false, "Arquivo de origem não encontrado.");

        var isRootChild = IsDirectRootChild(request.TargetFolderPath, conn.RootPath);
        var (metadata, _) = ReadMetadataInternal(request.TargetFolderPath, isRootChild);

        if (!metadata.AllowFileStorage)
            return new OperationResult<BackupFileItem>(
                false,
                "Arquivos só podem ser armazenados dentro de pastas da categoria 'Projeto' ou 'Documentação'.");

        if (string.IsNullOrWhiteSpace(request.Description) && !request.IsRoutineBackup)
            return new OperationResult<BackupFileItem>(false, "Informe uma observação ou marque 'Backup de Rotina'.");

        var sourceInfo = new FileInfo(request.SourceLocalFilePath);
        var sourceExt = sourceInfo.Extension.ToLowerInvariant();
        var now = DateTime.Now;

        if (metadata.AllowedExtensions.Count > 0 &&
            !metadata.AllowedExtensions.Contains(sourceExt, StringComparer.OrdinalIgnoreCase))
        {
            var permitted = string.Join(", ", metadata.AllowedExtensions);
            return new OperationResult<BackupFileItem>(
                false,
                $"Extensão '{sourceExt}' não permitida nesta pasta. Extensões aceitas: {permitted}.");
        }

        var prefix = BuildPrefixFromPathInternal(request.TargetFolderPath, conn.RootPath);
        var timestamp = (request.CustomFileTimestamp ?? now).ToString("yyyyMMdd_HHmmss");
        var baseName = string.IsNullOrWhiteSpace(prefix)
            ? timestamp
            : $"{prefix}_{timestamp}";
        var targetFileName = $"{baseName}{sourceExt}";
        var sequence = 1;
        while (File.Exists(Path.Combine(request.TargetFolderPath, targetFileName)))
        {
            targetFileName = $"{baseName}_{sequence:00}{sourceExt}";
            sequence++;
        }

        var extension = sourceExt;
        var destinationPath = Path.Combine(request.TargetFolderPath, targetFileName);

        var temporaryPath = Path.Combine(request.TargetFolderPath, $".{targetFileName}.{Guid.NewGuid():N}.uploading");
        try
        {
            // Upload to a temporary file first so an interrupted copy does not leave
            // a partially written backup under its final name.
            await using (var sourceStream = new FileStream(request.SourceLocalFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true))
            await using (var destStream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await sourceStream.CopyToAsync(destStream);
                await destStream.FlushAsync();
            }

            File.Move(temporaryPath, destinationPath, overwrite: false);

            var fileTypeName = await GuessFileTypeNameAsync(extension);

            metadata.Files.RemoveAll(f => string.Equals(f.FileName, targetFileName, StringComparison.OrdinalIgnoreCase));
            metadata.Files.Add(new BackupFileMetadataEntry
            {
                FileName = targetFileName,
                OriginalFileName = sourceInfo.Name,
                Extension = extension,
                Version = "1.0",
                Description = request.Description?.Trim() ?? string.Empty,
                IsRoutineBackup = request.IsRoutineBackup,
                FileTypeName = fileTypeName,
                UploadedBy = CurrentActor,
                UploadedAt = now,
                ModifiedAt = now,
                SizeBytes = sourceInfo.Length
            });

            metadata.ModifiedAt = now;
            metadata.ModifiedBy = CurrentActor;
            await WriteMetadataAsync(request.TargetFolderPath, metadata);

            var item = new BackupFileItem
            {
                FileName = targetFileName,
                OriginalFileName = sourceInfo.Name,
                FullPath = destinationPath,
                Extension = extension,
                SizeBytes = sourceInfo.Length,
                CreatedAt = now,
                ModifiedAt = now,
                Version = "1.0",
                Description = request.Description?.Trim() ?? string.Empty,
                IsRoutineBackup = request.IsRoutineBackup,
                FileTypeName = fileTypeName,
                UploadedBy = CurrentActor,
                IsRegisteredInJson = true,
                IsAllowedByFolderPolicy = true
            };

            return new OperationResult<BackupFileItem>(true, $"Backup salvo como '{targetFileName}'.", item);
        }
        catch (Exception ex)
        {
            if (ex is IOException && File.Exists(destinationPath))
                return new OperationResult<BackupFileItem>(false, $"Já existe um arquivo '{targetFileName}' nesta pasta. Nenhum arquivo foi sobrescrito.");

            return new OperationResult<BackupFileItem>(false, $"Erro ao copiar arquivo para o servidor: {ex.Message}");
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            catch
            {
                // Best-effort cleanup; do not hide the original operation result.
            }
        }
    }

    public async Task<OperationResult> DownloadBackupFileAsync(string remoteFilePath, string destinationLocalPath)
    {
        var conn = await smbConnectionService.EnsureConnectedAsync();
        if (!conn.IsConnected)
            return new OperationResult(false, conn.Message);

        if (!IsWithinRoot(remoteFilePath, conn.RootPath))
            return new OperationResult(false, "Arquivo fora do diretório raiz configurado.");

        if (!File.Exists(remoteFilePath))
            return new OperationResult(false, "Arquivo não encontrado no servidor.");

        if (string.IsNullOrWhiteSpace(destinationLocalPath))
            return new OperationResult(false, "Informe um caminho local de destino válido.");

        var temporaryPath = $"{destinationLocalPath}.{Guid.NewGuid():N}.download";
        try
        {
            await using var sourceStream = new FileStream(remoteFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
            await using (var destStream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await sourceStream.CopyToAsync(destStream);
                await destStream.FlushAsync();
            }

            File.Move(temporaryPath, destinationLocalPath, overwrite: true);
            return new OperationResult(true, $"Arquivo salvo em: {destinationLocalPath}");
        }
        catch (Exception ex)
        {
            return new OperationResult(false, $"Erro ao baixar arquivo: {ex.Message}");
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }

    public async Task<OperationResult> DeleteBackupFileAsync(string remoteFilePath)
    {
        var conn = await smbConnectionService.EnsureConnectedAsync();
        if (!conn.IsConnected)
            return new OperationResult(false, conn.Message);

        if (!IsWithinRoot(remoteFilePath, conn.RootPath))
            return new OperationResult(false, "Arquivo fora da raiz configurada.");

        if (!File.Exists(remoteFilePath))
            return new OperationResult(false, "Arquivo não encontrado no servidor.");

        try
        {
            var folderPath = Path.GetDirectoryName(remoteFilePath)!;
            var fileName = Path.GetFileName(remoteFilePath);

            File.Delete(remoteFilePath);

            var isRootChild = IsDirectRootChild(folderPath, conn.RootPath);
            var (metadata, hasMeta) = ReadMetadataInternal(folderPath, isRootChild);
            if (hasMeta)
            {
                metadata.Files.RemoveAll(f => string.Equals(f.FileName, fileName, StringComparison.OrdinalIgnoreCase));
                metadata.ModifiedAt = DateTime.Now;
                metadata.ModifiedBy = CurrentActor;
                await WriteMetadataAsync(folderPath, metadata);
            }

            return new OperationResult(true, $"Arquivo '{fileName}' excluído com sucesso.");
        }
        catch (Exception ex)
        {
            return new OperationResult(false, $"Erro ao excluir arquivo: {ex.Message}");
        }
    }

    public async Task<OperationResult<int>> SyncFileTypePolicyAcrossFoldersAsync(int fileTypeId)
    {
        var conn = await smbConnectionService.EnsureConnectedAsync();
        if (!conn.IsConnected)
            return new OperationResult<int>(false, conn.Message, 0);

        var allTypes = await fileTypeService.GetAllAsync(onlyActive: false);
        var typesById = allTypes.ToDictionary(t => t.Id);

        if (!typesById.ContainsKey(fileTypeId))
            return new OperationResult<int>(false, "Tipo de arquivo não encontrado no catálogo.", 0);

        var updatedCount = 0;

        async Task WalkAndSyncAsync(string currentDir, bool isRootChild)
        {
            var (metadata, hasMeta) = ReadMetadataInternal(currentDir, isRootChild);
            if (hasMeta && metadata.AllowFileStorage && metadata.AllowedFileTypeIds.Contains(fileTypeId))
            {
                var customExtensions = new List<string>();
                foreach (var label in metadata.AllowedTypeLabels)
                {
                    if (label.StartsWith("Personalizado (", StringComparison.OrdinalIgnoreCase) && label.EndsWith(')'))
                    {
                        var inside = label["Personalizado (".Length..^1];
                        customExtensions.AddRange(
                            inside.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                  .Select(e => e.StartsWith('.') ? e.ToLowerInvariant() : "." + e.ToLowerInvariant()));
                    }
                }

                var newExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var newLabels = new List<string>();

                foreach (var linkedId in metadata.AllowedFileTypeIds.Distinct())
                {
                    if (typesById.TryGetValue(linkedId, out var ft))
                    {
                        var exts = ft.GetNormalizedExtensions();
                        foreach (var ext in exts)
                            newExtensions.Add(ext);

                        newLabels.Add($"{ft.Name} ({string.Join(", ", exts)})");
                    }
                }

                var distinctCustom = customExtensions.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                foreach (var ext in distinctCustom)
                    newExtensions.Add(ext);

                if (distinctCustom.Count > 0)
                    newLabels.Add($"Personalizado ({string.Join(", ", distinctCustom)})");

                metadata.AllowedExtensions = newExtensions.OrderBy(e => e, StringComparer.OrdinalIgnoreCase).ToList();
                metadata.AllowedTypeLabels = newLabels;
                metadata.ModifiedAt = DateTime.Now;
                metadata.ModifiedBy = CurrentActor;

                await WriteMetadataAsync(currentDir, metadata);
                updatedCount++;
            }

            string[] subDirs;
            try
            {
                subDirs = Directory.GetDirectories(currentDir);
            }
            catch
            {
                return;
            }

            foreach (var subDir in subDirs)
            {
                await WalkAndSyncAsync(subDir, isRootChild: false);
            }
        }

        try
        {
            foreach (var rootDir in Directory.GetDirectories(conn.RootPath))
            {
                await WalkAndSyncAsync(rootDir, isRootChild: true);
            }

            var msg = updatedCount == 0
                ? "Nenhuma pasta de repositório no SMB estava vinculada a este tipo."
                : $"{updatedCount} pasta(s) de repositório atualizada(s) no servidor SMB.";

            return new OperationResult<int>(true, msg, updatedCount);
        }
        catch (Exception ex)
        {
            return new OperationResult<int>(false, $"Erro ao sincronizar pastas no SMB: {ex.Message}", updatedCount);
        }
    }

    public async Task<OperationResult<int>> SyncCategoryAppearanceAcrossFoldersAsync(
        FolderGroupType group,
        LocationSubCategory? locationCategory,
        ComponentSubCategory? componentCategory,
        DocumentationSubCategory? documentationCategory,
        string iconKey,
        string iconColor)
    {
        var conn = await smbConnectionService.EnsureConnectedAsync();
        if (!conn.IsConnected)
            return new OperationResult<int>(false, conn.Message, 0);

        var updatedCount = 0;

        bool MatchesTarget(FolderMetadata m) =>
            m.Group == group && group switch
            {
                FolderGroupType.Localizacao => m.LocationCategory == locationCategory,
                FolderGroupType.Componente => m.ComponentCategory == componentCategory,
                FolderGroupType.Documentacao => m.DocumentationCategory == documentationCategory,
                _ => true
            };

        async Task WalkAndSyncAsync(string currentDir, bool isRootChild)
        {
            var (metadata, hasMeta) = ReadMetadataInternal(currentDir, isRootChild);
            if (hasMeta && MatchesTarget(metadata))
            {
                metadata.IconKey = iconKey;
                metadata.IconColor = iconColor;
                metadata.ModifiedAt = DateTime.Now;
                metadata.ModifiedBy = CurrentActor;
                await WriteMetadataAsync(currentDir, metadata);
                updatedCount++;
            }

            string[] subDirs;
            try
            {
                subDirs = Directory.GetDirectories(currentDir);
            }
            catch
            {
                return;
            }

            foreach (var subDir in subDirs)
            {
                await WalkAndSyncAsync(subDir, isRootChild: false);
            }
        }

        try
        {
            foreach (var rootDir in Directory.GetDirectories(conn.RootPath))
            {
                await WalkAndSyncAsync(rootDir, isRootChild: true);
            }

            var msg = updatedCount == 0
                ? "Nenhuma pasta existente no SMB pertencia a esta categoria."
                : $"{updatedCount} pasta(s) existente(s) atualizada(s) com o novo ícone e cor no SMB.";

            return new OperationResult<int>(true, msg, updatedCount);
        }
        catch (Exception ex)
        {
            return new OperationResult<int>(false, $"Erro ao atualizar pastas no SMB: {ex.Message}", updatedCount);
        }
    }

    public async Task<OperationResult> SeedExamplePlantStructureAsync()
    {
        var conn = await smbConnectionService.EnsureConnectedAsync();
        if (!conn.IsConnected)
            return new OperationResult(false, conn.Message);

        var fileTypes = await fileTypeService.GetAllAsync(onlyActive: true);
        int? FindTypeId(string name) => fileTypes.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase))?.Id;

        var fabApp = await categoryConfigService.ResolveAppearanceAsync(FolderGroupType.Fabrica);
        var fabricaRes = await CreateOrGetFolderAsync(conn.RootPath, new FolderEditRequest
        {
            Name = "Fabrica Vinhedo",
            ShortName = "Vin",
            Group = FolderGroupType.Fabrica,
            IconKey = fabApp.IconKey,
            IconColor = fabApp.IconColor,
            FactoryInfo = new FactoryMetadataDetails
            {
                RazaoSocial = "Indústria Exemplo Automação S.A.",
                Cnpj = "12.345.678/0001-90",
                Endereco = "Rod. Anhanguera, Km 74 - Valinhos / Vinhedo - SP",
                Informacoes = "Unidade fabril principal de envase e estocagem."
            }
        });
        if (!fabricaRes.Success || fabricaRes.Data is null) return new OperationResult(false, fabricaRes.Message);

        var estocApp = await categoryConfigService.ResolveAppearanceAsync(FolderGroupType.Localizacao, LocationSubCategory.Estocagem);
        var estocRes = await CreateOrGetFolderAsync(fabricaRes.Data.FullPath, new FolderEditRequest
        {
            Name = "Estocagem",
            ShortName = "Estoc",
            Group = FolderGroupType.Localizacao,
            LocationCategory = LocationSubCategory.Estocagem,
            Description = "Área de estocagem e movimentação automatizada.",
            IconKey = estocApp.IconKey,
            IconColor = estocApp.IconColor
        });
        if (!estocRes.Success || estocRes.Data is null) return new OperationResult(false, estocRes.Message);

        var linhaApp = await categoryConfigService.ResolveAppearanceAsync(FolderGroupType.Localizacao, LocationSubCategory.LinhaProducao);
        var linhaRes = await CreateOrGetFolderAsync(estocRes.Data.FullPath, new FolderEditRequest
        {
            Name = "Setor 01",
            ShortName = "S01",
            Group = FolderGroupType.Localizacao,
            LocationCategory = LocationSubCategory.LinhaProducao,
            Description = "Linha de produção / Setor 01.",
            IconKey = linhaApp.IconKey,
            IconColor = linhaApp.IconColor
        });
        if (!linhaRes.Success || linhaRes.Data is null) return new OperationResult(false, linhaRes.Message);

        var eqApp = await categoryConfigService.ResolveAppearanceAsync(FolderGroupType.Equipamento);
        var equipRes = await CreateOrGetFolderAsync(linhaRes.Data.FullPath, new FolderEditRequest
        {
            Name = "Encaixotador",
            ShortName = string.Empty,
            Group = FolderGroupType.Equipamento,
            IconKey = eqApp.IconKey,
            IconColor = eqApp.IconColor,
            EquipmentInfo = new EquipmentMetadataDetails
            {
                Fabricante = "Krones / Sidel",
                Modelo = "Variopac Pro FS",
                AnoFabricacao = "2023",
                NumeroSerie = "SN-2023-88412"
            }
        });
        if (!equipRes.Success || equipRes.Data is null) return new OperationResult(false, equipRes.Message);

        var clpTypeId = FindTypeId("Studio 5000");
        var ihmTypeId = FindTypeId("FactoryTalk View");
        var invTypeId = FindTypeId("Inversores de Frequência");
        var docTypeId = FindTypeId("Diagramas Elétricos");

        var clpApp = await categoryConfigService.ResolveAppearanceAsync(FolderGroupType.Componente, componentCategory: ComponentSubCategory.Clp);
        await CreateOrGetFolderAsync(equipRes.Data.FullPath, new FolderEditRequest
        {
            Name = "CLP",
            ShortName = "CLP",
            Group = FolderGroupType.Componente,
            ComponentCategory = ComponentSubCategory.Clp,
            IconKey = clpApp.IconKey,
            IconColor = clpApp.IconColor,
            ComponentInfo = new ComponentMetadataDetails
            {
                Observations = "Controlador principal da linha."
            },
            AllowedFileTypeIds = clpTypeId.HasValue ? [clpTypeId.Value] : []
        });

        var ihmApp = await categoryConfigService.ResolveAppearanceAsync(FolderGroupType.Componente, componentCategory: ComponentSubCategory.Ihm);
        await CreateOrGetFolderAsync(equipRes.Data.FullPath, new FolderEditRequest
        {
            Name = "IHM",
            ShortName = "IHM",
            Group = FolderGroupType.Componente,
            ComponentCategory = ComponentSubCategory.Ihm,
            IconKey = ihmApp.IconKey,
            IconColor = ihmApp.IconColor,
            ComponentInfo = new ComponentMetadataDetails
            {
                Observations = "Interface de operação da linha."
            },
            AllowedFileTypeIds = ihmTypeId.HasValue ? [ihmTypeId.Value] : []
        });

        var invApp = await categoryConfigService.ResolveAppearanceAsync(FolderGroupType.Componente, componentCategory: ComponentSubCategory.Inversor);
        await CreateOrGetFolderAsync(equipRes.Data.FullPath, new FolderEditRequest
        {
            Name = "Inversor",
            ShortName = "INV",
            Group = FolderGroupType.Componente,
            ComponentCategory = ComponentSubCategory.Inversor,
            IconKey = invApp.IconKey,
            IconColor = invApp.IconColor,
            ComponentInfo = new ComponentMetadataDetails
            {
                Observations = "Inversor de frequência do motor de alimentação."
            },
            AllowedFileTypeIds = invTypeId.HasValue ? [invTypeId.Value] : []
        });

        var docApp = await categoryConfigService.ResolveAppearanceAsync(FolderGroupType.Documentacao, documentationCategory: DocumentationSubCategory.DiagramaEletrico);
        await CreateOrGetFolderAsync(equipRes.Data.FullPath, new FolderEditRequest
        {
            Name = "Diagrama Elétrico",
            ShortName = "DE",
            Group = FolderGroupType.Documentacao,
            DocumentationCategory = DocumentationSubCategory.DiagramaEletrico,
            Description = "Esquemas elétricos as-built em PDF e EPLAN.",
            IconKey = docApp.IconKey,
            IconColor = docApp.IconColor,
            AllowedFileTypeIds = docTypeId.HasValue ? [docTypeId.Value] : []
        });

        return new OperationResult(true, "Estrutura industrial de exemplo criada com sucesso!");
    }

    private static void ApplyGroupDetailsToMetadata(FolderMetadata metadata, FolderEditRequest request)
    {
        metadata.Group = request.Group;
        metadata.LocationCategory = request.Group == FolderGroupType.Localizacao ? request.LocationCategory : null;
        metadata.ComponentCategory = request.Group == FolderGroupType.Componente ? request.ComponentCategory : null;
        metadata.DocumentationCategory = request.Group == FolderGroupType.Documentacao ? request.DocumentationCategory : null;

        metadata.FactoryInfo = null;
        metadata.EquipmentInfo = null;
        metadata.ComponentInfo = null;
        metadata.ProjectInfo = null;

        switch (request.Group)
        {
            case FolderGroupType.Fabrica:
                var info = request.FactoryInfo ?? new FactoryMetadataDetails();
                metadata.FactoryInfo = new FactoryMetadataDetails
                {
                    RazaoSocial = info.RazaoSocial?.Trim() ?? string.Empty,
                    Cnpj = info.Cnpj?.Trim() ?? string.Empty,
                    Endereco = info.Endereco?.Trim() ?? string.Empty,
                    Informacoes = info.Informacoes?.Trim() ?? string.Empty
                };
                metadata.Description = metadata.FactoryInfo.Informacoes;
                break;

            case FolderGroupType.Equipamento:
                var eq = request.EquipmentInfo ?? new EquipmentMetadataDetails();
                metadata.EquipmentInfo = new EquipmentMetadataDetails
                {
                    Fabricante = eq.Fabricante?.Trim() ?? string.Empty,
                    Modelo = eq.Modelo?.Trim() ?? string.Empty,
                    AnoFabricacao = eq.AnoFabricacao?.Trim() ?? string.Empty,
                    NumeroSerie = eq.NumeroSerie?.Trim() ?? string.Empty
                };
                metadata.Description = request.Description?.Trim() ?? string.Empty;
                break;

            case FolderGroupType.Projeto:
                var pr = request.ProjectInfo ?? new ProjectMetadataDetails();
                metadata.ProjectInfo = new ProjectMetadataDetails
                {
                    FileTypeId = pr.FileTypeId,
                    Category = pr.Category,
                    ProjectName = pr.ProjectName?.Trim() ?? string.Empty,
                    AllowedExtensions = pr.AllowedExtensions?.Trim() ?? string.Empty
                };
                metadata.Description = request.Description?.Trim() ?? string.Empty;
                break;

            case FolderGroupType.Componente:
                var cp = request.ComponentInfo ?? new ComponentMetadataDetails();
                metadata.ComponentInfo = new ComponentMetadataDetails
                {
                    Observations = cp.Observations?.Trim() ?? string.Empty
                };
                metadata.Description = request.Description?.Trim() ?? string.Empty;
                break;

            default:
                metadata.Description = request.Description?.Trim() ?? string.Empty;
                break;
        }
    }

    private async Task<OperationResult> ValidateFileTypeSelectionsAsync(FolderEditRequest request)
    {
        if (request.Group is not (FolderGroupType.Projeto or FolderGroupType.Documentacao))
            return new OperationResult(true, string.Empty);

        var fileTypes = await fileTypeService.GetAllAsync(onlyActive: false);
        if (request.Group == FolderGroupType.Projeto)
        {
            if (!request.ProjectInfo.FileTypeId.HasValue ||
                fileTypes.All(type => type.Id != request.ProjectInfo.FileTypeId.Value))
            {
                return new OperationResult(false, "Selecione um tipo de repositório cadastrado.");
            }

            return new OperationResult(true, string.Empty);
        }

        var selectedIds = request.AllowedFileTypeIds.ToHashSet();
        if (fileTypes.Count(type => selectedIds.Contains(type.Id)) != selectedIds.Count)
            return new OperationResult(false, "Um ou mais tipos de repositório selecionados não existem mais.");

        if (request.AllowedFileTypeIds.Count == 0)
            return new OperationResult(false, "Selecione ao menos um repositório cadastrado para definir as extensões permitidas.");

        return new OperationResult(true, string.Empty);
    }

    private static OperationResult ValidateGroupAndSubCategory(FolderEditRequest request)
    {
        if (!Enum.IsDefined(request.Group))
            return new OperationResult(false, "Selecione um tipo de pasta válido.");

        if (request.Group == FolderGroupType.Localizacao &&
            (!request.LocationCategory.HasValue || !Enum.IsDefined(request.LocationCategory.Value)))
            return new OperationResult(false, "Selecione uma categoria válida para Localização.");

        if (request.Group == FolderGroupType.Projeto &&
            (!request.ProjectInfo.Category.HasValue || !Enum.IsDefined(request.ProjectInfo.Category.Value)))
            return new OperationResult(false, "Selecione uma categoria válida para Projeto.");

        if (request.Group == FolderGroupType.Projeto && !request.ProjectInfo.FileTypeId.HasValue)
            return new OperationResult(false, "Selecione um software cadastrado em Tipos de Repositório.");

        if (request.Group == FolderGroupType.Componente &&
            (!request.ComponentCategory.HasValue || !Enum.IsDefined(request.ComponentCategory.Value)))
            return new OperationResult(false, "Selecione uma categoria válida para Componente (estrutura antiga).");

        if (request.Group == FolderGroupType.Documentacao &&
            (!request.DocumentationCategory.HasValue || !Enum.IsDefined(request.DocumentationCategory.Value)))
            return new OperationResult(false, "Selecione uma categoria válida para Documentação.");

        return new OperationResult(true, string.Empty);
    }

    private async Task<OperationResult<FolderTreeNode>> CreateOrGetFolderAsync(string parentPath, FolderEditRequest request)
    {
        var candidate = Path.Combine(parentPath, request.Name.Trim());
        if (Directory.Exists(candidate))
        {
            var root = settingsService.Current.Smb.RootPath;
            var isRootChild = IsDirectRootChild(candidate, root);
            var parentPrefix = isRootChild
                ? string.Empty
                : BuildPrefixFromPathInternal(parentPath, root);
            return new OperationResult<FolderTreeNode>(true, "Pasta já existente.", BuildTreeNode(candidate, root, parentPrefix, isRootChild));
        }
        return await CreateFolderAsync(parentPath, request);
    }

    private FolderTreeNode BuildTreeNode(string folderPath, string rootPath, string parentPrefix, bool isRootLevel)
    {
        var (metadata, hasMeta) = ReadMetadataInternal(folderPath, isRootLevel);
        var dirName = Path.GetFileName(folderPath);

        if (string.IsNullOrWhiteSpace(metadata.Name))
            metadata.Name = dirName;

        var segment = ResolveFolderSegment(metadata, dirName);
        var namingPrefix = string.IsNullOrWhiteSpace(parentPrefix)
            ? segment
            : string.IsNullOrWhiteSpace(segment)
                ? parentPrefix
                : $"{parentPrefix}_{segment}";

        var fileCount = 0;
        try
        {
            fileCount = Directory.EnumerateFiles(folderPath)
                .Count(f => !string.Equals(Path.GetFileName(f), FolderMetadata.MetadataFileName, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            fileCount = 0;
        }

        var node = new FolderTreeNode
        {
            FullPath = folderPath,
            RelativePath = Path.GetRelativePath(rootPath, folderPath),
            Name = dirName,
            NamingPrefix = namingPrefix,
            HasMetadataFile = hasMeta,
            Metadata = metadata,
            FileCount = fileCount
        };

        try
        {
            var subDirs = Directory.GetDirectories(folderPath)
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase);

            foreach (var subDir in subDirs)
            {
                node.Children.Add(BuildTreeNode(subDir, rootPath, namingPrefix, isRootLevel: false));
            }
        }
        catch
        {
            // Ignora subdiretorios sem permissao de leitura
        }

        return node;
    }

    private static string BuildPrefixFromPathInternal(string folderPath, string rootPath)
    {
        var normalizedFolder = NormalizePath(folderPath);
        var normalizedRoot = NormalizePath(rootPath);

        if (string.Equals(normalizedFolder, normalizedRoot, StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        var relative = Path.GetRelativePath(normalizedRoot, normalizedFolder);
        var parts = relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);

        var current = normalizedRoot;
        var segments = new List<string>(parts.Length);

        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            current = Path.Combine(current, part);
            var (meta, _) = ReadMetadataInternal(current, isRootChild: i == 0);
            var segment = ResolveFolderSegment(meta, part);
            if (!string.IsNullOrWhiteSpace(segment))
                segments.Add(segment);
        }

        return string.Join("_", segments);
    }

    private static string ResolveFolderSegment(FolderMetadata metadata, string fallbackFolderName)
    {
        var chosen = !string.IsNullOrWhiteSpace(metadata.ShortName)
            ? metadata.ShortName
            : !string.IsNullOrWhiteSpace(metadata.Name)
                ? metadata.Name
                : fallbackFolderName;

        return SanitizeFileNameSegment(chosen);
    }

    public static string SanitizeFileNameSegment(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var invalidChars = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(raw.Length);
        var lastWasUnderscore = false;

        foreach (var ch in raw.Trim())
        {
            if (char.IsWhiteSpace(ch) || invalidChars.Contains(ch) || ch == '_')
            {
                if (!lastWasUnderscore && sb.Length > 0)
                {
                    sb.Append('_');
                    lastWasUnderscore = true;
                }
            }
            else
            {
                sb.Append(ch);
                lastWasUnderscore = false;
            }
        }

        return sb.ToString().Trim('_');
    }

    private static (FolderMetadata Metadata, bool HasMetadataFile) ReadMetadataInternal(string folderPath, bool isRootChild)
    {
        var metaPath = Path.Combine(folderPath, FolderMetadata.MetadataFileName);
        if (File.Exists(metaPath))
        {
            try
            {
                var json = File.ReadAllText(metaPath);
                var parsed = JsonSerializer.Deserialize<FolderMetadata>(json, JsonOptions);
                if (parsed is not null)
                {
                    if (isRootChild)
                    {
                        parsed.Group = FolderGroupType.Fabrica;
                        parsed.FactoryInfo ??= new FactoryMetadataDetails { Informacoes = parsed.Description };
                    }
                    return (parsed, true);
                }
            }
            catch
            {
                // Fallback para metadados inferidos caso o JSON esteja corrompido
            }
        }

        var info = new DirectoryInfo(folderPath);
        if (isRootChild)
        {
            return (new FolderMetadata
            {
                Name = info.Name,
                ShortName = string.Empty,
                Description = "Fábrica existente no servidor (sem folder.meta.json ainda).",
                Group = FolderGroupType.Fabrica,
                IconKey = "Domain",
                IconColor = "Primary",
                FactoryInfo = new FactoryMetadataDetails(),
                CreatedAt = info.CreationTime,
                ModifiedAt = info.LastWriteTime,
                CreatedBy = "Servidor SMB",
                ModifiedBy = "Servidor SMB"
            }, false);
        }

        return (new FolderMetadata
        {
            Name = info.Name,
            ShortName = string.Empty,
            Description = "Pasta existente no servidor (sem folder.meta.json ainda).",
            Group = FolderGroupType.Diversas,
            IconKey = "Folder",
            IconColor = "Default",
            CreatedAt = info.CreationTime,
            ModifiedAt = info.LastWriteTime,
            CreatedBy = "Servidor SMB",
            ModifiedBy = "Servidor SMB"
        }, false);
    }

    private static async Task WriteMetadataAsync(string folderPath, FolderMetadata metadata)
    {
        var metaPath = Path.Combine(folderPath, FolderMetadata.MetadataFileName);
        var tempPath = Path.Combine(folderPath, $"{FolderMetadata.MetadataFileName}.{Guid.NewGuid():N}.tmp");
        var json = JsonSerializer.Serialize(metadata, JsonOptions);

        try
        {
            await File.WriteAllTextAsync(tempPath, json, Encoding.UTF8);
            // Replace the metadata only after the temporary file was fully written.
            File.Move(tempPath, metaPath, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch
            {
                // Cleanup is best-effort; preserve the original write exception.
            }
        }
    }

    private async Task<(List<string> Extensions, List<string> Labels)> ResolveAllowedTypesAsync(FolderEditRequest request)
    {
        var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var labels = new List<string>();

        if (request.AllowedFileTypeIds.Count > 0)
        {
            var allTypes = await fileTypeService.GetAllAsync(onlyActive: false);
            var allowedTypes = allTypes.Where(type => request.AllowedFileTypeIds.Contains(type.Id));

            foreach (var ft in allowedTypes)
            {
                var exts = ft.GetNormalizedExtensions();
                foreach (var ext in exts)
                    extensions.Add(ext);

                labels.Add($"{ft.Name} ({string.Join(", ", exts)})");
            }
        }

        if (request.Group == FolderGroupType.Projeto)
        {
            var types = await fileTypeService.GetAllAsync(onlyActive: false);
            var selectedType = types.FirstOrDefault(type => type.Id == request.ProjectInfo.FileTypeId);

            var projectExtensions = selectedType?.GetNormalizedExtensions() ?? [];

            foreach (var ext in projectExtensions)
                extensions.Add(ext);

            if (selectedType is not null)
            {
                request.ProjectInfo.ProjectName = $"{request.ProjectInfo.Category?.GetLabel()} ({selectedType.Name})";
                request.ProjectInfo.AllowedExtensions = string.Join(";", projectExtensions);
            }

            if (selectedType is not null)
                labels.Add($"{selectedType.Name} ({string.Join(", ", projectExtensions)})");
            else if (projectExtensions.Count > 0)
                labels.Add($"Projeto ({string.Join(", ", projectExtensions)})");
        }

        return (extensions.OrderBy(e => e, StringComparer.OrdinalIgnoreCase).ToList(), labels);
    }

    private async Task<string> GuessFileTypeNameAsync(string extension)
    {
        var types = await fileTypeService.GetAllAsync(onlyActive: false);
        var match = types.FirstOrDefault(t =>
            t.GetNormalizedExtensions().Contains(extension, StringComparer.OrdinalIgnoreCase));

        return match?.Name ?? $"Arquivo {extension.ToUpperInvariant()}";
    }

    private static OperationResult ValidateFolderName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return new OperationResult(false, "Informe o nome da pasta.");

        var trimmed = name.Trim();
        if (trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || trimmed is "." or "..")
            return new OperationResult(false, "O nome da pasta contém caracteres inválidos para o sistema de arquivos.");

        return new OperationResult(true, string.Empty);
    }

    private static bool IsDirectRootChild(string folderPath, string rootPath)
    {
        var parent = Path.GetDirectoryName(NormalizePath(folderPath));
        return !string.IsNullOrWhiteSpace(parent) &&
               string.Equals(NormalizePath(parent), NormalizePath(rootPath), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWithinRoot(string candidatePath, string rootPath)
    {
        var normalizedCandidate = NormalizePath(candidatePath);
        var normalizedRoot = NormalizePath(rootPath);

        if (string.Equals(normalizedCandidate, normalizedRoot, StringComparison.OrdinalIgnoreCase))
            return true;

        return normalizedCandidate.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path) =>
        Path.GetFullPath(path.Trim()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
