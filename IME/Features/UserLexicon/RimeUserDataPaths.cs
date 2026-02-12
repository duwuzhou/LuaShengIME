using System;
using System.IO;
using Android.Content;
using IME.Shared.InputEngine;

namespace IME.Features.UserLexicon;

internal static class RimeUserDataPaths
{
    internal static string GetUserDataDir(Context context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        string dir = RimeNativeBindings.GetUserDataDir();
        if (!IsValidRootedPath(dir))
        {
            dir = Path.Combine(context.FilesDir.AbsolutePath, "rime", "user");
        }

        return dir;
    }

    internal static string GetSyncDir(Context context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        string dir = RimeNativeBindings.GetSyncDir();
        if (!IsValidRootedPath(dir))
        {
            dir = Path.Combine(context.FilesDir.AbsolutePath, "rime", "sync");
        }

        return dir;
    }

    internal static string EnsureUserDir(Context context)
    {
        string dir = GetUserDataDir(context);
        try
        {
            Directory.CreateDirectory(dir);
            return dir;
        }
        catch
        {
            string fallback = GetDefaultUserDir(context);
            Directory.CreateDirectory(fallback);
            return fallback;
        }
    }

    internal static string GetDefaultUserDir(Context context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        string fallback = Path.Combine(context.FilesDir.AbsolutePath, "rime", "user");
        Directory.CreateDirectory(fallback);
        return fallback;
    }

    private static bool IsValidRootedPath(string dir)
    {
        if (string.IsNullOrWhiteSpace(dir))
        {
            return false;
        }

        string trimmed = dir.Trim();
        if (trimmed == "/" || trimmed == "\\")
        {
            return false;
        }

        return Path.IsPathRooted(trimmed);
    }
}
