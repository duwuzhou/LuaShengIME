using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Android.Content;
using Android.Util;

namespace IME.Features.UserLexicon;

internal static class RimeConfigPatcher
{
    private const string Tag = "RimeConfigPatcher";
    private const string PageSizePatchHeader = "patch:\n  menu/page_size: ";

    internal static bool TryWritePageSizePatch(Context context, int pageSize)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (pageSize <= 0)
        {
            return false;
        }

        try
        {
            string userDir = RimeUserDataPaths.GetDefaultUserDir(context);
            string customPath = Path.Combine(userDir, "default.custom.yaml");
            string content;

            if (File.Exists(customPath))
            {
                string existing = File.ReadAllText(customPath, Encoding.UTF8);
                content = UpsertPageSizePatch(existing, pageSize);
                if (string.Equals(existing, content, StringComparison.Ordinal))
                {
                    return false;
                }
            }
            else
            {
                content = $"{PageSizePatchHeader}{pageSize}\n";
            }

            File.WriteAllText(customPath, content, new UTF8Encoding(false));
            return true;
        }
        catch (Exception ex)
        {
            Log.Warn(Tag, $"Write page size patch failed: {ex.Message}");
            return false;
        }
    }

    internal static string UpsertPageSizePatch(string existing, int pageSize)
    {
        if (string.IsNullOrEmpty(existing))
        {
            return $"{PageSizePatchHeader}{pageSize}\n";
        }

        string normalized = existing.Replace("\r\n", "\n");
        var lines = new List<string>(normalized.Split('\n'));

        int menuLineIndex = -1;
        int patchLineIndex = -1;

        for (int i = 0; i < lines.Count; i++)
        {
            string trimmed = lines[i].TrimStart();
            if (trimmed.StartsWith("patch:", StringComparison.Ordinal))
            {
                patchLineIndex = i;
            }
            if (trimmed.StartsWith("menu/page_size:", StringComparison.Ordinal))
            {
                menuLineIndex = i;
                break;
            }
        }

        if (menuLineIndex >= 0)
        {
            string indent = lines[menuLineIndex].Substring(0, lines[menuLineIndex].Length - lines[menuLineIndex].TrimStart().Length);
            if (indent.Length == 0)
                indent = "  ";
            lines[menuLineIndex] = $"{indent}menu/page_size: {pageSize}";
        }
        else if (patchLineIndex >= 0)
        {
            lines.Insert(patchLineIndex + 1, $"  menu/page_size: {pageSize}");
        }
        else
        {
            if (lines.Count > 0 && lines[^1].Length != 0)
                lines.Add(string.Empty);
            lines.Add("patch:");
            lines.Add($"  menu/page_size: {pageSize}");
        }

        return string.Join("\n", lines).TrimEnd() + "\n";
    }
}
