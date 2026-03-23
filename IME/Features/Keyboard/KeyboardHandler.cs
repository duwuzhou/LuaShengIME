using System;
using System.Collections.Generic;
using Android.Util;
using Android.Views;
using IME.Features.Candidates;
using IME.Features.Input;
using IME.Features.Input.Abstractions;
using IME.Features.Shortcuts;
using IME.Shared.Abstractions;
using IME.Shared.Utils.Enums;

namespace IME.Features.Keyboard;

public class KeyboardHandler
{
    private const int BackspaceKeyCode = -5;
    private const int ShiftKeyCode = -1;
    private const int SpaceKeyCode = 32;
    private const int BackspaceDelayRefreshIntervalMs = 1000;

    private readonly Ime _imeService;
    private readonly IInputEngineHost _engineHost;

    private readonly KeyboardLayoutManager _layoutManager;
    private readonly LongPressDetector _longPressDetector;
    private readonly CandidateManager _candidateManager;

    private CustomKeyboardView? _keyboardView;
    private T9KeyboardView? _t9KeyboardView;
    private LinearLayout? _linearLayout;
    private Gn1? _gn1;

    private bool _asciiMode;
    private bool _isT9Mode;
    private bool _wasT9BeforeSwitch;
    private bool _keyboardEventsBound;
    private bool _t9EventsBound;
    private bool _longPressBound;
    private bool _suppressNextBackspaceTap;
    private IKeyEventProcessor? _keyEventListener;
    private string _lastShiftLabel = string.Empty;
    private long _lastBackspaceDelayRefreshAt;
    private int _cachedBackspaceLongPressDelay;
    private string _t9DigitBuffer = "";
    private readonly List<int> _t9PinyinBoundaries = new();
    private readonly List<string> _t9SelectedPinyins = new();
    private bool _t9PinyinEventBound;
    private GnType _currentGnType = GnType.keyborad;
    private Gn1PanelMode _gn1PanelMode = Gn1PanelMode.Home;
    private string _gn1CurrentCategory = string.Empty;
    private List<string> _gn1CurrentItems = new();

    public KeyboardHandler(Ime imeService, IInputEngineHost engineHost)
    {
        _imeService = imeService;
        _engineHost = engineHost;

        _layoutManager = new KeyboardLayoutManager(imeService);
        _cachedBackspaceLongPressDelay = IME.Features.Settings.SettingsActivity.GetBackspaceLongPressDelay(imeService);
        _lastBackspaceDelayRefreshAt = Environment.TickCount64;
        _longPressDetector = new LongPressDetector
        {
            LongPressDelay = _cachedBackspaceLongPressDelay
        };
        _candidateManager = new CandidateManager();
    }

    public LinearLayout CreateKeyboardView()
    {
        DetachKeyboardEvents();
        if (_gn1 != null)
        {
            _gn1.StateChanged -= HandleGn1StateChanged;
        }

        _linearLayout = new LinearLayout(_imeService)
        {
            Orientation = Orientation.Vertical
        };

        var candidateView = new Candidate(_imeService, null, this);
        candidateView.SetImeService(_imeService);
        _candidateManager.SetCandidateView(candidateView);

        IInputEngine? engine = _engineHost.CurrentEngine;
        Log.Info(nameof(KeyboardHandler), $"CreateKeyboardView - engine: {(engine != null ? "set" : "null")}");
        if (engine != null)
        {
            _candidateManager.SetInputEngine(engine);
            bool useSimplified = IME.Features.Settings.SettingsActivity.GetSimplificationEnabled(_imeService);
            engine.SetSimplification(useSimplified);
            engine.SetAsciiMode(_asciiMode);
        }

        _linearLayout.AddView(candidateView);

        _keyboardView = new CustomKeyboardView(_imeService)
        {
            LayoutParameters = CreateKeyboardLayoutParams()
        };

        KeyboardLayoutModel initialLayout = _layoutManager.LoadInitialKeyboard();
        _keyboardView.RenderLayout(initialLayout);
        RefreshShiftKeyRole();
        _linearLayout.AddView(_keyboardView);

        _gn1 = new Gn1(_imeService, null);
        _gn1.SetImeService(_imeService);
        _gn1.RestoreState(_gn1PanelMode, _gn1CurrentCategory, _gn1CurrentItems);
        _gn1.StateChanged += HandleGn1StateChanged;
        _gn1.Visibility = ViewStates.Gone;
        _linearLayout.AddView(_gn1);

        BindEventsIfPossible();

        // 从偏好恢复 T9 模式
        if (IME.Features.Settings.SettingsActivity.GetT9Enabled(_imeService) && !_asciiMode)
        {
            SwitchToT9Mode();
        }

        // 恢复上一次的 GN 面板状态
        SwitchViewgn(_currentGnType);

        return _linearLayout;
    }

