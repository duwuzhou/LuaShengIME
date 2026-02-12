using Android.Content;
using Android.Content.Res;
using Android.Graphics;
using Android.OS;
using Android.Util;
using System;

namespace IME.Theme
{
    /// <summary>
    /// 主题管理器
    /// 支持浅色和深色主题切换
    /// </summary>
    public class ThemeManager
    {
        private const string Tag = "ThemeManager";
        private const string PREFS_NAME = "ime_settings";
        private const string KEY_THEME = "key_theme";

        private readonly Context _context;
        private ThemeColors _currentTheme;

        public static ThemeManager Instance { get; private set; }

        public ThemeManager(Context context)
        {
            _context = context;
            Instance = this;
            LoadTheme();
        }

        /// <summary>
        /// 获取当前主题色彩配置
        /// </summary>
        public ThemeColors CurrentColors => _currentTheme;

        /// <summary>
        /// 加载主题设置
        /// </summary>
        private void LoadTheme()
        {
            var prefs = _context.GetSharedPreferences(PREFS_NAME, FileCreationMode.Private);
            string themeName = prefs.GetString(KEY_THEME, "light");
            ApplyTheme(themeName);
        }

        /// <summary>
        /// 应用指定主题
        /// </summary>
        public void ApplyTheme(string themeName)
        {
            switch (themeName?.ToLower())
            {
                case "dark":
                    _currentTheme = ThemeColors.Dark;
                    break;
                case "light":
                default:
                    _currentTheme = ThemeColors.Light;
                    break;
            }
            Log.Info(Tag, $"已应用主题: {themeName}");
        }

        /// <summary>
        /// 保存主题设置
        /// </summary>
        public void SetTheme(string themeName)
        {
            var prefs = _context.GetSharedPreferences(PREFS_NAME, FileCreationMode.Private);
            var editor = prefs.Edit();
            editor.PutString(KEY_THEME, themeName);
            editor.Apply();
            ApplyTheme(themeName);
        }

        /// <summary>
        /// 检查系统是否处于深色模式
        /// </summary>
        public bool IsSystemDarkMode()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
            {
                var uiMode = _context.Resources.Configuration.UiMode & UiMode.NightMask;
                return uiMode == UiMode.NightYes;
            }
            return false;
        }

        /// <summary>
        /// 跟随系统主题
        /// </summary>
        public void FollowSystemTheme()
        {
            string theme = IsSystemDarkMode() ? "dark" : "light";
            SetTheme(theme);
        }
    }

    /// <summary>
    /// 主题色彩配置
    /// </summary>
    public class ThemeColors
    {
        /// <summary>
        /// 主背景色
        /// </summary>
        public Color BackgroundColor { get; set; }

        /// <summary>
        /// 按键背景色
        /// </summary>
        public Color KeyBackgroundColor { get; set; }

        /// <summary>
        /// 按键按下背景色
        /// </summary>
        public Color KeyPressedColor { get; set; }

        /// <summary>
        /// 按键文字颜色
        /// </summary>
        public Color KeyTextColor { get; set; }

        /// <summary>
        /// 功能键背景色
        /// </summary>
        public Color FunctionKeyColor { get; set; }

        /// <summary>
        /// 候选栏背景色
        /// </summary>
        public Color CandidateBackgroundColor { get; set; }

        /// <summary>
        /// 候选词文字颜色
        /// </summary>
        public Color CandidateTextColor { get; set; }

        /// <summary>
        /// 候选词选中背景色
        /// </summary>
        public Color CandidateSelectedColor { get; set; }

        /// <summary>
        /// 拼音预览文字颜色
        /// </summary>
        public Color PreviewTextColor { get; set; }

        /// <summary>
        /// 分隔线颜色
        /// </summary>
        public Color DividerColor { get; set; }

        /// <summary>
        /// 索引数字颜色
        /// </summary>
        public Color IndexColor { get; set; }

        /// <summary>
        /// 浅色主题
        /// </summary>
        public static ThemeColors Light => new ThemeColors
        {
            BackgroundColor = Color.ParseColor("#E0E0E0"),
            KeyBackgroundColor = Color.White,
            KeyPressedColor = Color.ParseColor("#BDBDBD"),
            KeyTextColor = Color.ParseColor("#333333"),
            FunctionKeyColor = Color.ParseColor("#BDBDBD"),
            CandidateBackgroundColor = Color.ParseColor("#F5F5F5"),
            CandidateTextColor = Color.ParseColor("#333333"),
            CandidateSelectedColor = Color.ParseColor("#E0E0E0"),
            PreviewTextColor = Color.ParseColor("#666666"),
            DividerColor = Color.ParseColor("#BDBDBD"),
            IndexColor = Color.ParseColor("#999999")
        };

        /// <summary>
        /// 深色主题
        /// </summary>
        public static ThemeColors Dark => new ThemeColors
        {
            BackgroundColor = Color.ParseColor("#1E1E1E"),
            KeyBackgroundColor = Color.ParseColor("#2D2D2D"),
            KeyPressedColor = Color.ParseColor("#3D3D3D"),
            KeyTextColor = Color.ParseColor("#E0E0E0"),
            FunctionKeyColor = Color.ParseColor("#3D3D3D"),
            CandidateBackgroundColor = Color.ParseColor("#252525"),
            CandidateTextColor = Color.ParseColor("#E0E0E0"),
            CandidateSelectedColor = Color.ParseColor("#3D3D3D"),
            PreviewTextColor = Color.ParseColor("#AAAAAA"),
            DividerColor = Color.ParseColor("#404040"),
            IndexColor = Color.ParseColor("#888888")
        };
    }
}
