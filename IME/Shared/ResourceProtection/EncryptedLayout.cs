using System;
using Android.Content;
using Android.Views;

namespace IME.Shared.ResourceProtection;

internal static class EncryptedLayout
{
    // Compatibility mode: Release keeps plaintext layouts to avoid runtime
    // inflate failures on Android/MIUI while other protections stay enabled.
    internal static View Inflate(Context context, string assetPath, ViewGroup? parent = null, bool attachToRoot = false)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        return Inflate(LayoutInflater.From(context), assetPath, parent, attachToRoot);
    }

    internal static View Inflate(Context context, string assetPath, int fallbackLayoutId, ViewGroup? parent = null, bool attachToRoot = false)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        return Inflate(LayoutInflater.From(context), assetPath, fallbackLayoutId, parent, attachToRoot);
    }

    internal static View Inflate(LayoutInflater inflater, string assetPath, ViewGroup? parent = null, bool attachToRoot = false)
    {
        if (inflater == null)
        {
            throw new ArgumentNullException(nameof(inflater));
        }

        int layoutId = ResolveLayoutId(inflater.Context, assetPath);
        if (layoutId == 0)
        {
            throw new InvalidOperationException("No plaintext layout resource was found.");
        }

        return inflater.Inflate(layoutId, parent, attachToRoot);
    }

    internal static View Inflate(LayoutInflater inflater, string assetPath, int fallbackLayoutId, ViewGroup? parent = null, bool attachToRoot = false)
    {
        if (inflater == null)
        {
            throw new ArgumentNullException(nameof(inflater));
        }

        int layoutId = fallbackLayoutId != 0
            ? fallbackLayoutId
            : ResolveLayoutId(inflater.Context, assetPath);

        if (layoutId == 0)
        {
            throw new InvalidOperationException("No plaintext layout resource was found.");
        }

        return inflater.Inflate(layoutId, parent, attachToRoot);
    }

    private static int ResolveLayoutId(Context context, string assetPath)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return 0;
        }

        string normalized = assetPath.Replace('\\', '/').TrimStart('/');
        if (normalized.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^4];
        }

        if (normalized.StartsWith("layout/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["layout/".Length..];
        }

        if (normalized.Length == 0)
        {
            return 0;
        }

        return context.Resources?.GetIdentifier(normalized, "layout", context.PackageName) ?? 0;
    }
}
