using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Android.Content;
using Android.Graphics;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidX.Core.Content;

namespace IME.Features.Keyboard;

/// <summary>
/// 搜狗风格三栏 T9 键盘复合视图。
/// 结构：
///   主区域（水平）：左侧符号栏 | 中间 CustomKeyboardView(3x4 网格) | 右侧 ⌫/↵
///   底部功能栏（全宽）：拼音(切QWERTY) | 中/英
/// </summary>
public sealed class T9KeyboardView : LinearLayout
{
    private const string Tag = "T9KeyboardView";
    private const string PrefsName = "t9_quick_symbols";
    private const string SymbolsKey = "symbols";
    private const string SymbolsV2Key = "symbols_v2";
    private const int BackspaceKeyCode = -5;
    private const int EnterKeyCode = -3;
    private const int SwitchSymbolCode = -6;
    private const int SwitchNumberCode = -2;
    private const int SpaceKeyCode = 32;
    private const int SwitchLanguageCode = -4;

    private static readonly QuickSymbolEntry[] DefaultSymbols =
    {
        new() { Display = "，", Commit = "，" },
        new() { Display = "。", Commit = "。" },
        new() { Display = "！", Commit = "！" },
        new() { Display = "？", Commit = "？" },
        new() { Display = "；", Commit = "；" },
        new() { Display = "：", Commit = "：" },
        new() { Display = "、", Commit = "、" },
        new() { Display = "…", Commit = "…" },
        new() { Display = "~", Commit = "~" },
        new() { Display = "·", Commit = "·" },
        new() { Display = "'", Commit = "'" },
        new() { Display = "（", Commit = "（" },
        new() { Display = "）", Commit = "）" },
        new() { Display = "—", Commit = "—" }
    };

    private LinearLayout? _mainSection;
    private ScrollView? _symbolScroll;
    private LinearLayout? _symbolContainer;
    private CustomKeyboardView? _centerKeyboard;
    private LinearLayout? _rightColumn;
    private Button? _deleteButton;
    private Button? _enterButton;
    private LinearLayout? _bottomBar;
    private bool _centerEventsBound;
    private bool _isPinyinMode;
    private List<QuickSymbolEntry>? _cachedSymbolEntries;

    public event EventHandler<KeyboardKeyEventArgs>? Key;
    public event EventHandler<KeyboardKeyEventArgs>? Press;
    public event EventHandler<KeyboardKeyEventArgs>? Release;
    public event EventHandler<string>? PinyinSelected;

    public T9KeyboardView(Context context, CustomKeyboardView centerKeyboard) : base(context)
    {
        Orientation = Orientation.Vertical;
        LayoutParameters = new LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
        SetBackgroundColor(new Color(ContextCompat.GetColor(context, Resource.Color.keyboard_background)));

        _centerKeyboard = centerKeyboard;
        BuildLayout();
        BindCenterEvents();
    }

    public CustomKeyboardView? ExtractCenterKeyboard()
    {
        UnbindCenterEvents();

        if (_centerKeyboard?.Parent != null)
        {
            ((ViewGroup)_centerKeyboard.Parent).RemoveView(_centerKeyboard);
        }

        var keyboard = _centerKeyboard;
        _centerKeyboard = null;
        return keyboard;
    }

    public void Cleanup()
    {
        UnbindCenterEvents();

        if (_deleteButton != null)
        {
            _deleteButton.Touch -= OnDeleteButtonTouch;
        }

        _deleteButton = null;
        _enterButton = null;
        _symbolContainer = null;
        _symbolScroll = null;
        _rightColumn = null;
        _mainSection = null;
        _bottomBar = null;
        _centerKeyboard = null;
    }

