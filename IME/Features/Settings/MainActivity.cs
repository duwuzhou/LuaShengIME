using Android.App;
using System.Collections.Generic;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using IME.Shared.ResourceProtection;
using IME.Shared.Security;
using AndroidEnvironment = Android.OS.Environment;
using AndroidSettings = Android.Provider.Settings;
using AndroidUri = Android.Net.Uri;

namespace IME.Features.Settings;

[Activity(
    Label = "@string/app_name",
    MainLauncher = true,
    Theme = "@style/MyNoActionBarTheme"
)]
public class MainActivity : SecurityMonitoredActivity
{
    private const int RequestLegacyStorage = 1001;
    private const int RequestManageAllFiles = 1002;

    protected override void OnResume()
    {
        base.OnResume();
        KamiVipVerificationCoordinator.Start(this, nameof(MainActivity));
    }

    protected override void OnPause()
    {
        KamiVipVerificationCoordinator.Stop();
        base.OnPause();
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window?.AddFlags(WindowManagerFlags.Fullscreen);

#if DEBUG
        SetContentView(Resource.Layout.activity_main);
#else
        SetContentView(EncryptedLayout.Inflate(this, "layout/activity_main", Resource.Layout.activity_main));
#endif

        var securityResult = AppSecurityGuard.Check(this);
        if (!securityResult.IsAllowed)
        {
            ApplySecurityBlock(securityResult);
            return;
        }

        FindViewById<View>(Resource.Id.btn_set_ime)!.Click += (sender, e) =>
        {
            var intent = new Intent(AndroidSettings.ActionInputMethodSettings);
            StartActivity(intent);
        };

        FindViewById<View>(Resource.Id.btn_settings)!.Click += (sender, e) =>
        {
            var intent = new Intent(this, typeof(SettingsActivity));
            StartActivity(intent);
        };

        FindViewById<View>(Resource.Id.btn_kami_vip)!.Click += (sender, e) =>
        {
            var intent = new Intent(this, typeof(KamiVipActivity));
            StartActivity(intent);
        };

        FindViewById<View>(Resource.Id.btn_get_ime)!.Click += (sender, e) => RequestStorageAccess();
        BindHomeMeta();
        BindOfficialWebsite();
    }

    private void BindHomeMeta()
    {
        var metaView = FindViewById(Resource.Id.tv_home_meta) as TextView;
        if (metaView == null)
        {
            return;
        }

        try
        {
            var packageInfo = PackageManager?.GetPackageInfo(PackageName!, 0);
            string versionName = packageInfo?.VersionName ?? "未知";
            metaView.Text = string.Format(GetString(Resource.String.home_footer_meta), versionName);
        }
        catch
        {
            metaView.Text = string.Format(GetString(Resource.String.home_footer_meta), "未知");
        }
    }

    private void BindOfficialWebsite()
    {
        var websiteView = FindViewById(Resource.Id.tv_home_website) as TextView;
        if (websiteView == null)
        {
            return;
        }

        websiteView.Click += (sender, e) =>
        {
            string url = GetString(Resource.String.home_official_website_url);
            var intent = new Intent(Intent.ActionView, AndroidUri.Parse(url));
            StartActivity(intent);
        };
    }

    private void ApplySecurityBlock(SecurityCheckResult result)
    {
        var titleView = FindViewById(Resource.Id.tv_home_title) as TextView;
        if (titleView != null)
        {
            titleView.Text = GetString(Resource.String.security_block_title);
        }

        var subtitleView = FindViewById(Resource.Id.tv_home_subtitle) as TextView;
        if (subtitleView != null)
        {
            string prefix = GetString(Resource.String.security_block_summary_prefix);
            subtitleView.Text = string.IsNullOrWhiteSpace(result.DisplayMessage)
                ? prefix
                : prefix + System.Environment.NewLine + result.DisplayMessage;
        }

        int[] blockedViewIds =
        {
            Resource.Id.btn_set_ime,
            Resource.Id.btn_settings,
            Resource.Id.btn_get_ime,
            Resource.Id.btn_kami_vip
        };

        foreach (int viewId in blockedViewIds)
        {
            var view = FindViewById<View>(viewId);
            if (view != null)
            {
                view.Visibility = ViewStates.Gone;
            }
        }

        var metaView = FindViewById(Resource.Id.tv_home_meta) as TextView;
        if (metaView != null)
        {
            metaView.Text = GetString(Resource.String.security_block_footer);
        }
    }

