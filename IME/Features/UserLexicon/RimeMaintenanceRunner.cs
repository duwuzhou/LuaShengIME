using System;
using System.IO;
using System.Text;
using Android.Content;
using Android.Util;
using IME.Shared.InputEngine;

namespace IME.Features.UserLexicon;

internal static class RimeMaintenanceRunner
{
    private const string Tag = "RimeMaintenance";

    public static RimeApplyResult DeployAndSync(Context context)
    {
        if (!EnsureInitialized(context, out string initError))
        {
            return new RimeApplyResult
            {
                Success = false,
                Error = initError
            };
        }

        try
        {
            bool started = RimeNativeBindings.StartMaintenance(true);
            bool maintenance = RimeNativeBindings.IsMaintenanceMode();
            bool deployed = RimeNativeBindings.Deploy();
            RimeNativeBindings.JoinMaintenanceThread();
            bool synced = RimeNativeBindings.SyncUserData();
            bool success = synced && (deployed || !maintenance || !started);
            return new RimeApplyResult
            {
                Success = success,
                Error = success ? null : "Deploy or Sync failed"
            };
        }
        catch (Exception ex)
        {
            Log.Error(Tag, $"DeployAndSync failed: {ex.Message}");
            return new RimeApplyResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    private static bool EnsureInitialized(Context context, out string error)
    {
        error = string.Empty;

        if (!RimeNativeBindings.InitializeApi())
        {
            error = "Rime API unavailable";
            return false;
        }

        try
        {
            string sharedDir = Path.Combine(context.FilesDir.AbsolutePath, "rime", "shared");
            string userDir = Path.Combine(context.FilesDir.AbsolutePath, "rime", "user");
            Directory.CreateDirectory(sharedDir);
            Directory.CreateDirectory(userDir);

            EnsureAssetsCopied(context, sharedDir);
            EnsureUserConfig(context);

            IntPtr traitsPtr = RimeTraitsHelper.CreateTraits(sharedDir, userDir);
            try
            {
                RimeNativeBindings.Setup(traitsPtr);
                RimeNativeBindings.RimeInitialize(traitsPtr);
            }
            finally
            {
                RimeTraitsHelper.FreeTraits(traitsPtr);
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            Log.Error(Tag, $"EnsureInitialized failed: {ex.Message}");
            return false;
        }
    }

    private static void EnsureAssetsCopied(Context context, string sharedDir)
    {
        try
        {
            string versionFile = Path.Combine(sharedDir, ".assets_version");
            string currentVersion = GetAppVersionName(context);

            if (File.Exists(versionFile))
            {
                string savedVersion = File.ReadAllText(versionFile, Encoding.UTF8).Trim();
                if (savedVersion == currentVersion)
                {
                    string openccDir = Path.Combine(sharedDir, "opencc");
                    if (Directory.Exists(openccDir))
                    {
                        return;
                    }
                }
            }

            CopyAssetDirectory(context, "rime", sharedDir);
            File.WriteAllText(versionFile, currentVersion, new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            Log.Warn(Tag, $"EnsureAssetsCopied failed: {ex.Message}");
        }
    }

    private static void EnsureUserConfig(Context context)
    {
        RimeUserLexiconStore.EnsureSchemaPatch(context);
        bool pageSizeChanged = EnsurePageSizePatch(context);
        if (pageSizeChanged)
        {
            TryCleanUserBuild(context);
        }
    }

    private static bool EnsurePageSizePatch(Context context)
    {
        try
        {
            int pageSize = IME.Features.Settings.SettingsActivity.GetCandidatePageSize(context);
            if (pageSize <= 0)
            {
                return false;
            }

            return RimeConfigPatcher.TryWritePageSizePatch(context, pageSize);
        }
        catch (Exception ex)
        {
            Log.Warn(Tag, $"EnsurePageSizePatch failed: {ex.Message}");
            return false;
        }
    }

    private static void TryCleanUserBuild(Context context)
    {
        try
        {
            string buildDir = Path.Combine(context.FilesDir.AbsolutePath, "rime", "user", "build");
            if (Directory.Exists(buildDir))
            {
                Directory.Delete(buildDir, true);
            }
        }
        catch (Exception ex)
        {
            Log.Warn(Tag, $"TryCleanUserBuild failed: {ex.Message}");
        }
    }

    private static void CopyAssetDirectory(Context context, string assetPath, string destPath)
    {
        Directory.CreateDirectory(destPath);
        string[] items = context.Assets.List(assetPath);
        if (items == null) return;

        foreach (string item in items)
        {
            string assetItemPath = $"{assetPath}/{item}";
            string destItemPath = Path.Combine(destPath, item);

            string[] subItems = context.Assets.List(assetItemPath);
            if (subItems != null && subItems.Length > 0)
            {
                CopyAssetDirectory(context, assetItemPath, destItemPath);
            }
            else
            {
                CopyAssetFile(context, assetItemPath, destItemPath);
            }
        }
    }

    private static void CopyAssetFile(Context context, string assetPath, string destPath)
    {
        try
        {
            if (File.Exists(destPath))
            {
                return;
            }

            using var input = context.Assets.Open(assetPath);
            using var output = File.Create(destPath);
            input.CopyTo(output);
        }
        catch (Exception ex)
        {
            Log.Warn(Tag, $"Copy asset failed {assetPath}: {ex.Message}");
        }
    }

    private static string GetAppVersionName(Context context)
    {
        try
        {
            var packageInfo = context.PackageManager.GetPackageInfo(context.PackageName, 0);
            return packageInfo?.VersionName ?? "1.0.0";
        }
        catch
        {
            return "1.0.0";
        }
    }

}

