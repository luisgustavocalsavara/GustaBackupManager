using System.ComponentModel;
using System.Runtime.InteropServices;
using GustaBackupManager.App.Interfaces;
using GustaBackupManager.App.Models;
using GustaBackupManager.Models.Entities;
using GustaBackupManager.Models.Interfaces;

namespace GustaBackupManager.Adapters.WinForms;

public sealed class WindowsSmbConnectionService(ISettingsService settingsService) : ISmbConnectionService
{
    private const int RESOURCETYPE_DISK = 0x00000001;
    private const int NO_ERROR = 0;
    private const int ERROR_SESSION_CREDENTIAL_CONFLICT = 1219;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NETRESOURCE
    {
        public int dwScope;
        public int dwType;
        public int dwDisplayType;
        public int dwUsage;
        public string? lpLocalName;
        public string? lpRemoteName;
        public string? lpComment;
        public string? lpProvider;
    }

    [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
    private static extern int WNetAddConnection2W(
        ref NETRESOURCE netResource,
        string? password,
        string? username,
        int flags);

    [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
    private static extern int WNetCancelConnection2W(
        string name,
        int flags,
        bool force);

    private readonly SemaphoreSlim _sync = new(1, 1);
    private string? _activeUncShare;

    public SmbConnectionStatus LastStatus { get; private set; } =
        new(false, false, string.Empty, "Servidor SMB ainda não configurado.");

    public event Action? StatusChanged;

    public string ProtectSecret(string plainText) => WindowsDataProtector.Protect(plainText);

    public string UnprotectSecret(string protectedBase64) => WindowsDataProtector.Unprotect(protectedBase64);

    public async Task<SmbConnectionStatus> EnsureConnectedAsync()
    {
        var current = settingsService.Current.Smb;
        if (string.IsNullOrWhiteSpace(current.RootPath))
        {
            return SetStatus(new SmbConnectionStatus(
                false,
                false,
                string.Empty,
                "Nenhum caminho SMB configurado. Acesse 'Configuração SMB' para definir o servidor de arquivos."));
        }

        if (LastStatus.IsConnected &&
            string.Equals(LastStatus.RootPath, current.RootPath.Trim(), StringComparison.OrdinalIgnoreCase) &&
            Directory.Exists(current.RootPath.Trim()))
        {
            return LastStatus;
        }

        return await ConnectAndVerifyAsync(current);
    }

    public async Task<SmbConnectionStatus> ConnectAndVerifyAsync(SmbConnectionSettings settings, string? plainPasswordOverride = null)
    {
        await _sync.WaitAsync();
        try
        {
            var rootPath = (settings.RootPath ?? string.Empty).Trim().TrimEnd('\\');
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                return SetStatus(new SmbConnectionStatus(
                    false,
                    false,
                    string.Empty,
                    @"Informe o caminho UNC do servidor SMB (ex.: \\192.168.1.50\Backups) ou uma pasta local."));
            }

            var isUnc = rootPath.StartsWith(@"\\", StringComparison.Ordinal);

            return await Task.Run(() =>
            {
                if (isUnc && !settings.UseIntegratedAuth && !string.IsNullOrWhiteSpace(settings.Username))
                {
                    var shareRoot = ExtractUncShareRoot(rootPath);
                    var password = plainPasswordOverride ?? UnprotectSecret(settings.ProtectedPassword);
                    var fullUser = string.IsNullOrWhiteSpace(settings.Domain)
                        ? settings.Username.Trim()
                        : $"{settings.Domain.Trim()}\\{settings.Username.Trim()}";

                    var netResource = new NETRESOURCE
                    {
                        dwType = RESOURCETYPE_DISK,
                        lpRemoteName = shareRoot
                    };

                    var errorCode = WNetAddConnection2W(ref netResource, password, fullUser, 0);
                    if (errorCode == ERROR_SESSION_CREDENTIAL_CONFLICT)
                    {
                        // Tenta encerrar sessao anterior e reconectar com as novas credenciais
                        _ = WNetCancelConnection2W(shareRoot, 0, true);
                        errorCode = WNetAddConnection2W(ref netResource, password, fullUser, 0);
                    }

                    if (errorCode != NO_ERROR)
                    {
                        var win32Msg = new Win32Exception(errorCode).Message;
                        return SetStatus(new SmbConnectionStatus(
                            false,
                            true,
                            rootPath,
                            $"Falha ao autenticar no compartilhamento SMB ({shareRoot}): [{errorCode}] {win32Msg}",
                            errorCode));
                    }

                    _activeUncShare = shareRoot;
                }

                try
                {
                    if (!Directory.Exists(rootPath))
                    {
                        if (!isUnc)
                        {
                            Directory.CreateDirectory(rootPath);
                        }
                        else
                        {
                            return SetStatus(new SmbConnectionStatus(
                                false,
                                true,
                                rootPath,
                                $"Compartilhamento ou pasta não encontrada no servidor: {rootPath}"));
                        }
                    }

                    // Valida permissao real de leitura enumerando entradas
                    _ = Directory.EnumerateFileSystemEntries(rootPath).Take(1).ToList();

                    var modeText = isUnc ? "Servidor SMB conectado" : "Diretório local conectado";
                    return SetStatus(new SmbConnectionStatus(
                        true,
                        isUnc,
                        rootPath,
                        $"{modeText} e acessível: {rootPath}"));
                }
                catch (Exception ex)
                {
                    return SetStatus(new SmbConnectionStatus(
                        false,
                        isUnc,
                        rootPath,
                        $"Não foi possível acessar '{rootPath}': {ex.Message}"));
                }
            });
        }
        finally
        {
            _sync.Release();
        }
    }

    public async Task<OperationResult> DisconnectAsync(string? rootPath = null)
    {
        await _sync.WaitAsync();
        try
        {
            var target = !string.IsNullOrWhiteSpace(rootPath)
                ? ExtractUncShareRoot(rootPath.Trim())
                : _activeUncShare;

            if (!string.IsNullOrWhiteSpace(target) && target.StartsWith(@"\\", StringComparison.Ordinal))
            {
                var code = WNetCancelConnection2W(target, 0, true);
                _activeUncShare = null;

                SetStatus(new SmbConnectionStatus(false, true, target, "Sessão SMB encerrada."));

                if (code != NO_ERROR && code != 2250) // 2250 = ERROR_NOT_CONNECTED
                {
                    return new OperationResult(false, $"Aviso ao desconectar SMB [{code}]: {new Win32Exception(code).Message}");
                }
            }
            else
            {
                SetStatus(new SmbConnectionStatus(false, false, string.Empty, "Desconectado."));
            }

            return new OperationResult(true, "Sessão SMB desconectada.");
        }
        finally
        {
            _sync.Release();
        }
    }

    private SmbConnectionStatus SetStatus(SmbConnectionStatus status)
    {
        LastStatus = status;
        StatusChanged?.Invoke();
        return status;
    }

    private static string ExtractUncShareRoot(string uncPath)
    {
        // Converte \\servidor\compartilhamento\subpasta -> \\servidor\compartilhamento
        var parts = uncPath.TrimStart('\\').Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
            return $@"\\{parts[0]}\{parts[1]}";

        return uncPath;
    }
}