    protected override void HandleSecurityBlocked(SecurityCheckResult result)
    {
        ApplySecurityBlock(result);
    }

    private void RequestStorageAccess()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
        {
            if (AndroidEnvironment.IsExternalStorageManager)
            {
                ShowGrantedDialog();
                return;
            }

            ShowManageAllFilesDialog();
            return;
        }

        RequestLegacyStoragePermissions();
    }

    private void ShowManageAllFilesDialog()
    {
        new AlertDialog.Builder(this)
            .SetTitle("需要存储权限")
            .SetMessage("此功能需要“所有文件访问权限”，请在设置中授权。")
            .SetPositiveButton("去设置", (d, w) => OpenManageAllFilesSettings())
            .SetNegativeButton("取消", (d, w) => { })
            .Show();
    }

    private void OpenManageAllFilesSettings()
    {
        try
        {
            var intent = new Intent(AndroidSettings.ActionManageAppAllFilesAccessPermission);
            intent.SetData(AndroidUri.Parse("package:" + PackageName));
            StartActivityForResult(intent, RequestManageAllFiles);
        }
        catch
        {
            var intent = new Intent(AndroidSettings.ActionManageAllFilesAccessPermission);
            StartActivityForResult(intent, RequestManageAllFiles);
        }
    }

    private void RequestLegacyStoragePermissions()
    {
        var missing = new List<string>();
        var readPermission = Android.Manifest.Permission.ReadExternalStorage;
        var writePermission = Android.Manifest.Permission.WriteExternalStorage;

        if (ContextCompat.CheckSelfPermission(this, readPermission) != Permission.Granted)
        {
            missing.Add(readPermission);
        }

        if (ContextCompat.CheckSelfPermission(this, writePermission) != Permission.Granted)
        {
            missing.Add(writePermission);
        }

        if (missing.Count == 0)
        {
            ShowGrantedDialog();
            return;
        }

        if (ActivityCompat.ShouldShowRequestPermissionRationale(this, missing[0]))
        {
            new AlertDialog.Builder(this)
                .SetTitle("需要存储权限")
                .SetMessage("此功能需要访问存储空间，请授权。")
                .SetPositiveButton("确定", (d, w) => RequestPermissions(missing.ToArray(), RequestLegacyStorage))
                .Show();
        }
        else
        {
            RequestPermissions(missing.ToArray(), RequestLegacyStorage);
        }
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);

        if (requestCode != RequestLegacyStorage)
        {
            return;
        }

        bool granted = grantResults.Length > 0;
        for (int i = 0; i < grantResults.Length; i++)
        {
            if (grantResults[i] != Permission.Granted)
            {
                granted = false;
                break;
            }
        }

        if (granted)
        {
            ShowGrantedDialog();
            return;
        }

        if (permissions.Length > 0 && !ActivityCompat.ShouldShowRequestPermissionRationale(this, permissions[0]))
        {
            new AlertDialog.Builder(this)
                .SetTitle("权限被永久拒绝")
                .SetMessage("请在系统设置中手动开启存储权限。")
                .SetPositiveButton("去设置", (d, w) =>
                {
                    var intent = new Intent(AndroidSettings.ActionApplicationDetailsSettings);
                    intent.SetData(AndroidUri.Parse("package:" + PackageName));
                    StartActivity(intent);
                })
                .Show();
        }
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
    {
        base.OnActivityResult(requestCode, resultCode, data);

        if (requestCode == RequestManageAllFiles)
        {
            if (AndroidEnvironment.IsExternalStorageManager)
            {
                ShowGrantedDialog();
            }
            else
            {
                new AlertDialog.Builder(this)
                    .SetTitle("权限未授予")
                    .SetMessage("未获得所有文件访问权限。")
                    .SetPositiveButton("确定", (d, w) => { })
                    .Show();
            }
        }
    }

    private void ShowGrantedDialog()
    {
        new AlertDialog.Builder(this)
            .SetTitle("存储权限已授予")
            .SetMessage("您可以继续使用此功能。")
            .SetPositiveButton("确定", (d, w) => { })
            .Show();
    }
}