    private LinearLayout.LayoutParams CreateKeyboardLayoutParams()
    {
        var layoutParams = new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.WrapContent);
        int verticalMargin = Dp(_imeService, 1);
        layoutParams.TopMargin = verticalMargin;
        layoutParams.BottomMargin = verticalMargin;
        return layoutParams;
    }

    private static int Dp(Android.Content.Context context, int value)
    {
        return (int)TypedValue.ApplyDimension(ComplexUnitType.Dip, value, context.Resources.DisplayMetrics);
    }

    public void SwitchViewgn(GnType gnType)
    {
        if (_gn1 == null)
        {
            return;
        }

        _currentGnType = gnType;

        View? activeKeyboardView = _isT9Mode ? (View?)_t9KeyboardView : _keyboardView;
        if (activeKeyboardView == null)
        {
            return;
        }

        switch (gnType)
        {
            case GnType.keyborad:
                activeKeyboardView.Visibility = ViewStates.Visible;
                _gn1.Visibility = ViewStates.Gone;
                break;
            case GnType.gn1:
                activeKeyboardView.Visibility = ViewStates.Gone;
                _gn1.UpdateViews();
                _gn1.Visibility = ViewStates.Visible;
                break;
        }
    }

    public void ShowFunctionHome()
    {
        _gn1?.ShowHome();
    }

    public void SwitchKeyboard(IKeyboardType type)
    {
        KeyboardLayoutModel layout = _layoutManager.SwitchKeyboard(type);
        _keyboardView?.RenderLayout(layout);
        _lastShiftLabel = string.Empty;
        RefreshShiftKeyRole();
    }

    public bool SwitchAsciiMode()
    {
        _asciiMode = !_asciiMode;

        IInputEngine? engine = _engineHost.CurrentEngine;
        engine?.SetAsciiMode(_asciiMode);

        if (_asciiMode && _isT9Mode)
        {
            // 切到英文：销毁 T9 视图，切 Rime 方案，渲染 QWERTY
            ExitT9Temporarily();

            if (engine is Shared.InputEngine.RimeInputEngine rimeEngine)
            {
                rimeEngine.SwitchSchema("luna_pinyin");
            }

            KeyboardLayoutModel layout = _layoutManager.SwitchByAsciiMode(_asciiMode);
            _keyboardView?.RenderLayout(layout);
            _lastShiftLabel = string.Empty;
            RefreshShiftKeyRole();
        }
        else if (!_asciiMode && _wasT9BeforeSwitch)
        {
            // 切回中文：恢复 T9 模式
            _wasT9BeforeSwitch = false;
            SwitchToT9Mode();
        }
        else
        {
            KeyboardLayoutModel layout = _layoutManager.SwitchByAsciiMode(_asciiMode);
            _keyboardView?.RenderLayout(layout);
            _lastShiftLabel = string.Empty;
            RefreshShiftKeyRole();
        }

        Log.Info(nameof(KeyboardHandler), $"Switch to {(_asciiMode ? "ASCII" : "Chinese")} mode");

        engine?.Reset();

        return _asciiMode;
    }

    public bool GetAsciiMode() => _asciiMode;

    public bool IsT9Mode => _isT9Mode;

    public void SwitchToT9Mode()
    {
        if (_keyboardView == null || _linearLayout == null)
        {
            return;
        }

        _isT9Mode = true;
        IME.Features.Settings.SettingsActivity.SetT9Enabled(_imeService, true);
        SwitchKeyboard(IKeyboardType.T9);

        // 解绑 _keyboardView 上的事件
        DetachKeyboardEvents();

        // 记住 _keyboardView 在 _linearLayout 中的位置
        int keyboardIndex = _linearLayout.IndexOfChild(_keyboardView);
        if (keyboardIndex < 0)
        {
            Log.Warn(nameof(KeyboardHandler), "SwitchToT9Mode: _keyboardView not found in _linearLayout");
            return;
        }

        // 从 _linearLayout 移除 _keyboardView（T9KeyboardView 会接管）
        _linearLayout.RemoveView(_keyboardView);

        // 创建 T9 复合视图
        _t9KeyboardView = new T9KeyboardView(_imeService, _keyboardView)
        {
            LayoutParameters = CreateKeyboardLayoutParams()
        };

        // 插入到原来的位置
        _linearLayout.AddView(_t9KeyboardView, keyboardIndex);

        // 绑定 T9 复合视图事件
        BindT9Events();

        if (_engineHost.CurrentEngine is Shared.InputEngine.RimeInputEngine rimeEngine)
        {
            rimeEngine.SwitchSchema("t9_pinyin");
        }

        Log.Info(nameof(KeyboardHandler), "Switched to T9 mode");
    }

    public void SwitchFromT9ToQwerty()
    {
        _engineHost.CurrentEngine?.Reset();
        ClearCandidates();

        _isT9Mode = false;
        _wasT9BeforeSwitch = false;
        IME.Features.Settings.SettingsActivity.SetT9Enabled(_imeService, false);

        // 解绑 T9 复合视图事件
        DetachT9Events();

        if (_t9KeyboardView != null && _linearLayout != null)
        {
            // 取回 _keyboardView
            _keyboardView = _t9KeyboardView.ExtractCenterKeyboard();

            // 替换视图
            int t9Index = _linearLayout.IndexOfChild(_t9KeyboardView);
            _linearLayout.RemoveView(_t9KeyboardView);
            _t9KeyboardView.Cleanup();
            _t9KeyboardView = null;

            if (_keyboardView != null && t9Index >= 0)
            {
                _keyboardView.LayoutParameters = CreateKeyboardLayoutParams();
                _linearLayout.AddView(_keyboardView, t9Index);
            }
        }

        // 切换到 QWERTY 布局
        var targetType = _asciiMode ? IKeyboardType.UpperCase : IKeyboardType.LowerCase;
        SwitchKeyboard(targetType);

        // 重新绑定 _keyboardView 事件
        BindEventsIfPossible();

        if (_engineHost.CurrentEngine is Shared.InputEngine.RimeInputEngine rimeEngine)
        {
            rimeEngine.SwitchSchema("luna_pinyin");
        }

        Log.Info(nameof(KeyboardHandler), "Switched from T9 to QWERTY");
    }

    public void ShowLayoutPicker()
    {
        PrepareNonT9Switch();
        SwitchKeyboard(IKeyboardType.LayoutPicker);
    }

    /// <summary>
    /// T9 模式下临时切走（符号/数字/中英等）时调用。
    /// 拆解 T9 复合视图，恢复裸 _keyboardView，不切 Rime 方案。
    /// </summary>
    private void ExitT9Temporarily()
    {
        _engineHost.CurrentEngine?.Reset();
        ClearCandidates();

        _isT9Mode = false;
        _wasT9BeforeSwitch = true;

        DetachT9Events();

        if (_t9KeyboardView != null && _linearLayout != null)
        {
            _keyboardView = _t9KeyboardView.ExtractCenterKeyboard();

            int t9Index = _linearLayout.IndexOfChild(_t9KeyboardView);
            _linearLayout.RemoveView(_t9KeyboardView);
            _t9KeyboardView.Cleanup();
            _t9KeyboardView = null;

            if (_keyboardView != null && t9Index >= 0)
            {
                _keyboardView.LayoutParameters = CreateKeyboardLayoutParams();
                _linearLayout.AddView(_keyboardView, t9Index);
            }
        }

        BindEventsIfPossible();
    }

    /// <summary>
    /// 从 T9 切换到非 T9 键盘前调用（符号/数字等）。
    /// </summary>
    public void PrepareNonT9Switch()
    {
        if (_isT9Mode)
        {
            ExitT9Temporarily();
        }
    }

    /// <summary>
    /// 切回字母键盘时调用，若之前是 T9 模式则恢复。
    /// </summary>
    /// <returns>true 表示已恢复 T9，调用方无需再切键盘。</returns>
    public bool TryRestoreT9()
    {
        if (_wasT9BeforeSwitch && !_asciiMode)
        {
            _wasT9BeforeSwitch = false;
            SwitchToT9Mode();
            return true;
        }

        return false;
    }

    public void SetKeyEventListener(IKeyEventProcessor listener)
    {
        _keyEventListener = listener;
        if (_isT9Mode)
        {
            BindT9Events();
        }
        else
        {
            BindEventsIfPossible();
        }
    }

    public bool TryHandlePinyinEditKey(int keyCode)
    {
        if (_asciiMode)
        {
            return false;
        }

        return _candidateManager.TryHandlePinyinEditKey(keyCode);
    }

    public void UpdateChineseCandidates()
    {
        string? displayComposingOverride = BuildT9DisplayComposingText();
        bool hideNumericCandidates = _isT9Mode && !_asciiMode;
        _candidateManager.UpdateChineseCandidates(_engineHost.CurrentEngine, displayComposingOverride, hideNumericCandidates);
        RefreshShiftKeyRole();
    }

    public void ClearCandidates()
    {
        _candidateManager.Clear();
        RefreshShiftKeyRole();
        if (_isT9Mode) ResetT9PinyinState();
    }

    /// <summary>
    /// 候选词上屏后调用，仅重置 T9 数字 buffer 和左栏拼音，不触发候选清空循环。
    /// </summary>
    public void NotifyT9BufferShouldReset()
    {
        if (_isT9Mode)
        {
            ResetT9PinyinState();
        }
    }

    public void ShowPredictionCandidates(IReadOnlyList<string> predictions, int maxResults)
    {
        _candidateManager.ShowPredictionCandidates(predictions, maxResults);
        RefreshShiftKeyRole();
    }

    public void NotifyEngineChanged(IInputEngine? engine)
    {
        if (engine != null)
        {
            _candidateManager.NotifyEngineChanged(engine);
            bool useSimplified = IME.Features.Settings.SettingsActivity.GetSimplificationEnabled(_imeService);
            engine.SetSimplification(useSimplified);
            engine.SetAsciiMode(_asciiMode);

            // T9 模式下，确保新引擎切换到 T9 拼音方案
            if (_isT9Mode && engine is Shared.InputEngine.RimeInputEngine rimeEngine)
            {
                rimeEngine.SwitchSchema("t9_pinyin");
                Log.Info(nameof(KeyboardHandler), "Restored t9_pinyin schema after engine change");
            }
        }

        _lastShiftLabel = string.Empty;
        RefreshShiftKeyRole();
    }

    public void Cleanup()
    {
        DetachKeyboardEvents();
        DetachT9Events();

        if (_longPressBound)
        {
            _longPressDetector.OnLongPress -= HandleLongPress;
            _longPressBound = false;
        }

        _longPressDetector.Cleanup();
        _layoutManager.Cleanup();
        _candidateManager.Cleanup();

        _t9KeyboardView?.Cleanup();
        _t9KeyboardView = null;
        _t9DigitBuffer = "";
        _t9PinyinBoundaries.Clear();
        _t9SelectedPinyins.Clear();
        _keyboardView = null;
        if (_gn1 != null)
        {
            _gn1.StateChanged -= HandleGn1StateChanged;
        }
        _gn1 = null;
        _linearLayout = null;
        _keyEventListener = null;
    }

    private void BindEventsIfPossible()
    {
        if (_keyboardView == null || _keyEventListener == null || _keyboardEventsBound)
        {
            return;
        }

        _keyboardView.Key += HandleKey;
        _keyboardView.Press += HandlePress;
        _keyboardView.Release += HandleRelease;
        _keyboardEventsBound = true;

        if (!_longPressBound)
        {
            _longPressDetector.OnLongPress += HandleLongPress;
            _longPressBound = true;
        }
    }

    private void DetachKeyboardEvents()
    {
        if (_keyboardView != null && _keyboardEventsBound)
        {
            _keyboardView.Key -= HandleKey;
            _keyboardView.Press -= HandlePress;
            _keyboardView.Release -= HandleRelease;
        }

        _keyboardEventsBound = false;
        _suppressNextBackspaceTap = false;
    }

    private void BindT9Events()
    {
        if (_t9KeyboardView == null || _keyEventListener == null || _t9EventsBound)
        {
            return;
        }

        _t9KeyboardView.Key += HandleKey;
        _t9KeyboardView.Press += HandlePress;
        _t9KeyboardView.Release += HandleRelease;
        _t9EventsBound = true;

        if (!_t9PinyinEventBound)
        {
            _t9KeyboardView.PinyinSelected += HandlePinyinSelected;
            _t9PinyinEventBound = true;
        }

        if (!_longPressBound)
        {
            _longPressDetector.OnLongPress += HandleLongPress;
            _longPressBound = true;
        }
    }

    private void DetachT9Events()
    {
        if (_t9KeyboardView != null && _t9EventsBound)
        {
            _t9KeyboardView.Key -= HandleKey;
            _t9KeyboardView.Press -= HandlePress;
            _t9KeyboardView.Release -= HandleRelease;
        }

        if (_t9KeyboardView != null && _t9PinyinEventBound)
        {
            _t9KeyboardView.PinyinSelected -= HandlePinyinSelected;
            _t9PinyinEventBound = false;
        }

        _t9EventsBound = false;
    }

    private void HandleKey(object? sender, KeyboardKeyEventArgs e)
    {
        if (_keyEventListener == null)
        {
            return;
        }

        if (e.PrimaryCode == BackspaceKeyCode && _suppressNextBackspaceTap)
        {
            _suppressNextBackspaceTap = false;
            return;
        }

        // 多字符文本直接上屏（自定义符号等）
        if (e.Text != null && e.Text.Length > 1)
        {
            _keyEventListener.OnTextCommit(e.Text);
            ResetT9PinyinState();
            return;
        }

        // T9 数字 buffer 追踪
        if (_isT9Mode && !_asciiMode)
        {
            char ch = (char)e.PrimaryCode;
            if (ch >= '2' && ch <= '9')
            {
                _t9DigitBuffer += ch;
            }
            else if (e.PrimaryCode == BackspaceKeyCode)
            {
                if (_t9DigitBuffer.Length > 0)
                {
                    _t9DigitBuffer = _t9DigitBuffer.Substring(0, _t9DigitBuffer.Length - 1);
                    // 只移除超出 buffer 长度的边界，保留有效的已选拼音分词
                    while (_t9PinyinBoundaries.Count > 0
                           && _t9PinyinBoundaries[_t9PinyinBoundaries.Count - 1] > _t9DigitBuffer.Length)
                    {
                        _t9PinyinBoundaries.RemoveAt(_t9PinyinBoundaries.Count - 1);
                    }

                    while (_t9SelectedPinyins.Count > _t9PinyinBoundaries.Count)
                    {
                        _t9SelectedPinyins.RemoveAt(_t9SelectedPinyins.Count - 1);
                    }
                }
            }
            else if (e.PrimaryCode == SpaceKeyCode || ch == '0')
            {
                // 选词/空格后清空
                ResetT9PinyinState();
            }
        }

        _keyEventListener.OnKeyPressed(e.PrimaryCode);

        if (!_asciiMode && ShouldAutoRefreshCandidates(e.PrimaryCode))
        {
            UpdateChineseCandidates();
        }

        // 更新 T9 拼音选项
        if (_isT9Mode && !_asciiMode)
        {
            UpdateT9PinyinOptions();
        }
    }

    private void RefreshShiftKeyRole()
    {
        if (_keyboardView == null)
        {
            return;
        }

        string label = ShouldUseSegmentationShift() ? "分词" : GetShiftDefaultLabel();
        if (string.Equals(_lastShiftLabel, label, StringComparison.Ordinal))
        {
            return;
        }

        _lastShiftLabel = label;
        _keyboardView.SetKeyLabel(ShiftKeyCode, label);
    }

    private bool ShouldUseSegmentationShift()
    {
        if (_asciiMode)
        {
            return false;
        }

        string? composingText = _engineHost.CurrentEngine?.GetComposingText();
        return !string.IsNullOrEmpty(composingText);
    }

    private string GetShiftDefaultLabel()
    {
        KeyboardLayoutModel? layout = _layoutManager.CurrentLayout;
        if (layout == null)
        {
            return "Shift";
        }

        for (int rowIndex = 0; rowIndex < layout.Rows.Count; rowIndex++)
        {
            KeyboardRowModel row = layout.Rows[rowIndex];
            for (int keyIndex = 0; keyIndex < row.Keys.Count; keyIndex++)
            {
                KeyboardKeyModel key = row.Keys[keyIndex];
                if (key.Code != ShiftKeyCode)
                {
                    continue;
                }

                return string.IsNullOrWhiteSpace(key.Label) ? "Shift" : key.Label;
            }
        }

        return "Shift";
    }

    private bool ShouldAutoRefreshCandidates(int keyCode)
    {
        if (keyCode <= 0 || keyCode == SpaceKeyCode)
        {
            return false;
        }

        char ch = (char)keyCode;

        if (_isT9Mode && ch >= '2' && ch <= '9')
        {
            return true;
        }

        return (ch >= 'a' && ch <= 'z')
               || (ch >= 'A' && ch <= 'Z')
               || ch == '\'';
    }

    // ── T9 拼音纠正 ──────────────────────────────────────

    private void UpdateT9PinyinOptions()
    {
        if (_t9KeyboardView == null) return;
        NormalizeT9SelectionState();

        if (_t9DigitBuffer.Length == 0)
        {
            _t9KeyboardView.RestoreSymbols();
            return;
        }

        int resolvedLen = _t9PinyinBoundaries.Count > 0
            ? _t9PinyinBoundaries[_t9PinyinBoundaries.Count - 1]
            : 0;
        if (resolvedLen < 0)
        {
            resolvedLen = 0;
        }
        if (resolvedLen > _t9DigitBuffer.Length)
        {
            resolvedLen = _t9DigitBuffer.Length;
        }
        string unresolvedDigits = _t9DigitBuffer.Substring(resolvedLen);
        if (unresolvedDigits.Length == 0)
        {
            _t9KeyboardView.RestoreSymbols();
            return;
        }

        var pinyins = BuildT9CorrectionOptions(unresolvedDigits);
        if (pinyins.Count > 0)
        {
            _t9KeyboardView.ShowPinyinOptions(pinyins);
        }
        else
        {
            _t9KeyboardView.RestoreSymbols();
        }
    }

    private void ResetT9PinyinState()
    {
        _t9DigitBuffer = "";
        _t9PinyinBoundaries.Clear();
        _t9SelectedPinyins.Clear();
        _t9KeyboardView?.RestoreSymbols();
    }

    private void HandlePinyinSelected(object? sender, string pinyin)
    {
        if (string.IsNullOrEmpty(pinyin) || _t9DigitBuffer.Length == 0) return;
        NormalizeT9SelectionState();

        int pinyinDigitLen = T9PinyinHelper.GetDigitLength(pinyin);
        if (pinyinDigitLen <= 0)
        {
            return;
        }

        int resolvedLen = _t9PinyinBoundaries.Count > 0
            ? _t9PinyinBoundaries[_t9PinyinBoundaries.Count - 1]
            : 0;
        int newBoundary = resolvedLen + pinyinDigitLen;
        if (newBoundary > _t9DigitBuffer.Length)
        {
            return;
        }

        _t9PinyinBoundaries.Add(newBoundary);
        _t9SelectedPinyins.Add(pinyin);

        // 先尝试按「已选拼音 + 剩余数字」重放，失败则回退到纯数字重放。
        IInputEngine? engine = _engineHost.CurrentEngine;
        if (engine == null) return;

        bool appliedSelection = ReplayT9BufferToEngine(engine);
        if (!appliedSelection)
        {
            // 本次纠错未被引擎接受，回滚本次选择，避免预览与引擎状态不一致。
            if (_t9PinyinBoundaries.Count > 0)
            {
                _t9PinyinBoundaries.RemoveAt(_t9PinyinBoundaries.Count - 1);
            }

            if (_t9SelectedPinyins.Count > 0)
            {
                _t9SelectedPinyins.RemoveAt(_t9SelectedPinyins.Count - 1);
            }
        }

        NormalizeT9SelectionState();
        UpdateChineseCandidates();
        UpdateT9PinyinOptions();
    }

    private string? BuildT9DisplayComposingText()
    {
        if (!_isT9Mode || _asciiMode)
        {
            return null;
        }
        NormalizeT9SelectionState();

        if (_t9DigitBuffer.Length == 0)
        {
            return string.Empty;
        }

        string resolvedPinyinText = BuildResolvedT9PinyinText();

        int resolvedLen = _t9PinyinBoundaries.Count > 0
            ? _t9PinyinBoundaries[_t9PinyinBoundaries.Count - 1]
            : 0;
        if (resolvedLen < 0)
        {
            resolvedLen = 0;
        }
        if (resolvedLen > _t9DigitBuffer.Length)
        {
            resolvedLen = _t9DigitBuffer.Length;
        }

        string unresolvedDigits = _t9DigitBuffer.Substring(resolvedLen);
        string unresolvedPreview = BuildUnresolvedT9Preview(unresolvedDigits);

        if (!string.IsNullOrEmpty(resolvedPinyinText) && !string.IsNullOrEmpty(unresolvedPreview))
        {
            return $"{resolvedPinyinText}'{unresolvedPreview}";
        }

        if (!string.IsNullOrEmpty(resolvedPinyinText))
        {
            return resolvedPinyinText;
        }

        if (!string.IsNullOrEmpty(unresolvedPreview))
        {
            return unresolvedPreview;
        }

        // 安全兜底：当 buffer 与引擎状态短暂不同步时，优先展示引擎当前串。
        string composingText = _engineHost.CurrentEngine?.GetComposingText() ?? string.Empty;
        return composingText;
    }

    private string BuildResolvedT9PinyinText()
    {
        if (_t9SelectedPinyins.Count == 0)
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder();
        int count = Math.Min(_t9SelectedPinyins.Count, _t9PinyinBoundaries.Count);
        for (int i = 0; i < count; i++)
        {
            string syllable = _t9SelectedPinyins[i];
            if (string.IsNullOrWhiteSpace(syllable))
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append('\'');
            }

            builder.Append(syllable.ToLowerInvariant());
        }

        return builder.ToString();
    }

    private static string BuildUnresolvedT9Preview(string unresolvedDigits)
    {
        if (string.IsNullOrEmpty(unresolvedDigits))
        {
            return string.Empty;
        }

        var parts = new List<string>();
        int cursor = 0;
        int safety = 24;

        while (cursor < unresolvedDigits.Length && safety-- > 0)
        {
            string remaining = unresolvedDigits.Substring(cursor);
            List<string> hints = T9PinyinHelper.GetMatchingSyllables(remaining);
            if (hints.Count == 0)
            {
                parts.Add(remaining);
                break;
            }

            string? best = null;
            for (int i = 0; i < hints.Count; i++)
            {
                string candidate = hints[i];
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    continue;
                }

                if (best == null || candidate.Length > best.Length)
                {
                    best = candidate;
                }
            }

            if (string.IsNullOrEmpty(best))
            {
                parts.Add(remaining);
                break;
            }

            parts.Add(best.ToLowerInvariant());

            int consume = T9PinyinHelper.GetDigitLength(best);
            if (consume <= 0)
            {
                parts.Add(remaining);
                break;
            }

            if (consume > remaining.Length)
            {
                consume = remaining.Length;
            }

            cursor += consume;
        }

        return string.Join("'", parts);
    }

    private static List<string> BuildT9CorrectionOptions(string unresolvedDigits)
    {
        List<string> hints = T9PinyinHelper.GetMatchingSyllables(unresolvedDigits);
        if (hints.Count == 0)
        {
            return hints;
        }

        var byLength = new Dictionary<int, List<string>>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < hints.Count; i++)
        {
            string candidate = hints[i];
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            string normalized = candidate.ToLowerInvariant();
            if (!seen.Add(normalized))
            {
                continue;
            }

            int len = normalized.Length;
            if (len <= 0 || len > unresolvedDigits.Length)
            {
                continue;
            }

            if (!byLength.TryGetValue(len, out List<string>? bucket))
            {
                bucket = new List<string>();
                byLength[len] = bucket;
            }

            // 每个长度保留前 2 个高频拼音，既能纠错也能继续分段。
            if (bucket.Count < 2)
            {
                bucket.Add(normalized);
            }
        }

        var results = new List<string>(8);
        for (int len = 1; len <= unresolvedDigits.Length && results.Count < 8; len++)
        {
            if (!byLength.TryGetValue(len, out List<string>? bucket))
            {
                continue;
            }

            for (int i = 0; i < bucket.Count && results.Count < 8; i++)
            {
                results.Add(bucket[i]);
            }
        }

        if (results.Count > 0)
        {
            return results;
        }

        for (int i = 0; i < hints.Count && results.Count < 8; i++)
        {
            string candidate = hints[i];
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                results.Add(candidate.ToLowerInvariant());
            }
        }

        return results;
    }

    private bool ReplayT9BufferToEngine(IInputEngine engine)
    {
        if (!TryReplayWithSelectedPinyin(engine))
        {
            ReplayWithDigits(engine);
            return false;
        }

        return true;
    }

    private bool TryReplayWithSelectedPinyin(IInputEngine engine)
    {
        if (_t9SelectedPinyins.Count == 0)
        {
            return false;
        }

        int resolvedLen = _t9PinyinBoundaries.Count > 0
            ? _t9PinyinBoundaries[_t9PinyinBoundaries.Count - 1]
            : 0;
        string unresolvedDigits = _t9DigitBuffer.Substring(Math.Min(resolvedLen, _t9DigitBuffer.Length));

        var normalizedSyllables = new List<string>(_t9SelectedPinyins.Count);
        for (int i = 0; i < _t9SelectedPinyins.Count; i++)
        {
            string syllable = _t9SelectedPinyins[i];
            if (!string.IsNullOrWhiteSpace(syllable))
            {
                normalizedSyllables.Add(syllable);
            }
        }

        var replayText = new List<char>();
        for (int i = 0; i < normalizedSyllables.Count; i++)
        {
            string syllable = normalizedSyllables[i];

            for (int j = 0; j < syllable.Length; j++)
            {
                replayText.Add(char.ToLowerInvariant(syllable[j]));
            }

            bool needDelimiter = i < normalizedSyllables.Count - 1 || unresolvedDigits.Length > 0;
            if (needDelimiter)
            {
                replayText.Add(' ');
            }
        }

        for (int i = 0; i < unresolvedDigits.Length; i++)
        {
            replayText.Add(unresolvedDigits[i]);
        }

        if (replayText.Count == 0)
        {
            return false;
        }

        engine.Reset();
        bool acceptedAny = false;
        for (int i = 0; i < replayText.Count; i++)
        {
            bool accepted = engine.ProcessKey(replayText[i]);
            if (!accepted)
            {
                return false;
            }

            acceptedAny = true;
        }

        return acceptedAny;
    }

    private void NormalizeT9SelectionState()
    {
        if (_t9DigitBuffer.Length <= 0)
        {
            _t9PinyinBoundaries.Clear();
            _t9SelectedPinyins.Clear();
            return;
        }

        int maxLen = _t9DigitBuffer.Length;
        for (int i = _t9PinyinBoundaries.Count - 1; i >= 0; i--)
        {
            int boundary = _t9PinyinBoundaries[i];
            int previous = i > 0 ? _t9PinyinBoundaries[i - 1] : 0;
            if (boundary <= previous || boundary > maxLen)
            {
                _t9PinyinBoundaries.RemoveAt(i);
            }
        }

        while (_t9SelectedPinyins.Count > _t9PinyinBoundaries.Count)
        {
            _t9SelectedPinyins.RemoveAt(_t9SelectedPinyins.Count - 1);
        }

        while (_t9PinyinBoundaries.Count > _t9SelectedPinyins.Count)
        {
            _t9PinyinBoundaries.RemoveAt(_t9PinyinBoundaries.Count - 1);
        }
    }

    private void ReplayWithDigits(IInputEngine engine)
    {
        engine.Reset();

        int boundaryIdx = 0;
        for (int i = 0; i < _t9DigitBuffer.Length; i++)
        {
            if (boundaryIdx < _t9PinyinBoundaries.Count
                && i == _t9PinyinBoundaries[boundaryIdx]
                && i < _t9DigitBuffer.Length)
            {
                engine.ProcessKey(32);
                boundaryIdx++;
            }

            engine.ProcessKey(_t9DigitBuffer[i]);
        }
    }

    private void HandlePress(object? sender, KeyboardKeyEventArgs e)
    {
        if (e.PrimaryCode == BackspaceKeyCode)
        {
            RefreshBackspaceLongPressDelay();
        }

        _longPressDetector.StartDetection(e.PrimaryCode);
    }

    private void RefreshBackspaceLongPressDelay(bool force = false)
    {
        long now = Environment.TickCount64;
        if (!force && (now - _lastBackspaceDelayRefreshAt) < BackspaceDelayRefreshIntervalMs)
        {
            return;
        }

        _lastBackspaceDelayRefreshAt = now;
        int delay = IME.Features.Settings.SettingsActivity.GetBackspaceLongPressDelay(_imeService);
        if (delay == _cachedBackspaceLongPressDelay)
        {
            return;
        }

        _cachedBackspaceLongPressDelay = delay;
        _longPressDetector.LongPressDelay = delay;
    }

    private void HandleRelease(object? sender, KeyboardKeyEventArgs e)
    {
        _longPressDetector.CancelDetection();
        _keyEventListener?.OnKeyRelease(e.PrimaryCode);

        if (e.PrimaryCode == BackspaceKeyCode)
        {
            _suppressNextBackspaceTap = false;
        }
    }

    private void HandleLongPress(int keyCode)
    {
        if (keyCode == BackspaceKeyCode)
        {
            _suppressNextBackspaceTap = true;
        }

        _keyEventListener?.OnKeyLongPress(keyCode);
    }

    private void HandleGn1StateChanged(object? sender, Gn1StateChangedEventArgs e)
    {
        _gn1PanelMode = e.PanelMode;
        _gn1CurrentCategory = e.CurrentCategory ?? string.Empty;
        _gn1CurrentItems = e.CurrentItems.Count > 0
            ? new List<string>(e.CurrentItems)
            : new List<string>();
    }
}



