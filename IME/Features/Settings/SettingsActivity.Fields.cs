using System.Collections.Generic;
using Android.Content;
using Android.Widget;
using Google.Android.Material.SwitchMaterial;
using IME.Features.UserLexicon;

namespace IME.Features.Settings
{
    public partial class SettingsActivity
    {
        private const string Tag = "SettingsActivity";
        private const string PrefsName = "ime_settings";
        private const int RequestImportLexicon = 2101;
        private const int RequestExportLexicon = 2102;

        private const string KeyVibrate = "key_vibrate";
        private const string KeySound = "key_sound";
        private const string KeySimplification = "key_simplification";
        private const string KeySchema = "key_schema";
        private const string KeyTheme = "key_theme";
        private const string KeyCandidatePageSize = "key_candidate_page_size";
        private const string KeyCandidatePreview = "key_candidate_preview";
        private const string KeyPredictionEnabled = "key_prediction_enabled";
        private const string KeyPredictionAlways = "key_prediction_always";
        private const string KeyPredictionRounds = "key_prediction_rounds";
        private const string KeyBackspaceLongPressDelay = "key_backspace_long_press_delay";
        private const string KeyEnterAction = "key_enter_action";
        private const string KeyTapFallbackLevel = "key_tap_fallback_level";
        private const string KeyTapFallbackMaxDurationMs = "key_tap_fallback_max_duration_ms";
        private const string KeyTapFallbackMoveSlopDp = "key_tap_fallback_move_slop_dp";
        private const string KeyT9Enabled = "key_t9_enabled";

        private const int DefaultTapFallbackMaxDurationMs = 180;
        private const int DefaultTapFallbackMoveSlopDp = 14;

        public const int EnterActionNewLine = 0;
        public const int EnterActionSend = 1;

        private Spinner _spinnerSchema;
        private Spinner _spinnerTheme;
        private SwitchMaterial _switchVibrate;
        private SwitchMaterial _switchSound;
        private SwitchMaterial _switchSimplification;
        private SwitchMaterial _switchCandidatePreview;
        private SwitchMaterial _switchPredictionEnabled;
        private SwitchMaterial _switchPredictionAlways;
        private Button _btnSyncUserData;
        private Button _btnRedeploy;
        private Button _btnImportLexicon;
        private Button _btnExportDict;
        private Button _btnPredictionManage;
        private Button _btnShortcutManage;
        private TextView _tvRimeVersion;
        private TextView _tvAppVersion;
        private Spinner _spinnerCandidatePageSize;
        private Spinner _spinnerPredictionRounds;
        private Spinner _spinnerBackspaceLongPressDelay;
        private Spinner _spinnerEnterAction;
        private EditText _editTapFallbackMaxDurationMs;
        private EditText _editTapFallbackMoveSlopDp;
        private Button _btnResetTapFallback;
        private Button _btnKamiVipPage;
        private bool _suppressTapFallbackTextEvents;

        private LocalUserLexiconStore _localLexiconStore;
        private RimeUserLexiconStore _rimeLexiconStore;
        private UserLexiconService _lexiconService;
        private ISharedPreferences _cachedPrefs;

        private readonly List<string> _schemaList = new List<string> { "luna_pinyin", "cangjie5" };
        private readonly List<string> _schemaDisplayList = new List<string> { "月光拼音", "仓颉5" };
        private readonly List<string> _themeList = new List<string> { "light", "dark" };
        private readonly List<string> _themeDisplayList = new List<string> { "浅色主题", "深色主题" };
        private readonly List<int> _candidatePageSizes = new List<int> { 3, 6, 9, 12 };
        private readonly List<string> _candidatePageSizeDisplay = new List<string> { "3 个/页", "6 个/页", "9 个/页", "12 个/页" };
        private readonly List<int> _predictionRounds = new List<int> { 1, 2, 3, 5, 10 };
        private readonly List<string> _predictionRoundsDisplay = new List<string> { "1 次", "2 次", "3 次", "5 次", "10 次" };
        private readonly List<int> _backspaceLongPressDelayMs = new List<int> { 300, 500, 800, 1000, 1500 };
        private readonly List<string> _backspaceLongPressDelayDisplay = new List<string> { "300 ms", "500 ms", "800 ms", "1000 ms", "1500 ms" };
        private readonly List<int> _enterActionModes = new List<int> { EnterActionNewLine, EnterActionSend };
        private readonly List<string> _enterActionDisplay = new List<string> { "\u6362\u884c", "ImeAction.Send" };
    }
}
