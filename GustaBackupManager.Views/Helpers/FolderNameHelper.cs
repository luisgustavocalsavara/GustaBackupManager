using System.Text;

namespace GustaBackupManager.Views.Helpers;

public static class FolderNameHelper
{
    public static string SanitizeSegment(string? raw)
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

    public static string ComposePrefix(string? parentPrefix, string? shortName, string? fullName)
    {
        var chosen = !string.IsNullOrWhiteSpace(shortName) ? shortName : fullName;
        var segment = SanitizeSegment(chosen);

        if (string.IsNullOrWhiteSpace(parentPrefix))
            return segment;

        return string.IsNullOrWhiteSpace(segment)
            ? parentPrefix
            : $"{parentPrefix}_{segment}";
    }

    public static string FormatPreviewFileName(string? prefix, string? extension, DateTime? referenceTime = null)
    {
        var stamp = (referenceTime ?? DateTime.Now).ToString("yyyyMMdd_HHmmss");
        var ext = string.IsNullOrWhiteSpace(extension)
            ? ".ext"
            : extension.StartsWith('.') ? extension.ToLowerInvariant() : "." + extension.ToLowerInvariant();

        return string.IsNullOrWhiteSpace(prefix)
            ? $"{stamp}{ext}"
            : $"{prefix}_{stamp}{ext}";
    }
}