    private void BuildLayout()
    {
        // 计算中间键盘高度：3行 × 52dp键高 + 2个间距 × 5dp(3dp XML + 2dp extra)
        int centerHeightDp = 3 * 52 + 2 * 5; // = 166dp
        int centerHeightPx = DpToPx(centerHeightDp);

        // 主区域：水平三栏，固定高度与中间键盘一致
        _mainSection = new LinearLayout(Context)
        {
            Orientation = Orientation.Horizontal,
            LayoutParameters = new LayoutParams(
                ViewGroup.LayoutParams.MatchParent, centerHeightPx)
        };

        BuildLeftSymbolColumn();
        BuildCenterColumn();
        BuildRightColumn();

        AddView(_mainSection);

        // 底部功能栏
        BuildBottomBar();
    }

    // ── 左侧符号栏 ──────────────────────────────────────

    private void BuildLeftSymbolColumn()
    {
        _symbolScroll = new ScrollView(Context)
        {
            LayoutParameters = new LayoutParams(0, ViewGroup.LayoutParams.MatchParent, 0.10f),
            VerticalScrollBarEnabled = false,
            FillViewport = true
        };

        _symbolContainer = new LinearLayout(Context)
        {
            Orientation = Orientation.Vertical,
            LayoutParameters = new ViewGroup.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
        };
        _symbolContainer.SetGravity(GravityFlags.Top);

        List<QuickSymbolEntry> symbols = LoadSymbols();
        _cachedSymbolEntries = symbols;
        foreach (var entry in symbols)
        {
            AddSymbolButton(entry);
        }

        // "+" 自定义按钮
        AddCustomizeButton();

        _symbolScroll.AddView(_symbolContainer);
        _mainSection!.AddView(_symbolScroll);
    }

    private void AddSymbolButton(QuickSymbolEntry entry)
    {
        var button = new Button(Context)
        {
            Text = entry.Display,
            Gravity = GravityFlags.Center,
            TextAlignment = TextAlignment.Center
        };
        button.SetAllCaps(false);
        button.SetBackgroundResource(Resource.Drawable.key_background);
        button.SetTextColor(new Color(ContextCompat.GetColor(Context, Resource.Color.key_text)));
        button.SetTextSize(ComplexUnitType.Sp, 16f);
        button.SetPadding(0, 0, 0, 0);
        button.SetIncludeFontPadding(false);

        // 高度与中间键盘行高对齐（52dp key + 3dp gap = 55dp）
        int rowHeight = DpToPx(55);
        var layoutParams = new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent, rowHeight);
        button.LayoutParameters = layoutParams;

        string commitText = entry.Commit;
        button.Click += (_, _) =>
        {
            int keyCode = char.ConvertToUtf32(commitText, 0);
            Key?.Invoke(this, new KeyboardKeyEventArgs(keyCode, commitText));
        };

