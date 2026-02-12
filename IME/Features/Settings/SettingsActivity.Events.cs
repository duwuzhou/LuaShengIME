using System;
using System.IO;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using AndroidUri = Android.Net.Uri;
using Android.Util;
using Android.Widget;
using IME.Features.UserLexicon;
using IME.Features.Shortcuts;
using IME.Shared.InputEngine;

namespace IME.Features.Settings
{
    public partial class SettingsActivity
    {
        private void SetupEventHandlers()
        {
            _switchVibrate.CheckedChange += (sender, e) =>
            {
                SaveSetting(KeyVibrate, e.IsChecked);
                ShowToast(e.IsChecked ? "已启用振动反馈" : "已关闭振动反馈");
            };

            _switchSound.CheckedChange += (sender, e) =>
            {
                SaveSetting(KeySound, e.IsChecked);
                ShowToast(e.IsChecked ? "已启用按键音效" : "已关闭按键音效");
            };

            _switchSimplification.CheckedChange += (sender, e) =>
            {
                SaveSetting(KeySimplification, e.IsChecked);
                ShowToast(e.IsChecked ? "已切换为简体输出" : "已切换为繁体输出");
            };

            _switchCandidatePreview.CheckedChange += (sender, e) =>
            {
                SaveSetting(KeyCandidatePreview, e.IsChecked);
                ShowToast(e.IsChecked ? "已开启候选预览" : "已关闭候选预览");
            };

            _switchPredictionEnabled.CheckedChange += (sender, e) =>
            {
                SaveSetting(KeyPredictionEnabled, e.IsChecked);
                UpdatePredictionRoundsEnabled();
                ShowToast(e.IsChecked ? "已开启词预测" : "已关闭词预测");
            };

            _switchPredictionAlways.CheckedChange += (sender, e) =>
            {
                SaveSetting(KeyPredictionAlways, e.IsChecked);
                UpdatePredictionRoundsEnabled();
                ShowToast(e.IsChecked ? "已设置始终预测" : "已关闭始终预测");
            };

            _spinnerSchema.ItemSelected += (sender, e) =>
            {
                if (e.Position >= 0 && e.Position < _schemaList.Count)
                {
                    string schema = _schemaList[e.Position];
                    SaveSetting(KeySchema, schema);
                    Log.Info(Tag, $"选择输入方案: {schema}");
                }
            };

            _spinnerTheme.ItemSelected += (sender, e) =>
            {
                if (e.Position >= 0 && e.Position < _themeList.Count)
                {
                    string theme = _themeList[e.Position];
                    SaveSetting(KeyTheme, theme);
                    Log.Info(Tag, $"选择主题: {theme}");
                }
            };

            _spinnerCandidatePageSize.ItemSelected += (sender, e) =>
            {
                if (e.Position >= 0 && e.Position < _candidatePageSizes.Count)
                {
                    int size = _candidatePageSizes[e.Position];
                    SaveSetting(KeyCandidatePageSize, size);
                    WriteCandidatePageSizePatch(size);
                    ShowToast($"候选每页 {size} 个，重新部署后生效");
                }
            };

            _spinnerPredictionRounds.ItemSelected += (sender, e) =>
            {
                if (e.Position >= 0 && e.Position < _predictionRounds.Count)
                {
                    int rounds = _predictionRounds[e.Position];
                    SaveSetting(KeyPredictionRounds, rounds);
                    if (_switchPredictionAlways != null && !_switchPredictionAlways.Checked)
                    {
                        ShowToast($"预测次数已设置为 {rounds} 次");
                    }
                }
            };

            _spinnerBackspaceLongPressDelay.ItemSelected += (sender, e) =>
            {
                if (e.Position >= 0 && e.Position < _backspaceLongPressDelayMs.Count)
                {
                    int delay = _backspaceLongPressDelayMs[e.Position];
                    SaveSetting(KeyBackspaceLongPressDelay, delay);
                    ShowToast($"\u957F\u6309\u5220\u9664\u89E6\u53D1\u65F6\u95F4\u5DF2\u8BBE\u7F6E\u4E3A {delay} ms");
                }
            };

            _spinnerEnterAction.ItemSelected += (sender, e) =>
            {
                if (e.Position >= 0 && e.Position < _enterActionModes.Count)
                {
                    int action = _enterActionModes[e.Position];
                    SaveSetting(KeyEnterAction, action);

                    if (action == EnterActionSend)
                    {
                        ShowToast("\u56DE\u8F66\u5DF2\u8BBE\u7F6E\u4E3A ImeAction.Send");
                    }
                    else
                    {
                        ShowToast("\u56DE\u8F66\u5DF2\u8BBE\u7F6E\u4E3A\u6362\u884C");
                    }
                }
            };

            if (_editTapFallbackMaxDurationMs != null)
            {
                _editTapFallbackMaxDurationMs.AfterTextChanged += (sender, e) =>
                {
                    if (_suppressTapFallbackTextEvents)
                    {
                        return;
                    }

                    string text = _editTapFallbackMaxDurationMs.Text?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        return;
                    }

                    if (int.TryParse(text, out int value))
                    {
                        int clamped = ClampTapFallbackDurationMs(value);
                        if (clamped != value)
                        {
                            _suppressTapFallbackTextEvents = true;
                            _editTapFallbackMaxDurationMs.Text = clamped.ToString();
                            _editTapFallbackMaxDurationMs.SetSelection(_editTapFallbackMaxDurationMs.Text.Length);
                            _suppressTapFallbackTextEvents = false;
                        }

                        SaveSetting(KeyTapFallbackMaxDurationMs, clamped);
                    }
                };
            }

