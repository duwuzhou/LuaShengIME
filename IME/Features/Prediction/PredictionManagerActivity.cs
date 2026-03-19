using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Net;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;
using IME.Features.UserLexicon;
using IME.Features.Settings;
using IME.Shared.ResourceProtection;
using IME.Shared.InputEngine;
using IME.Shared.Security;

namespace IME.Features.Prediction;

[Activity(Label = "预测词管理", Theme = "@style/MyNoActionBarTheme")]
public class PredictionManagerActivity : SecurityMonitoredActivity
{
    private const string Tag = "PredictionManager";
    private const int RequestImport = 2201;
    private const int RequestExport = 2202;
    private const int RequestExportRime = 2203;
    private const int RequestImportRime = 2204;

    protected override void OnResume()
    {
        base.OnResume();
        KamiVipVerificationCoordinator.Start(this, nameof(PredictionManagerActivity));
    }

    protected override void OnPause()
    {
        KamiVipVerificationCoordinator.Stop();
        base.OnPause();
    }

    private EditText _editPrev;
    private EditText _editNext;
    private Button _btnAdd;
    private Button _btnImport;
    private Button _btnExport;
    private Button _btnImportRime;
    private Button _btnExportRime;
    private LinearLayout _listContainer;
    private TextView _tvEmpty;

    private CustomPredictionStore _store;

    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        if (!EnsureSecurityAllowedNow())
        {
            return;
        }

        if (!KamiVipConfig.CanUseCustomPrediction(this))
        {
            Toast.MakeText(this, KamiVipConfig.CustomPredictionRestrictedMessage, ToastLength.Short)?.Show();
            Finish();
            return;
        }

#if DEBUG
        SetContentView(Resource.Layout.activity_prediction_manager);
#else
        SetContentView(EncryptedLayout.Inflate(this, "layout/activity_prediction_manager", Resource.Layout.activity_prediction_manager));
#endif

        _store = new CustomPredictionStore(this);

        _editPrev = FindViewById<EditText>(Resource.Id.edit_prediction_prev);
        _editNext = FindViewById<EditText>(Resource.Id.edit_prediction_next);
        _btnAdd = FindViewById<Button>(Resource.Id.btn_prediction_add);
        _btnImport = FindViewById<Button>(Resource.Id.btn_prediction_import);
        _btnExport = FindViewById<Button>(Resource.Id.btn_prediction_export);
        _btnImportRime = FindViewById<Button>(Resource.Id.btn_prediction_import_rime);
        _btnExportRime = FindViewById<Button>(Resource.Id.btn_prediction_export_rime);
        _listContainer = FindViewById<LinearLayout>(Resource.Id.list_prediction_container);
        _tvEmpty = FindViewById<TextView>(Resource.Id.tv_prediction_empty);

        _btnAdd.Click += (s, e) => AddPrediction();
        _btnImport.Click += (s, e) => StartImport();
        _btnExport.Click += (s, e) => StartExport();
        _btnImportRime.Click += (s, e) => StartImportRime();
        _btnExportRime.Click += (s, e) => StartExportRime();

