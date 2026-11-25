using System.Runtime.InteropServices;
using System.Text;

namespace GustaBackupManager.Adapters.WinForms;

/// <summary>
/// Criptografia nativa Windows DPAPI (CurrentUser) via crypt32.dll sem dependencias externas.
/// </summary>
internal static class WindowsDataProtector
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DATA_BLOB
    {
        public int cbData;
        public nint pbData;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(
        ref DATA_BLOB pDataIn,
        string? szDataDescr,
        nint pOptionalEntropy,
        nint pvReserved,
        nint pPromptStruct,
        int dwFlags,
        out DATA_BLOB pDataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptUnprotectData(
        ref DATA_BLOB pDataIn,
        nint ppszDataDescr,
        nint pOptionalEntropy,
        nint pvReserved,
        nint pPromptStruct,
        int dwFlags,
        out DATA_BLOB pDataOut);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint LocalFree(nint hMem);

    public static string Protect(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        var bytes = Encoding.UTF8.GetBytes(plainText);
        var inBlob = new DATA_BLOB
        {
            cbData = bytes.Length,
            pbData = Marshal.AllocHGlobal(bytes.Length)
        };

        try
        {
            Marshal.Copy(bytes, 0, inBlob.pbData, bytes.Length);
            if (!CryptProtectData(ref inBlob, "GustaBackupSMB", nint.Zero, nint.Zero, nint.Zero, 0, out var outBlob))
                return string.Empty;

            try
            {
                var protectedBytes = new byte[outBlob.cbData];
                Marshal.Copy(outBlob.pbData, protectedBytes, 0, outBlob.cbData);
                return Convert.ToBase64String(protectedBytes);
            }
            finally
            {
                if (outBlob.pbData != nint.Zero)
                    LocalFree(outBlob.pbData);
            }
        }
        finally
        {
            if (inBlob.pbData != nint.Zero)
                Marshal.FreeHGlobal(inBlob.pbData);
        }
    }

    public static string Unprotect(string protectedBase64)
    {
        if (string.IsNullOrWhiteSpace(protectedBase64))
            return string.Empty;

        try
        {
            var bytes = Convert.FromBase64String(protectedBase64);
            var inBlob = new DATA_BLOB
            {
                cbData = bytes.Length,
                pbData = Marshal.AllocHGlobal(bytes.Length)
            };

            try
            {
                Marshal.Copy(bytes, 0, inBlob.pbData, bytes.Length);
                if (!CryptUnprotectData(ref inBlob, nint.Zero, nint.Zero, nint.Zero, nint.Zero, 0, out var outBlob))
                    return string.Empty;

                try
                {
                    var plainBytes = new byte[outBlob.cbData];
                    Marshal.Copy(outBlob.pbData, plainBytes, 0, outBlob.cbData);
                    return Encoding.UTF8.GetString(plainBytes);
                }
                finally
                {
                    if (outBlob.pbData != nint.Zero)
                        LocalFree(outBlob.pbData);
                }
            }
            finally
            {
                if (inBlob.pbData != nint.Zero)
                    Marshal.FreeHGlobal(inBlob.pbData);
            }
        }
        catch
        {
            return string.Empty;
        }
    }
}
