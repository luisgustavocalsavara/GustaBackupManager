using System.Diagnostics;
using GustaBackupManager.App.Interfaces;
using GustaBackupManager.App.Models;

namespace GustaBackupManager.Adapters.WinForms;

public sealed class WinFormsFileDialogService : INativeFileDialogService
{
    public Task<SelectedLocalFile?> PickFileAsync(string title, IReadOnlyCollection<string> allowedExtensions)
    {
        return RunStaAsync<SelectedLocalFile?>(() =>
        {
            using var dialog = new OpenFileDialog
            {
                Title = title,
                CheckFileExists = true,
                Multiselect = false,
                Filter = BuildFilter(allowedExtensions)
            };

            if (dialog.ShowDialog() != DialogResult.OK || string.IsNullOrWhiteSpace(dialog.FileName))
                return null;

            var info = new FileInfo(dialog.FileName);
            return new SelectedLocalFile(info.FullName, info.Name, info.Extension.ToLowerInvariant(), info.Length);
        });
    }

    public Task<string?> PickSaveLocationAsync(string title, string suggestedFileName, string extension)
    {
        return RunStaAsync<string?>(() =>
        {
            var cleanExt = string.IsNullOrWhiteSpace(extension) ? string.Empty : extension.TrimStart('.');
            var filter = string.IsNullOrWhiteSpace(cleanExt)
                ? "Todos os arquivos (*.*)|*.*"
                : $"Arquivo {cleanExt.ToUpperInvariant()} (*.{cleanExt})|*.{cleanExt}|Todos os arquivos (*.*)|*.*";

            using var dialog = new SaveFileDialog
            {
                Title = title,
                Filter = filter,
                DefaultExt = cleanExt,
                FileName = suggestedFileName,
                AddExtension = !string.IsNullOrWhiteSpace(cleanExt),
                OverwritePrompt = true
            };

            return dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : null;
        });
    }

    public Task<string?> PickFolderAsync(string description, string? initialPath = null)
    {
        return RunStaAsync<string?>(() =>
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = description,
                UseDescriptionForTitle = true,
                ShowNewFolderButton = true
            };

            if (!string.IsNullOrWhiteSpace(initialPath) && Directory.Exists(initialPath))
                dialog.InitialDirectory = initialPath;

            return dialog.ShowDialog() == DialogResult.OK ? dialog.SelectedPath : null;
        });
    }

    public OperationResult OpenPathInShell(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return new OperationResult(false, "Caminho inválido.");

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
            return new OperationResult(true, "Aberto no sistema operacional.");
        }
        catch (Exception ex)
        {
            return new OperationResult(false, $"Não foi possível abrir o caminho: {ex.Message}");
        }
    }

    private static string BuildFilter(IReadOnlyCollection<string> allowedExtensions)
    {
        if (allowedExtensions is null || allowedExtensions.Count == 0)
            throw new ArgumentException("Informe ao menos uma extensão permitida para selecionar um arquivo.", nameof(allowedExtensions));

        var normalized = allowedExtensions
            .Where(extension => !string.IsNullOrWhiteSpace(extension))
            .Select(extension => extension.StartsWith('.') ? $"*{extension.ToLowerInvariant()}" : $"*.{extension.ToLowerInvariant()}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Count == 0)
            throw new ArgumentException("Informe ao menos uma extensão permitida para selecionar um arquivo.", nameof(allowedExtensions));

        var joined = string.Join(";", normalized);
        return $"Arquivos permitidos ({string.Join(", ", normalized)})|{joined}";
    }

    private static Task<T> RunStaAsync<T>(Func<T> action)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                tcs.SetResult(action());
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        })
        {
            IsBackground = true
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return tcs.Task;
    }
}
