using System;
using Android.Content;
using Android.Util;
using Android.Widget;
using Google.Android.Material.SwitchMaterial;
using IME.Features.UserLexicon;
using IME.Shared.InputEngine;

namespace IME.Features.Settings
{
    public partial class SettingsActivity
    {
        private void InitializeViews()
        {
            _spinnerSchema = FindViewById<Spinner>(Resource.Id.spinner_schema);
            _spinnerTheme = FindViewById<Spinner>(Resource.Id.spinner_theme);
            _switchVibrate = FindViewById<SwitchMaterial>(Resource.Id.switch_vibrate);
            _switchSound = FindViewById<SwitchMaterial>(Resource.Id.switch_sound);
            _switchSimplification = FindViewById<SwitchMaterial>(Resource.Id.switch_simplification);
            _switchCandidatePreview = FindViewById<SwitchMaterial>(Resource.Id.switch_candidate_preview);
            _switchPredictionEnabled = FindViewById<SwitchMaterial>(Resource.Id.switch_prediction_enabled);
            _switchPredictionAlways = FindViewById<SwitchMaterial>(Resource.Id.switch_prediction_always);
            _btnSyncUserData = FindViewById<Button>(Resource.Id.btn_sync_user_data);
            _btnRedeploy = FindViewById<Button>(Resource.Id.btn_redeploy);
            _btnImportLexicon = FindViewById<Button>(Resource.Id.btn_import_lexicon);
            _btnExportDict = FindViewById<Button>(Resource.Id.btn_export_dict);
            _btnPredictionManage = FindViewById<Button>(Resource.Id.btn_prediction_manage);
            _btnShortcutManage = FindViewById<Button>(Resource.Id.btn_shortcut_manage);
            _tvRimeVersion = FindViewById<TextView>(Resource.Id.tv_rime_version);
            _tvAppVersion = FindViewById<TextView>(Resource.Id.tv_app_version);
            _spinnerCandidatePageSize = FindViewById<Spinner>(Resource.Id.spinner_candidate_page_size);
            _spinnerPredictionRounds = FindViewById<Spinner>(Resource.Id.spinner_prediction_rounds);
            _spinnerBackspaceLongPressDelay = FindViewById<Spinner>(Resource.Id.spinner_backspace_long_press_delay);
            _spinnerEnterAction = FindViewById<Spinner>(Resource.Id.spinner_enter_action);
            _editTapFallbackMaxDurationMs = FindViewById<EditText>(Resource.Id.edit_tap_fallback_duration_ms);
            _editTapFallbackMoveSlopDp = FindViewById<EditText>(Resource.Id.edit_tap_fallback_move_slop_dp);
            _btnResetTapFallback = FindViewById<Button>(Resource.Id.btn_reset_tap_fallback);

            EnsureKamiViews();

            var schemaAdapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleSpinnerItem, _schemaDisplayList);
            schemaAdapter.SetDropDownViewResource(Android.Resource.Layout.SimpleSpinnerDropDownItem);
            _spinnerSchema.Adapter = schemaAdapter;

            var themeAdapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleSpinnerItem, _themeDisplayList);
            themeAdapter.SetDropDownViewResource(Android.Resource.Layout.SimpleSpinnerDropDownItem);
            _spinnerTheme.Adapter = themeAdapter;

