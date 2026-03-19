using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Util;
namespace IME.Shared.Security;

public sealed class SecurityCheckResult
{
    public static SecurityCheckResult Allowed { get; } = new(true, Array.Empty<string>());

    public SecurityCheckResult(bool isAllowed, IReadOnlyList<string> reasons)
    {
        IsAllowed = isAllowed;
        Reasons = reasons;
    }

    public bool IsAllowed { get; }

    public IReadOnlyList<string> Reasons { get; }

    public string DisplayMessage => string.Join(System.Environment.NewLine, Reasons);
}

internal static class AppSecurityGuard
{
    private const string Tag = "AppSecurityGuard";
    private const string ExpectedPackageName = "com.hualuo.luanshenIME";
    private const string ExpectedSignatureSha256 = "DC4C39302EDD050B338C28DD4F1BB1A6BA23702191A885337AB03501F6C91B38";

    private static readonly string[] SuspiciousPackageNames =
    {
        "com.guoshi.httpcanary",
        "com.guoshi.httpcanary.premium",
        "com.httpcanary.pro",
        "app.greyshirts.sslcapture",
        "com.minhui.networkcapture",
        "com.charles.proxy",
        "com.telerik.fiddler",
        "tech.httptoolkit.android.v1",
        "com.reqable.android"
    };

    public static SecurityCheckResult Check(Context context)
    {
#if DEBUG
        return SecurityCheckResult.Allowed;
#else
        var reasons = new List<string>();

        try
        {
            if (context == null)
            {
                reasons.Add("运行环境无效");
                return new SecurityCheckResult(false, reasons);
            }

            ValidatePackageIdentity(context, reasons);
            ValidateSignature(context, reasons);
            ValidateDebugger(context, reasons);
            ValidatePacketCaptureEnvironment(context, reasons);
        }
        catch (System.Exception ex)
        {
            reasons.Add("安全校验执行异常");
            Log.Warn(Tag, $"Security check failed unexpectedly: {ex}");
        }

        var result = reasons.Count == 0
            ? SecurityCheckResult.Allowed
            : new SecurityCheckResult(false, reasons.Distinct(StringComparer.Ordinal).ToArray());

        LogResult(result);
        return result;
#endif
    }

    private static void ValidatePackageIdentity(Context context, List<string> reasons)
    {
        string runtimePackageName = context.PackageName ?? string.Empty;
        string appInfoPackageName = context.ApplicationInfo?.PackageName ?? string.Empty;

        if (!string.Equals(runtimePackageName, ExpectedPackageName, StringComparison.Ordinal) ||
            !string.Equals(appInfoPackageName, ExpectedPackageName, StringComparison.Ordinal))
        {
            reasons.Add("检测到包名异常");
            return;
        }

        PackageInfo? packageInfo = GetPackageInfo(context, runtimePackageName, 0);
        if (!string.Equals(packageInfo?.PackageName, ExpectedPackageName, StringComparison.Ordinal))
        {
            reasons.Add("检测到安装包标识异常");
        }
    }

