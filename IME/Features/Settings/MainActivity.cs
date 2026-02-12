using Android.App;
using System.Collections.Generic;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using AndroidEnvironment = Android.OS.Environment;
using AndroidSettings = Android.Provider.Settings;
using AndroidUri = Android.Net.Uri;

namespace IME.Features.Settings;

[Activity(
    Label = "@string/app_name",
    MainLauncher = true,
    Theme = "@style/MyNoActionBarTheme"
)]
public class MainActivity : Activity
{
    private const int RequestLegacyStorage = 1001;
    private const int RequestManageAllFiles = 1002;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window?.AddFlags(WindowManagerFlags.Fullscreen);

        SetContentView(Resource.Layout.activity_main);

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

        FindViewById<View>(Resource.Id.btn_get_ime)!.Click += (sender, e) => RequestStorageAccess();
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