            var candidatePageSizeAdapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleSpinnerItem, _candidatePageSizeDisplay);
            candidatePageSizeAdapter.SetDropDownViewResource(Android.Resource.Layout.SimpleSpinnerDropDownItem);
            _spinnerCandidatePageSize.Adapter = candidatePageSizeAdapter;

            var predictionRoundsAdapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleSpinnerItem, _predictionRoundsDisplay);
            predictionRoundsAdapter.SetDropDownViewResource(Android.Resource.Layout.SimpleSpinnerDropDownItem);
            _spinnerPredictionRounds.Adapter = predictionRoundsAdapter;

            var backspaceLongPressDelayAdapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleSpinnerItem, _backspaceLongPressDelayDisplay);
            backspaceLongPressDelayAdapter.SetDropDownViewResource(Android.Resource.Layout.SimpleSpinnerDropDownItem);
            _spinnerBackspaceLongPressDelay.Adapter = backspaceLongPressDelayAdapter;

            var enterActionAdapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleSpinnerItem, _enterActionDisplay);
            enterActionAdapter.SetDropDownViewResource(Android.Resource.Layout.SimpleSpinnerDropDownItem);
            _spinnerEnterAction.Adapter = enterActionAdapter;
        }

        private void InitializeLexiconServices()
        {
            _localLexiconStore = new LocalUserLexiconStore(this);
            _rimeLexiconStore = new RimeUserLexiconStore(this);
            _lexiconService = new UserLexiconService(_localLexiconStore, _rimeLexiconStore);
        }

        private void LoadSettings()
        {
            var prefs = GetCachedPrefs();

            _switchVibrate.Checked = prefs.GetBoolean(KeyVibrate, true);
            _switchSound.Checked = prefs.GetBoolean(KeySound, false);
            _switchSimplification.Checked = prefs.GetBoolean(KeySimplification, true);
            _switchCandidatePreview.Checked = prefs.GetBoolean(KeyCandidatePreview, true);
            _switchPredictionEnabled.Checked = prefs.GetBoolean(KeyPredictionEnabled, true);
            _switchPredictionAlways.Checked = prefs.GetBoolean(KeyPredictionAlways, true);

            string savedSchema = prefs.GetString(KeySchema, "luna_pinyin");
            int schemaIndex = _schemaList.IndexOf(savedSchema);
            if (schemaIndex >= 0)
            {
                _spinnerSchema.SetSelection(schemaIndex);
            }

            string savedTheme = prefs.GetString(KeyTheme, "light");
            int themeIndex = _themeList.IndexOf(savedTheme);
            if (themeIndex >= 0)
            {
                _spinnerTheme.SetSelection(themeIndex);
            }

            int savedPageSize = prefs.GetInt(KeyCandidatePageSize, 6);
            int pageIndex = _candidatePageSizes.IndexOf(savedPageSize);
            if (pageIndex >= 0)
            {
                _spinnerCandidatePageSize.SetSelection(pageIndex);
            }

            int savedRounds = prefs.GetInt(KeyPredictionRounds, 3);
            int roundIndex = _predictionRounds.IndexOf(savedRounds);
            if (roundIndex >= 0)
            {
                _spinnerPredictionRounds.SetSelection(roundIndex);
            }

            int savedLongPressDelay = prefs.GetInt(KeyBackspaceLongPressDelay, 1000);
            int longPressDelayIndex = _backspaceLongPressDelayMs.IndexOf(savedLongPressDelay);
            if (longPressDelayIndex < 0)
            {
                longPressDelayIndex = _backspaceLongPressDelayMs.IndexOf(1000);
            }

            if (longPressDelayIndex >= 0)
            {
                _spinnerBackspaceLongPressDelay.SetSelection(longPressDelayIndex);
            }

            int savedEnterAction = prefs.GetInt(KeyEnterAction, EnterActionNewLine);
            int enterActionIndex = _enterActionModes.IndexOf(savedEnterAction);
            if (enterActionIndex < 0)
            {
                enterActionIndex = _enterActionModes.IndexOf(EnterActionNewLine);
            }

            if (enterActionIndex >= 0)
            {
                _spinnerEnterAction.SetSelection(enterActionIndex);
            }

            (int tapFallbackDurationMs, int tapFallbackMoveSlopDp) = ResolveTapFallbackDefaults(prefs);
            _suppressTapFallbackTextEvents = true;
            if (_editTapFallbackMaxDurationMs != null)
            {
                _editTapFallbackMaxDurationMs.Text = tapFallbackDurationMs.ToString();
            }

            if (_editTapFallbackMoveSlopDp != null)
            {
                _editTapFallbackMoveSlopDp.Text = tapFallbackMoveSlopDp.ToString();
            }
            _suppressTapFallbackTextEvents = false;

            UpdatePredictionRoundsEnabled();
        }

        private void UpdatePredictionRoundsEnabled()
        {
            bool enabled = _switchPredictionEnabled != null && _switchPredictionEnabled.Checked;
            bool always = _switchPredictionAlways != null && _switchPredictionAlways.Checked;
            if (_spinnerPredictionRounds != null)
            {
                _spinnerPredictionRounds.Enabled = enabled && !always;
            }
        }

        private void LoadVersionInfo()
        {
            try
            {
                string rimeVersion = RimeNativeBindings.GetVersion();
                _tvRimeVersion.Text = $"Rime 版本: {rimeVersion ?? "未知"}";

                var packageInfo = PackageManager.GetPackageInfo(PackageName, 0);
                _tvAppVersion.Text = $"应用版本: {packageInfo?.VersionName ?? "未知"}";
            }
            catch (Exception ex)
            {
                Log.Error(Tag, $"获取版本信息失败: {ex.Message}");
            }
        }
    }
}