    private static void ValidateSignature(Context context, List<string> reasons)
    {
        string? signatureSha256 = GetInstalledSignatureSha256(context);
        if (string.IsNullOrWhiteSpace(signatureSha256) ||
            !string.Equals(signatureSha256, ExpectedSignatureSha256, StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("检测到签名异常");
        }
    }

    private static void ValidateDebugger(Context context, List<string> reasons)
    {
        bool isDebuggable =
            (context.ApplicationInfo?.Flags & ApplicationInfoFlags.Debuggable) == ApplicationInfoFlags.Debuggable;

        if (System.Diagnostics.Debugger.IsAttached ||
            Android.OS.Debug.IsDebuggerConnected ||
            Android.OS.Debug.WaitingForDebugger() ||
            isDebuggable ||
            HasTracerPid())
        {
            reasons.Add("检测到调试器或可调试环境");
        }
    }

    private static void ValidatePacketCaptureEnvironment(Context context, List<string> reasons)
    {
        if (HasProxyConfigured())
        {
            reasons.Add("检测到系统代理");
        }

        if (HasTunnelInterface())
        {
            reasons.Add("检测到 VPN 或隧道网络");
        }

        if (HasSuspiciousAppsInstalled(context))
        {
            reasons.Add("检测到抓包工具");
        }
    }

    private static PackageInfo? GetPackageInfo(Context context, string packageName, PackageInfoFlags flags)
    {
        try
        {
            return context.PackageManager?.GetPackageInfo(packageName, flags);
        }
        catch
        {
            return null;
        }
    }

    private static string? GetInstalledSignatureSha256(Context context)
    {
        PackageInfoFlags flags =
            Build.VERSION.SdkInt >= BuildVersionCodes.P
                ? PackageInfoFlags.SigningCertificates
                : PackageInfoFlags.Signatures;

        PackageInfo? packageInfo = GetPackageInfo(context, context.PackageName ?? ExpectedPackageName, flags);
        if (packageInfo == null)
        {
            return null;
        }

        Signature? signature = null;

        if (Build.VERSION.SdkInt >= BuildVersionCodes.P && packageInfo.SigningInfo != null)
        {
            Signature[]? signatures = packageInfo.SigningInfo.HasMultipleSigners
                ? packageInfo.SigningInfo.GetApkContentsSigners()
                : packageInfo.SigningInfo.GetSigningCertificateHistory();

            signature = signatures?.FirstOrDefault();
        }

#pragma warning disable CS0618
        signature ??= packageInfo.Signatures?.FirstOrDefault();
#pragma warning restore CS0618

        if (signature == null)
        {
            return null;
        }

        using var sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(signature.ToByteArray());
        return Convert.ToHexString(hash);
    }

    private static bool HasProxyConfigured()
    {
        string? httpProxyHost = Java.Lang.JavaSystem.GetProperty("http.proxyHost");
        string? httpsProxyHost = Java.Lang.JavaSystem.GetProperty("https.proxyHost");
        string? httpProxyPort = Java.Lang.JavaSystem.GetProperty("http.proxyPort");
        string? httpsProxyPort = Java.Lang.JavaSystem.GetProperty("https.proxyPort");

        return !string.IsNullOrWhiteSpace(httpProxyHost) ||
               !string.IsNullOrWhiteSpace(httpsProxyHost) ||
               (!string.IsNullOrWhiteSpace(httpProxyPort) && httpProxyPort != "-1") ||
               (!string.IsNullOrWhiteSpace(httpsProxyPort) && httpsProxyPort != "-1");
    }

    private static bool HasTunnelInterface()
    {
        try
        {
            foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                string name = networkInterface.Name ?? string.Empty;
                string description = networkInterface.Description ?? string.Empty;
                string combined = (name + " " + description).ToLowerInvariant();

                if (combined.Contains("tun") ||
                    combined.Contains("ppp") ||
                    combined.Contains("tap") ||
                    combined.Contains("wg"))
                {
                    return true;
                }
            }
        }
        catch (System.Exception ex)
        {
            Log.Warn(Tag, $"Tunnel interface check failed: {ex.Message}");
        }

        return false;
    }

    private static bool HasSuspiciousAppsInstalled(Context context)
    {
        if (context.PackageManager == null)
        {
            return false;
        }

        foreach (string packageName in SuspiciousPackageNames)
        {
            try
            {
                PackageInfo? packageInfo = context.PackageManager.GetPackageInfo(packageName, 0);
                if (packageInfo != null)
                {
                    return true;
                }
            }
            catch
            {
                // Ignore package lookup failures.
            }
        }

        return false;
    }

    private static bool HasTracerPid()
    {
        try
        {
            const string statusPath = "/proc/self/status";
            if (!File.Exists(statusPath))
            {
                return false;
            }

            foreach (string line in File.ReadLines(statusPath))
            {
                if (!line.StartsWith("TracerPid:", StringComparison.Ordinal))
                {
                    continue;
                }

                string value = line.Substring("TracerPid:".Length).Trim();
                if (int.TryParse(value, out int pid))
                {
                    return pid > 0;
                }
            }
        }
        catch (System.Exception ex)
        {
            Log.Warn(Tag, $"TracerPid check failed: {ex.Message}");
        }

        return false;
    }

    private static void LogResult(SecurityCheckResult result)
    {
        if (result.IsAllowed)
        {
            Log.Info(Tag, "Security check passed.");
            return;
        }

        Log.Warn(Tag, $"Security check blocked app startup: {result.DisplayMessage}");
    }
}
