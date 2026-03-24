using Android.Content;

namespace IME.Features.Settings
{
    public partial class SettingsActivity
    {
        private ISharedPreferences GetCachedPrefs()
        {
            return _cachedPrefs ??= GetSharedPreferences(PrefsName, FileCreationMode.Private);
        }

        private static ISharedPreferences GetPrefs(Context context)
        {
            return context.GetSharedPreferences(PrefsName, FileCreationMode.Private);
        }

        private void SaveSetting(string key, bool value)
        {
            var editor = GetCachedPrefs().Edit();
            editor.PutBoolean(key, value);
            editor.Apply();
        }

        private void SaveSetting(string key, string value)
        {
            var editor = GetCachedPrefs().Edit();
            editor.PutString(key, value);
            editor.Apply();
        }

        private void SaveSetting(string key, int value)
        {
            var editor = GetCachedPrefs().Edit();
            editor.PutInt(key, value);
            editor.Apply();
        }

        public static bool GetVibrateEnabled(Context context)
        {
            return GetPrefs(context).GetBoolean(KeyVibrate, true);
        }

        public static bool GetSoundEnabled(Context context)
        {
            return GetPrefs(context).GetBoolean(KeySound, false);
        }

        public static bool GetSimplificationEnabled(Context context)
        {
            return GetPrefs(context).GetBoolean(KeySimplification, true);
        }

        public static int GetCandidatePageSize(Context context)
        {
            return GetPrefs(context).GetInt(KeyCandidatePageSize, 6);
        }

        public static bool GetCandidatePreviewEnabled(Context context)
        {
            return GetPrefs(context).GetBoolean(KeyCandidatePreview, true);
        }

        public static string GetCurrentSchema(Context context)
        {
            return GetPrefs(context).GetString(KeySchema, "luna_pinyin");
        }

        public static string GetCurrentTheme(Context context)
        {
            return GetPrefs(context).GetString(KeyTheme, "light");
        }

        public static bool GetPredictionEnabled(Context context)
        {
            return GetPrefs(context).GetBoolean(KeyPredictionEnabled, true);
        }

        public static bool GetPredictionAlways(Context context)
        {
            return GetPrefs(context).GetBoolean(KeyPredictionAlways, true);
        }

        public static int GetPredictionRounds(Context context)
        {
            return GetPrefs(context).GetInt(KeyPredictionRounds, 3);
        }

        public static int GetBackspaceLongPressDelay(Context context)
        {
            var delay = GetPrefs(context).GetInt(KeyBackspaceLongPressDelay, 1000);
            if (delay < 200)
            {
                return 200;
            }

            if (delay > 3000)
            {
                return 3000;
            }

            return delay;
        }

        public static int GetTapFallbackMaxDurationMs(Context context)
        {
            var prefs = GetPrefs(context);
            EnsureTapFallbackSettings(prefs);
            int value = prefs.GetInt(KeyTapFallbackMaxDurationMs, DefaultTapFallbackMaxDurationMs);
            return ClampTapFallbackDurationMs(value);
        }

        public static int GetTapFallbackMoveSlopDp(Context context)
        {
            var prefs = GetPrefs(context);
            EnsureTapFallbackSettings(prefs);
            int value = prefs.GetInt(KeyTapFallbackMoveSlopDp, DefaultTapFallbackMoveSlopDp);
            return ClampTapFallbackMoveSlopDp(value);
        }

        private static void EnsureTapFallbackSettings(ISharedPreferences prefs)
        {
            bool hasDuration = prefs.Contains(KeyTapFallbackMaxDurationMs);
            bool hasMoveSlop = prefs.Contains(KeyTapFallbackMoveSlopDp);
            if (hasDuration && hasMoveSlop)
            {
                return;
            }

            int level = prefs.GetInt(KeyTapFallbackLevel, 2);
            (int durationMs, int moveSlopDp) = MapTapFallbackLevel(level);

            var editor = prefs.Edit();
            if (!hasDuration)
            {
                editor.PutInt(KeyTapFallbackMaxDurationMs, durationMs);
            }

            if (!hasMoveSlop)
            {
                editor.PutInt(KeyTapFallbackMoveSlopDp, moveSlopDp);
            }

            editor.Apply();
        }

        private static (int durationMs, int moveSlopDp) ResolveTapFallbackDefaults(ISharedPreferences prefs)
        {
            bool hasDuration = prefs.Contains(KeyTapFallbackMaxDurationMs);
            bool hasMoveSlop = prefs.Contains(KeyTapFallbackMoveSlopDp);
            if (hasDuration || hasMoveSlop)
            {
                int duration = prefs.GetInt(KeyTapFallbackMaxDurationMs, DefaultTapFallbackMaxDurationMs);
                int moveSlop = prefs.GetInt(KeyTapFallbackMoveSlopDp, DefaultTapFallbackMoveSlopDp);
                return (ClampTapFallbackDurationMs(duration), ClampTapFallbackMoveSlopDp(moveSlop));
            }

            int level = prefs.GetInt(KeyTapFallbackLevel, 2);
            return MapTapFallbackLevel(level);
        }

        private static (int durationMs, int moveSlopDp) MapTapFallbackLevel(int level)
        {
            return level switch
            {
                0 => (0, 0),
                1 => (140, 10),
                3 => (240, 18),
                _ => (DefaultTapFallbackMaxDurationMs, DefaultTapFallbackMoveSlopDp)
            };
        }

        private static int ClampTapFallbackDurationMs(int value)
        {
            if (value < 0)
            {
                return 0;
            }

            if (value > 1000)
            {
                return 1000;
            }

            return value;
        }

        private static int ClampTapFallbackMoveSlopDp(int value)
        {
            if (value < 0)
            {
                return 0;
            }

            if (value > 50)
            {
                return 50;
            }

            return value;
        }

        public static int GetEnterAction(Context context)
        {
            var action = GetPrefs(context).GetInt(KeyEnterAction, EnterActionNewLine);
            return action == EnterActionSend ? EnterActionSend : EnterActionNewLine;
        }

        public static bool GetT9Enabled(Context context)
        {
            return GetPrefs(context).GetBoolean(KeyT9Enabled, false);
        }

        public static void SetT9Enabled(Context context, bool enabled)
        {
            var editor = GetPrefs(context).Edit();
            editor.PutBoolean(KeyT9Enabled, enabled);
            editor.Apply();
        }
        public static string GetSimulatedTypingText(Context context)
        {
            return GetPrefs(context).GetString(KeySimulatedTypingText, string.Empty) ?? string.Empty;
        }
    }
}