        RenderList();
    }

    private void AddPrediction()
    {
        string prev = _editPrev?.Text?.Trim() ?? string.Empty;
        string next = _editNext?.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(prev) || string.IsNullOrWhiteSpace(next))
        {
            ShowToast("请输入前词和预测词");
            return;
        }

        bool added = _store.Add(prev, next);
        if (!added)
        {
            ShowToast("该预测词已存在或无效");
            return;
        }

        _editPrev.Text = string.Empty;
        _editNext.Text = string.Empty;
        ShowToast("已添加预测词");
        RenderList();
    }

    private void StartImport()
    {
        var intent = new Intent(Intent.ActionOpenDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("text/plain");
        StartActivityForResult(intent, RequestImport);
    }

    private void StartExport()
    {
        var intent = new Intent(Intent.ActionCreateDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("text/plain");
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        intent.PutExtra(Intent.ExtraTitle, $"ime_prediction_{timestamp}.txt");
        StartActivityForResult(intent, RequestExport);
    }

    private void StartExportRime()
    {
        var intent = new Intent(Intent.ActionCreateDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("application/zip");
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        intent.PutExtra(Intent.ExtraTitle, $"ime_rime_user_dicts_{timestamp}.zip");
        StartActivityForResult(intent, RequestExportRime);
    }

    private void StartImportRime()
    {
        var intent = new Intent(Intent.ActionOpenDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("*/*");
        intent.PutExtra(Intent.ExtraMimeTypes, new[]
        {
            "application/zip",
            "application/x-zip-compressed",
            "application/octet-stream"
        });
        StartActivityForResult(intent, RequestImportRime);
    }

    protected override async void OnActivityResult(int requestCode, Result resultCode, Intent data)
    {
        base.OnActivityResult(requestCode, resultCode, data);

        if (resultCode != Result.Ok || data?.Data == null)
        {
            return;
        }

        var uri = data.Data;
        if (requestCode == RequestImport)
        {
            ImportFromUri(uri);
        }
        else if (requestCode == RequestExport)
        {
            ExportToUri(uri);
        }
        else if (requestCode == RequestExportRime)
        {
            await ExportRimeUserDataAsync(uri);
        }
        else if (requestCode == RequestImportRime)
        {
            await ImportRimeUserDataAsync(uri);
        }
    }

    private void ImportFromUri(Android.Net.Uri uri)
    {
        try
        {
            using var stream = ContentResolver.OpenInputStream(uri);
            if (stream == null)
            {
                ShowToast("无法打开文件");
                return;
            }

            using var reader = new StreamReader(stream, Encoding.UTF8, true);
            string content = reader.ReadToEnd();
            var pairs = ParsePairs(content);
            int added = _store.Import(pairs);
            ShowToast($"导入完成：新增 {added} 条");
            RenderList();
        }
        catch (Exception ex)
        {
            Log.Error(Tag, $"导入失败: {ex.Message}");
            ShowToast("导入失败：" + ex.Message);
        }
    }

    private void ExportToUri(Android.Net.Uri uri)
    {
        try
        {
            using var stream = ContentResolver.OpenOutputStream(uri);
            if (stream == null)
            {
                ShowToast("无法创建文件");
                return;
            }

            var pairs = _store.GetAllPairs();
            var sb = new StringBuilder();
            foreach (var pair in pairs)
            {
                if (string.IsNullOrWhiteSpace(pair.Previous) || string.IsNullOrWhiteSpace(pair.Next))
                {
                    continue;
                }

                sb.Append(pair.Previous).Append('\t').Append(pair.Next).Append('\n');
            }

            using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            writer.Write(sb.ToString());
            writer.Flush();

            ShowToast("导出完成");
        }
        catch (Exception ex)
        {
            Log.Error(Tag, $"导出失败: {ex.Message}");
            ShowToast("导出失败：" + ex.Message);
        }
    }

    private async Task ExportRimeUserDataAsync(Android.Net.Uri uri)
    {
        string tempDir = string.Empty;
        string tempZip = string.Empty;

        try
        {
            var syncService = new SyncService(this);
            await Task.Run(() => syncService.SyncRimeUserData());

            if (!RimeLeversBindings.IsAvailable)
            {
                ShowToast("导出失败：Rime levers 不可用");
                return;
            }

            var dicts = RimeLeversBindings.ListUserDicts();
            if (dicts.Count == 0)
            {
                ShowToast("未找到可导出的词库");
                return;
            }

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            tempDir = Path.Combine(CacheDir.AbsolutePath, $"rime_user_dicts_{timestamp}");
            Directory.CreateDirectory(tempDir);

            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int exported = 0;

            foreach (var dict in dicts)
            {
                string baseName = SanitizeFileName(dict);
                string fileName = EnsureUniqueName(usedNames, baseName) + ".txt";
                string path = Path.Combine(tempDir, fileName);
                int result = RimeLeversBindings.ExportUserDict(dict, path);
                if (result >= 0 && File.Exists(path))
                {
                    exported++;
                }
            }

            if (exported == 0)
            {
                ShowToast("导出失败：无可用词库");
                return;
            }

            tempZip = Path.Combine(CacheDir.AbsolutePath, $"ime_rime_user_dicts_{timestamp}.zip");
            if (File.Exists(tempZip))
            {
                File.Delete(tempZip);
            }

            using (var zipStream = new FileStream(tempZip, FileMode.Create))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                string[] files = Directory.GetFiles(tempDir, "*.txt", SearchOption.TopDirectoryOnly);
                foreach (string file in files)
                {
                    string entryName = Path.GetFileName(file);
                    if (string.IsNullOrEmpty(entryName))
                    {
                        continue;
                    }
                    archive.CreateEntryFromFile(file, entryName, CompressionLevel.Optimal);
                }
            }

            using var output = ContentResolver.OpenOutputStream(uri);
            if (output == null)
            {
                ShowToast("无法创建文件");
                return;
            }

            using var input = new FileStream(tempZip, FileMode.Open, FileAccess.Read);
            input.CopyTo(output);
            output.Flush();

            ShowToast($"导出完成：{exported} 个词库");
        }
        catch (Exception ex)
        {
            Log.Error(Tag, $"导出 Rime 用户数据失败: {ex.Message}");
            ShowToast("导出失败：" + ex.Message);
        }
        finally
        {
            if (!string.IsNullOrEmpty(tempZip))
            {
                try
                {
                    if (File.Exists(tempZip))
                    {
                        File.Delete(tempZip);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn(Tag, $"清理临时文件失败: {ex.Message}");
                }
            }

            if (!string.IsNullOrEmpty(tempDir))
            {
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn(Tag, $"清理临时目录失败: {ex.Message}");
                }
            }
        }
    }

    private async Task ImportRimeUserDataAsync(Android.Net.Uri uri)
    {
        string tempDir = string.Empty;
        string tempZip = string.Empty;

        try
        {
            var syncService = new SyncService(this);
            await Task.Run(() => syncService.SyncRimeUserData());

            if (!RimeLeversBindings.IsAvailable)
            {
                ShowToast("导入失败：Rime levers 不可用");
                return;
            }

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            tempDir = Path.Combine(CacheDir.AbsolutePath, $"rime_user_dicts_import_{timestamp}");
            Directory.CreateDirectory(tempDir);
            tempZip = Path.Combine(tempDir, "import.zip");

            using (var input = ContentResolver.OpenInputStream(uri))
            {
                if (input == null)
                {
                    ShowToast("无法打开文件");
                    return;
                }

                using var output = new FileStream(tempZip, FileMode.Create, FileAccess.Write);
                input.CopyTo(output);
            }

            int imported = 0;
            int skipped = 0;
            int failed = 0;
            bool foundTxt = false;
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using (var archive = ZipFile.OpenRead(tempZip))
            {
                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        continue;
                    }

                    if (!entry.Name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    foundTxt = true;
                    string dictName = Path.GetFileNameWithoutExtension(entry.Name);
                    if (string.IsNullOrWhiteSpace(dictName))
                    {
                        skipped++;
                        continue;
                    }

                    if (!usedNames.Add(dictName))
                    {
                        skipped++;
                        continue;
                    }

                    string fileName = SanitizeFileName(dictName) + ".txt";
                    string path = Path.Combine(tempDir, fileName);

                    using (var entryStream = entry.Open())
                    using (var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write))
                    {
                        entryStream.CopyTo(fileStream);
                    }

                    int result = RimeLeversBindings.ImportUserDict(dictName, path);
                    if (result >= 0)
                    {
                        imported++;
                    }
                    else
                    {
                        failed++;
                    }
                }
            }

            if (!foundTxt)
            {
                ShowToast("导入失败：未找到可导入的 txt 词库");
                return;
            }

            if (imported == 0)
            {
                ShowToast("导入失败：未成功导入词库");
                return;
            }

            var message = new StringBuilder($"导入完成：{imported} 个词库");
            if (skipped > 0)
            {
                message.Append($"，跳过 {skipped} 个");
            }
            if (failed > 0)
            {
                message.Append($"，失败 {failed} 个");
            }

            message.Append("，请重新部署 Rime 生效");
            ShowToast(message.ToString());
        }
        catch (Exception ex)
        {
            Log.Error(Tag, $"导入 Rime 用户数据失败: {ex.Message}");
            ShowToast("导入失败：" + ex.Message);
        }
        finally
        {
            if (!string.IsNullOrEmpty(tempZip))
            {
                try
                {
                    if (File.Exists(tempZip))
                    {
                        File.Delete(tempZip);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn(Tag, $"清理临时文件失败: {ex.Message}");
                }
            }

            if (!string.IsNullOrEmpty(tempDir))
            {
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn(Tag, $"清理临时目录失败: {ex.Message}");
                }
            }
        }
    }

    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "dict";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            bool isInvalid = false;
            for (int j = 0; j < invalid.Length; j++)
            {
                if (invalid[j] == c)
                {
                    isInvalid = true;
                    break;
                }
            }
            sb.Append(isInvalid ? '_' : c);
        }

        return sb.Length == 0 ? "dict" : sb.ToString();
    }

    private static string EnsureUniqueName(HashSet<string> usedNames, string baseName)
    {
        string name = string.IsNullOrEmpty(baseName) ? "dict" : baseName;
        if (usedNames.Add(name))
        {
            return name;
        }

        int index = 1;
        while (true)
        {
            string candidate = $"{name}_{index}";
            if (usedNames.Add(candidate))
            {
                return candidate;
            }
            index++;
        }
    }

    private List<PredictionPair> ParsePairs(string content)
    {
        var results = new List<PredictionPair>();
        if (string.IsNullOrEmpty(content))
        {
            return results;
        }

        var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            if (line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            string prev;
            string next;
            if (!TrySplitLine(line, out prev, out next))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(prev) || string.IsNullOrWhiteSpace(next))
            {
                continue;
            }

            results.Add(new PredictionPair(prev, next));
        }

        return results;
    }

    private static bool TrySplitLine(string line, out string prev, out string next)
    {
        prev = string.Empty;
        next = string.Empty;

        if (string.IsNullOrEmpty(line))
        {
            return false;
        }

        string[] parts;
        if (line.Contains('\t'))
        {
            parts = line.Split('\t');
        }
        else if (line.Contains("->", StringComparison.Ordinal))
        {
            parts = line.Split(new[] { "->" }, StringSplitOptions.None);
        }
        else if (line.Contains("=>", StringComparison.Ordinal))
        {
            parts = line.Split(new[] { "=>" }, StringSplitOptions.None);
        }
        else if (line.Contains(','))
        {
            parts = line.Split(',');
        }
        else
        {
            return false;
        }

        if (parts.Length < 2)
        {
            return false;
        }

        prev = parts[0].Trim();
        next = parts[1].Trim();
        return true;
    }

    private void RenderList()
    {
        if (_listContainer == null)
        {
            return;
        }

        _listContainer.RemoveAllViews();
        var pairs = _store.GetAllPairs();
        pairs.Sort((a, b) =>
        {
            int cmp = string.CompareOrdinal(a.Previous, b.Previous);
            return cmp != 0 ? cmp : string.CompareOrdinal(a.Next, b.Next);
        });

        if (_tvEmpty != null)
        {
            _tvEmpty.Visibility = pairs.Count == 0 ? ViewStates.Visible : ViewStates.Gone;
        }

        foreach (var pair in pairs)
        {
            AddRow(pair);
        }
    }

    private void AddRow(PredictionPair pair)
    {
        var row = new LinearLayout(this)
        {
            Orientation = Orientation.Horizontal
        };
        row.SetPadding(8, 8, 8, 8);

        var text = new TextView(this)
        {
            Text = $"{pair.Previous} → {pair.Next}"
        };
        text.SetTextColor(Android.Graphics.Color.Black);
        text.LayoutParameters = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f);

        var btn = new Button(this)
        {
            Text = "删除"
        };
        btn.SetTextSize(Android.Util.ComplexUnitType.Sp, 12);
        btn.Click += (s, e) =>
        {
            if (_store.Remove(pair.Previous, pair.Next))
            {
                ShowToast("已删除");
                RenderList();
            }
        };

        row.AddView(text);
        row.AddView(btn);
        _listContainer.AddView(row);
    }

    private void ShowToast(string message)
    {
        Toast.MakeText(this, message, ToastLength.Short).Show();
    }
}