        _symbolContainer?.AddView(button);
    }

    private void AddCustomizeButton()
    {
        var button = new Button(Context)
        {
            Text = "+",
            Gravity = GravityFlags.Center,
            TextAlignment = TextAlignment.Center
        };
        button.SetAllCaps(false);
        button.SetBackgroundResource(Resource.Drawable.key_function_background);
        button.SetTextColor(new Color(ContextCompat.GetColor(Context, Resource.Color.key_text_secondary)));
        button.SetTextSize(ComplexUnitType.Sp, 20f);
        button.SetPadding(0, 0, 0, 0);
        button.SetIncludeFontPadding(false);

        int rowHeight = DpToPx(55);
        var layoutParams = new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent, rowHeight);
        button.LayoutParameters = layoutParams;

        button.Click += (_, _) =>
        {
            try
            {
                var intent = new Intent(Context, typeof(QuickSymbolEditorActivity));
                intent.AddFlags(ActivityFlags.NewTask);
                Context.StartActivity(intent);
            }
            catch (Exception ex)
            {
                Log.Warn(Tag, $"Failed to launch QuickSymbolEditorActivity: {ex.Message}");
            }
        };

        _symbolContainer?.AddView(button);
    }

    // ── 中间键盘区 ──────────────────────────────────────

    private void BuildCenterColumn()
    {
        if (_centerKeyboard == null)
        {
            return;
        }

        if (_centerKeyboard.Parent != null)
        {
            ((ViewGroup)_centerKeyboard.Parent).RemoveView(_centerKeyboard);
        }

        _centerKeyboard.LayoutParameters = new LayoutParams(
            0, ViewGroup.LayoutParams.MatchParent, 0.72f);
        _mainSection!.AddView(_centerKeyboard);
    }

    // ── 右侧功能键栏 ────────────────────────────────────

    private void BuildRightColumn()
    {
        _rightColumn = new LinearLayout(Context)
        {
            Orientation = Orientation.Vertical,
            LayoutParameters = new LayoutParams(0, ViewGroup.LayoutParams.MatchParent, 0.18f)
        };

        // 删除按钮（上半）
        _deleteButton = CreateSideButton("⌫", 20f);
        _deleteButton.LayoutParameters = new LayoutParams(
            ViewGroup.LayoutParams.MatchParent, 0, 1f)
        {
            LeftMargin = DpToPx(2),
            RightMargin = DpToPx(2),
            TopMargin = DpToPx(2),
            BottomMargin = DpToPx(1)
        };
        _deleteButton.Touch += OnDeleteButtonTouch;
        _rightColumn.AddView(_deleteButton);

        // 0 键（下半）
        var zeroButton = CreateSideButton("0", 18f);
        zeroButton.SetBackgroundResource(Resource.Drawable.key_background);
        zeroButton.SetTextColor(new Color(ContextCompat.GetColor(Context, Resource.Color.key_text)));
        zeroButton.LayoutParameters = new LayoutParams(
            ViewGroup.LayoutParams.MatchParent, 0, 1f)
        {
            LeftMargin = DpToPx(2),
            RightMargin = DpToPx(2),
            TopMargin = DpToPx(1),
            BottomMargin = DpToPx(2)
        };
        zeroButton.Click += (_, _) =>
        {
            Key?.Invoke(this, new KeyboardKeyEventArgs(48)); // '0' = 48
        };
        _rightColumn.AddView(zeroButton);

        _mainSection!.AddView(_rightColumn);
    }

    // ── 底部功能栏 ──────────────────────────────────────

    private void BuildBottomBar()
    {
        _bottomBar = new LinearLayout(Context)
        {
            Orientation = Orientation.Horizontal,
            LayoutParameters = new LayoutParams(
                ViewGroup.LayoutParams.MatchParent, DpToPx(42))
        };
        _bottomBar.SetGravity(GravityFlags.CenterVertical);

        int margin = DpToPx(2);

        // 符号
        var symbolButton = CreateBottomBarButton("符号");
        symbolButton.LayoutParameters = new LayoutParams(0, ViewGroup.LayoutParams.MatchParent, 1f)
        {
            LeftMargin = margin, RightMargin = margin / 2,
            TopMargin = margin / 2, BottomMargin = margin
        };
        symbolButton.Click += (_, _) =>
        {
            Key?.Invoke(this, new KeyboardKeyEventArgs(SwitchSymbolCode));
        };
        _bottomBar.AddView(symbolButton);

        // 123 数字键盘
        var numberButton = CreateBottomBarButton("123");
        numberButton.LayoutParameters = new LayoutParams(0, ViewGroup.LayoutParams.MatchParent, 0.8f)
        {
            LeftMargin = margin / 2, RightMargin = margin / 2,
            TopMargin = margin / 2, BottomMargin = margin
        };
        numberButton.Click += (_, _) =>
        {
            Key?.Invoke(this, new KeyboardKeyEventArgs(SwitchNumberCode));
        };
        _bottomBar.AddView(numberButton);

        // 空格
        var spaceButton = CreateBottomBarButton("空格");
        spaceButton.LayoutParameters = new LayoutParams(0, ViewGroup.LayoutParams.MatchParent, 2f)
        {
            LeftMargin = margin / 2, RightMargin = margin / 2,
            TopMargin = margin / 2, BottomMargin = margin
        };
        spaceButton.Click += (_, _) =>
        {
            Key?.Invoke(this, new KeyboardKeyEventArgs(SpaceKeyCode));
        };
        _bottomBar.AddView(spaceButton);

        // 中/英
        var langButton = CreateBottomBarButton("中/英");
        langButton.LayoutParameters = new LayoutParams(0, ViewGroup.LayoutParams.MatchParent, 1f)
        {
            LeftMargin = margin / 2, RightMargin = margin / 2,
            TopMargin = margin / 2, BottomMargin = margin
        };
        langButton.Click += (_, _) =>
        {
            Key?.Invoke(this, new KeyboardKeyEventArgs(SwitchLanguageCode));
        };
        _bottomBar.AddView(langButton);

        // 回车
        _enterButton = CreateBottomBarButton("↵");
        _enterButton.LayoutParameters = new LayoutParams(0, ViewGroup.LayoutParams.MatchParent, 0.8f)
        {
            LeftMargin = margin / 2, RightMargin = margin,
            TopMargin = margin / 2, BottomMargin = margin
        };
        _enterButton.Click += (_, _) =>
        {
            Key?.Invoke(this, new KeyboardKeyEventArgs(EnterKeyCode));
        };
        _bottomBar.AddView(_enterButton);

        AddView(_bottomBar);
    }

    // ── 按钮工厂 ────────────────────────────────────────

    private Button CreateSideButton(string label, float textSizeSp)
    {
        var button = new Button(Context)
        {
            Text = label,
            Gravity = GravityFlags.Center,
            TextAlignment = TextAlignment.Center
        };
        button.SetAllCaps(false);
        button.SetBackgroundResource(Resource.Drawable.key_function_background);
        button.SetTextColor(new Color(ContextCompat.GetColor(Context, Resource.Color.key_text_secondary)));
        button.SetTextSize(ComplexUnitType.Sp, textSizeSp);
        return button;
    }

    private Button CreateBottomBarButton(string label)
    {
        var button = new Button(Context)
        {
            Text = label,
            Gravity = GravityFlags.Center,
            TextAlignment = TextAlignment.Center
        };
        button.SetAllCaps(false);
        button.SetBackgroundResource(Resource.Drawable.key_function_background);
        button.SetTextColor(new Color(ContextCompat.GetColor(Context, Resource.Color.key_text_secondary)));
        button.SetTextSize(ComplexUnitType.Sp, 14f);
        button.SetPadding(0, 0, 0, 0);
        return button;
    }

    // ── 删除键触摸事件（支持长按连删） ───────────────────

    private void OnDeleteButtonTouch(object? sender, TouchEventArgs e)
    {
        if (e.Event == null)
        {
            return;
        }

        switch (e.Event.ActionMasked)
        {
            case MotionEventActions.Down:
                Press?.Invoke(this, new KeyboardKeyEventArgs(BackspaceKeyCode));
                e.Handled = true;
                break;

            case MotionEventActions.Up:
                Key?.Invoke(this, new KeyboardKeyEventArgs(BackspaceKeyCode));
                Release?.Invoke(this, new KeyboardKeyEventArgs(BackspaceKeyCode));
                e.Handled = true;
                break;

            case MotionEventActions.Cancel:
                Release?.Invoke(this, new KeyboardKeyEventArgs(BackspaceKeyCode));
                e.Handled = true;
                break;

            default:
                e.Handled = false;
                break;
        }
    }

    // ── 中间键盘事件转发 ────────────────────────────────

    private void BindCenterEvents()
    {
        if (_centerKeyboard == null || _centerEventsBound)
        {
            return;
        }

        _centerKeyboard.Key += OnCenterKey;
        _centerKeyboard.Press += OnCenterPress;
        _centerKeyboard.Release += OnCenterRelease;
        _centerEventsBound = true;
    }

    private void UnbindCenterEvents()
    {
        if (_centerKeyboard != null && _centerEventsBound)
        {
            _centerKeyboard.Key -= OnCenterKey;
            _centerKeyboard.Press -= OnCenterPress;
            _centerKeyboard.Release -= OnCenterRelease;
        }

        _centerEventsBound = false;
    }

    private void OnCenterKey(object? sender, KeyboardKeyEventArgs e)
    {
        Key?.Invoke(this, e);
    }

    private void OnCenterPress(object? sender, KeyboardKeyEventArgs e)
    {
        Press?.Invoke(this, e);
    }

    private void OnCenterRelease(object? sender, KeyboardKeyEventArgs e)
    {
        Release?.Invoke(this, e);
    }

    // ── 符号数据持久化 ──────────────────────────────────

    private List<QuickSymbolEntry> LoadSymbols()
    {
        try
        {
            var prefs = Context.GetSharedPreferences(PrefsName, FileCreationMode.Private);

            // 优先读 v2 格式
            string? jsonV2 = prefs?.GetString(SymbolsV2Key, null);
            if (!string.IsNullOrEmpty(jsonV2))
            {
                var entries = JsonSerializer.Deserialize<List<QuickSymbolEntry>>(jsonV2);
                if (entries != null && entries.Count > 0)
                {
                    return entries;
                }
            }

            // 回退读旧格式（纯字符串列表）
            string? json = prefs?.GetString(SymbolsKey, null);
            if (!string.IsNullOrEmpty(json))
            {
                var symbols = JsonSerializer.Deserialize<List<string>>(json);
                if (symbols != null && symbols.Count > 0)
                {
                    return symbols.Select(s => new QuickSymbolEntry { Display = s, Commit = s }).ToList();
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn(Tag, $"Failed to load symbols: {ex.Message}");
        }

        return DefaultSymbols.Select(e => new QuickSymbolEntry { Display = e.Display, Commit = e.Commit }).ToList();
    }

    // ── 拼音模式左栏切换 ──────────────────────────────────

    public void ShowPinyinOptions(List<string> pinyins)
    {
        if (_symbolContainer == null) return;

        _isPinyinMode = true;
        _symbolContainer.RemoveAllViews();

        // 最多显示 8 个结果（频率排序后最常用的在前）
        int limit = Math.Min(pinyins.Count, 8);
        for (int i = 0; i < limit; i++)
        {
            AddPinyinButton(pinyins[i]);
        }
    }

    public void RestoreSymbols()
    {
        if (!_isPinyinMode || _symbolContainer == null) return;

        _isPinyinMode = false;
        _symbolContainer.RemoveAllViews();

        if (_cachedSymbolEntries != null)
        {
            foreach (var entry in _cachedSymbolEntries)
            {
                AddSymbolButton(entry);
            }
        }

        AddCustomizeButton();
    }

    private void AddPinyinButton(string pinyin)
    {
        var button = new Button(Context)
        {
            Text = pinyin,
            Gravity = GravityFlags.Center,
            TextAlignment = TextAlignment.Center
        };
        button.SetAllCaps(false);
        button.SetBackgroundResource(Resource.Drawable.key_background);
        button.SetTextColor(new Color(ContextCompat.GetColor(Context, Resource.Color.key_text)));
        button.SetTextSize(ComplexUnitType.Sp, 13f);
        button.SetPadding(0, 0, 0, 0);
        button.SetIncludeFontPadding(false);

        int rowHeight = DpToPx(42);
        button.LayoutParameters = new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent, rowHeight);

        string py = pinyin;
        button.Click += (_, _) =>
        {
            PinyinSelected?.Invoke(this, py);
        };

        _symbolContainer?.AddView(button);
    }

    private int DpToPx(float dp)
    {
        return (int)TypedValue.ApplyDimension(ComplexUnitType.Dip, dp, Context.Resources.DisplayMetrics);
    }
}