            if (_editTapFallbackMoveSlopDp != null)
            {
                _editTapFallbackMoveSlopDp.AfterTextChanged += (sender, e) =>
                {
                    if (_suppressTapFallbackTextEvents)
                    {
                        return;
                    }

                    string text = _editTapFallbackMoveSlopDp.Text?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        return;
                    }

                    if (int.TryParse(text, out int value))
                    {
                        int clamped = ClampTapFallbackMoveSlopDp(value);
                        if (clamped != value)
                        {
                            _suppressTapFallbackTextEvents = true;
                            _editTapFallbackMoveSlopDp.Text = clamped.ToString();
                            _editTapFallbackMoveSlopDp.SetSelection(_editTapFallbackMoveSlopDp.Text.Length);
                            _suppressTapFallbackTextEvents = false;
                        }

                        SaveSetting(KeyTapFallbackMoveSlopDp, clamped);
                    }
                };
            }

            if (_btnResetTapFallback != null)
            {
                _btnResetTapFallback.Click += (sender, e) =>
                {
                    int durationMs = DefaultTapFallbackMaxDurationMs;
                    int moveSlopDp = DefaultTapFallbackMoveSlopDp;

                    SaveSetting(KeyTapFallbackMaxDurationMs, durationMs);
                    SaveSetting(KeyTapFallbackMoveSlopDp, moveSlopDp);

                    _suppressTapFallbackTextEvents = true;
                    if (_editTapFallbackMaxDurationMs != null)
                    {
                        _editTapFallbackMaxDurationMs.Text = durationMs.ToString();
                    }

                    if (_editTapFallbackMoveSlopDp != null)
                    {
                        _editTapFallbackMoveSlopDp.Text = moveSlopDp.ToString();
                    }
                    _suppressTapFallbackTextEvents = false;

                    ShowToast("已恢复默认补偿参数");
                };
            }

            _btnSyncUserData.Click += async (sender, e) =>
            {
                _btnSyncUserData.Enabled = false;
                _btnSyncUserData.Text = "同步中...";

                try
                {
                    bool result = await Task.Run(() => RimeNativeBindings.SyncUserData());
                    ShowToast(result ? "用户词库同步成功" : "用户词库同步失败");
                }
                catch (Exception ex)
                {
                    Log.Error(Tag, $"同步失败: {ex.Message}");
                    ShowToast("同步失败：" + ex.Message);
                }
                finally
                {
                    _btnSyncUserData.Enabled = true;
                    _btnSyncUserData.Text = "同步用户词库";
                }
            };

            _btnRedeploy.Click += async (sender, e) =>
            {
                _btnRedeploy.Enabled = false;
                _btnRedeploy.Text = "部署中...";

                try
                {
                    var result = await Task.Run(() => RimeMaintenanceRunner.DeployAndSync(this));
                    ShowToast(result.Success ? "Rime 部署成功" : $"Rime 部署失败：{result.Error}");
                }
                catch (Exception ex)
                {
                    Log.Error(Tag, $"部署失败: {ex.Message}");
                    ShowToast("部署失败：" + ex.Message);
                }
                finally
                {
                    _btnRedeploy.Enabled = true;
                    _btnRedeploy.Text = "重新部署 Rime";
                }
            };

