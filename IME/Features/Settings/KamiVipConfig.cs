using System;
using System.Globalization;
using Android.Content;
using AndroidSettings = Android.Provider.Settings;

namespace IME.Features.Settings;

public static class KamiVipConfig
{
    public const string PrefsName = "ime_settings";
    public const string KeyKamiVipExpiresAt = "key_kami_vip_expires_at";
    public const string KeyKamiUserId = "key_kami_user_id";
    public const string KeyKamiDeviceUid = "key_kami_device_uid";
    public const string KeyKamiLastVerifyAtUtc = "key_kami_last_verify_at_utc";

    public const bool AllowUntrustedTlsForKamiHost = true;
    public const string KamiBaseUrl = "https://km.moluhualuo.top";
    public const string RedeemEndpointPath = "/api/redeem-code";
    public const string VerifyEndpointPath = "/api/verify-vip";

    public static readonly TimeSpan VerifyInterval = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan VerifyTimeout = TimeSpan.FromSeconds(20);

    public const string CustomPredictionRestrictedMessage = "自定义预测词需要卡密会员";
    public const string CustomShortcutRestrictedMessage = "自定义快捷短语需要卡密会员";
    public const string UserLexiconImportRestrictedMessage = "用户词库导入需要卡密会员";
    public const string UserLexiconLearningRestrictedMessage = "用户词库学习需要卡密会员";

    public static bool IsVipActive(Context context)
    {
        string vipExpiresAt = GetStoredVipExpiresAt(context);
        if (string.IsNullOrWhiteSpace(vipExpiresAt))
        {
            return false;
        }

        return TryParseVipExpiry(vipExpiresAt, out DateTimeOffset expiresAt)
               && expiresAt > DateTimeOffset.UtcNow;
    }

    public static bool HasStoredBinding(Context context)
    {
        return GetStoredUserId(context) > 0 && !string.IsNullOrWhiteSpace(GetStoredDeviceUid(context));
    }

    public static bool CanUseCustomPrediction(Context context) => IsVipActive(context);

    public static bool CanUseCustomShortcuts(Context context) => IsVipActive(context);

    public static bool CanImportUserLexicon(Context context) => IsVipActive(context);

    public static bool CanLearnUserLexicon(Context context) => IsVipActive(context);

    public static int GetStoredUserId(Context context)
    {
        return GetPrefs(context).GetInt(KeyKamiUserId, 0);
    }

    public static string GetStoredDeviceUid(Context context)
    {
        return GetPrefs(context).GetString(KeyKamiDeviceUid, string.Empty) ?? string.Empty;
    }

    public static string GetStoredVipExpiresAt(Context context)
    {
        return GetPrefs(context).GetString(KeyKamiVipExpiresAt, string.Empty) ?? string.Empty;
    }

    public static DateTimeOffset? GetLastVerifyAtUtc(Context context)
    {
        string raw = GetPrefs(context).GetString(KeyKamiLastVerifyAtUtc, string.Empty) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (!DateTimeOffset.TryParse(
                raw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset value))
        {
            return null;
        }

        return value;
    }

    public static void SaveVipState(Context context, int userId, string vipExpiresAt)
    {
        using var editor = GetPrefs(context).Edit();
        editor.PutInt(KeyKamiUserId, userId);
        editor.PutString(KeyKamiVipExpiresAt, vipExpiresAt ?? string.Empty);
        editor.Apply();
    }

    public static void ClearVipState(Context context, bool preserveDeviceUid = true)
    {
        string existingDeviceUid = preserveDeviceUid ? GetStoredDeviceUid(context) : string.Empty;

        using var editor = GetPrefs(context).Edit();
        editor.Remove(KeyKamiUserId);
        editor.Remove(KeyKamiVipExpiresAt);
        editor.Remove(KeyKamiLastVerifyAtUtc);

        if (!preserveDeviceUid)
        {
            editor.Remove(KeyKamiDeviceUid);
        }
        else if (!string.IsNullOrWhiteSpace(existingDeviceUid))
        {
            editor.PutString(KeyKamiDeviceUid, existingDeviceUid);
        }

        editor.Apply();
    }

    public static void MarkVerifiedNow(Context context)
    {
        using var editor = GetPrefs(context).Edit();
        editor.PutString(KeyKamiLastVerifyAtUtc, DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        editor.Apply();
    }

    public static string GetOrCreateDeviceUid(Context context)
    {
        string existing = GetStoredDeviceUid(context);
        if (!string.IsNullOrWhiteSpace(existing))
        {
            return existing;
        }

        string androidId = AndroidSettings.Secure.GetString(
            context.ContentResolver,
            AndroidSettings.Secure.AndroidId
        ) ?? string.Empty;

        if (string.IsNullOrWhiteSpace(androidId))
        {
            androidId = $"ime-{Guid.NewGuid():N}";
        }

        using var editor = GetPrefs(context).Edit();
        editor.PutString(KeyKamiDeviceUid, androidId);
        editor.Apply();
        return androidId;
    }

    public static string BuildUserRef(Context context)
    {
        return $"{context.PackageName}:{GetOrCreateDeviceUid(context)}";
    }

    public static string NormalizeBaseUrl(string raw)
    {
        return (raw ?? string.Empty).Trim().TrimEnd('/');
    }

    public static bool TryParseVipExpiry(string raw, out DateTimeOffset expiresAt)
    {
        return DateTimeOffset.TryParse(
            raw,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out expiresAt);
    }

    private static ISharedPreferences GetPrefs(Context context)
    {
        return context.GetSharedPreferences(PrefsName, FileCreationMode.Private)!;
    }
}