            _btnImportLexicon.Click += (sender, e) =>
            {
                var intent = new Intent(Intent.ActionOpenDocument);
                intent.AddCategory(Intent.CategoryOpenable);
                intent.SetType("text/plain");
                StartActivityForResult(intent, RequestImportLexicon);
            };

            _btnExportDict.Click += (sender, e) =>
            {
                var intent = new Intent(Intent.ActionCreateDocument);
                intent.AddCategory(Intent.CategoryOpenable);
                intent.SetType("text/plain");
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                intent.PutExtra(Intent.ExtraTitle, $"ime_user_lexicon_{timestamp}.txt");
                StartActivityForResult(intent, RequestExportLexicon);
            };

            _btnPredictionManage.Click += (sender, e) =>
            {
                var intent = new Intent(this, typeof(IME.Features.Prediction.PredictionManagerActivity));
                StartActivity(intent);
            };

            _btnShortcutManage.Click += (sender, e) =>
            {
                var intent = new Intent(this, typeof(ShortcutManagerActivity));
                StartActivity(intent);
            };
        }

        protected override async void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            base.OnActivityResult(requestCode, resultCode, data);

            if (resultCode != Result.Ok || data?.Data == null)
            {
                return;
            }

            AndroidUri uri = data.Data;
            switch (requestCode)
            {
                case RequestImportLexicon:
                    await ImportLexiconAsync(uri);
                    break;
                case RequestExportLexicon:
                    await ExportLexiconAsync(uri);
                    break;
            }
        }

        private async Task ImportLexiconAsync(AndroidUri uri)
        {
            if (_lexiconService == null)
            {
                ShowToast("词库服务不可用");
                return;
            }

            SetLexiconButtonsEnabled(false, "导入中...", _btnExportDict?.Text ?? "导出用户词库");

            try
            {
                using var stream = ContentResolver.OpenInputStream(uri);
                if (stream == null)
                {
                    ShowToast("无法打开文件");
                    return;
                }

                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                buffer.Position = 0;

                var result = await Task.Run(() => _lexiconService.ImportTxt(buffer, false));
                ShowToast($"导入完成：{result.MergedEntries} 条，错误 {result.Errors.Count} 行。请点击“重新部署 Rime”生效。");
            }
            catch (Exception ex)
            {
                Log.Error(Tag, $"导入失败: {ex.Message}");
                ShowToast("导入失败：" + ex.Message);
            }
            finally
            {
                SetLexiconButtonsEnabled(true, "导入用户词库", "导出用户词库");
            }
        }

        private async Task ExportLexiconAsync(AndroidUri uri)
        {
            if (_lexiconService == null)
            {
                ShowToast("词库服务不可用");
                return;
            }

            SetLexiconButtonsEnabled(false, _btnImportLexicon?.Text ?? "导入用户词库", "导出中...");

            try
            {
                using var stream = ContentResolver.OpenOutputStream(uri);
                if (stream == null)
                {
                    ShowToast("无法创建文件");
                    return;
                }

                var result = await Task.Run(() => _lexiconService.ExportTxt(stream));
                ShowToast($"导出完成：{result.TotalEntries} 条");
            }
            catch (Exception ex)
            {
                Log.Error(Tag, $"导出失败: {ex.Message}");
                ShowToast("导出失败：" + ex.Message);
            }
            finally
            {
                SetLexiconButtonsEnabled(true, "导入用户词库", "导出用户词库");
            }
        }

        private void SetLexiconButtonsEnabled(bool enabled, string importText, string exportText)
        {
            if (_btnImportLexicon != null)
            {
                _btnImportLexicon.Enabled = enabled;
                _btnImportLexicon.Text = importText;
            }

            if (_btnExportDict != null)
            {
                _btnExportDict.Enabled = enabled;
                _btnExportDict.Text = exportText;
            }
        }

        private void ShowToast(string message)
        {
            Toast.MakeText(this, message, ToastLength.Short).Show();
        }
    }
}



