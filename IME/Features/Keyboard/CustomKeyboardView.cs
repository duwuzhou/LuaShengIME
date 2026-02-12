using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Android.Content;
using Android.Graphics;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidX.Core.Content;
using IME.Features.Settings;
using IME.Shared.Utils.Enums;

namespace IME.Features.Keyboard;

public sealed class KeyboardKeyEventArgs : EventArgs
{
    public KeyboardKeyEventArgs(int primaryCode)
    {
        PrimaryCode = primaryCode;
    }

    public KeyboardKeyEventArgs(int primaryCode, string? text) : this(primaryCode)
    {
        Text = text;
    }

    public int PrimaryCode { get; }
    public string? Text { get; }
}

public sealed class CustomKeyboardView : LinearLayout
{
    private const string TouchLogTag = "IME.Touch";
    private const bool LockToDownKey = false;
    private const float KeyReleaseSlopDp = 8f;
    private const float CrossKeyReleaseSlopDp = 4f;
    private const float RowSwitchSlopFactor = 0.33f;
    private const float ExtraHorizontalGapDp = 3f;
    private const float ExtraVerticalGapDp = 2f;
    private const float TouchYOffsetDp = 0f;
    private const int DefaultTapFallbackMaxDurationMs = 180;
    private const float DefaultTapFallbackMoveSlopDp = 14f;
    private const int TapFallbackSettingsRefreshIntervalMs = 1000;

    private readonly Dictionary<int, List<Button>> _buttonsByCode = new();
    private readonly Dictionary<Button, int> _buttonCodeMap = new();
    private readonly Dictionary<Button, int> _buttonRowMap = new();
    private readonly List<RowViewState> _rowStates = new();
    private readonly int[] _tempLocation = new int[2];
    private readonly Dictionary<Button, Rect> _buttonBoundsCache = new();
    private bool _boundsValid;

    private readonly int _keyReleaseSlopPx;
    private readonly int _crossKeyReleaseSlopPx;
    private readonly int _touchYOffsetPx;
    private int _lastKeyHeightPx;
    private int _tapFallbackMoveSlopPx;
    private int _tapFallbackMaxDurationMs;
    private bool _tapFallbackEnabled = true;
    private long _lastTapFallbackSettingsRefreshAt;
    private readonly Dictionary<int, PointerState> _pointerStates = new();
    private readonly Dictionary<Button, int> _pressedButtonCounts = new();

    public event EventHandler<KeyboardKeyEventArgs>? Key;
    public event EventHandler<KeyboardKeyEventArgs>? Press;
    public event EventHandler<KeyboardKeyEventArgs>? Release;

    public CustomKeyboardView(Context context) : base(context)
    {
        Orientation = Orientation.Vertical;
        LayoutParameters = new LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
        SetBackgroundColor(new Color(ContextCompat.GetColor(context, Resource.Color.keyboard_background)));
        _keyReleaseSlopPx = DpToPx(KeyReleaseSlopDp);
        _crossKeyReleaseSlopPx = DpToPx(CrossKeyReleaseSlopDp);
        _touchYOffsetPx = DpToPx(TouchYOffsetDp);
        _tapFallbackMoveSlopPx = DpToPx(DefaultTapFallbackMoveSlopDp);
        _tapFallbackMaxDurationMs = DefaultTapFallbackMaxDurationMs;
        RefreshTapFallbackSettings(true);
    }

    public void RenderLayout(KeyboardLayoutModel layoutModel)
    {
        if (layoutModel == null)
        {
            return;
        }

        int keyHeightPx = DpToPx(layoutModel.KeyHeightDp);
        int verticalGapPx = DpToPx(layoutModel.VerticalGapDp + ExtraVerticalGapDp);
        int horizontalGapPx = DpToPx(layoutModel.HorizontalGapDp + ExtraHorizontalGapDp);

        _buttonsByCode.Clear();
        _buttonCodeMap.Clear();
        _buttonRowMap.Clear();
        _buttonBoundsCache.Clear();
        _boundsValid = false;
        _pointerStates.Clear();
        _pressedButtonCounts.Clear();
        _lastKeyHeightPx = keyHeightPx;

        if (!CanReuseLayoutStructure(layoutModel))
        {
            RebuildLayout(layoutModel, keyHeightPx, verticalGapPx, horizontalGapPx);
            return;
        }

        UpdateExistingLayout(layoutModel, keyHeightPx, verticalGapPx, horizontalGapPx);
    }

    public void SetKeyLabel(int keyCode, string label)
    {
        if (!_buttonsByCode.TryGetValue(keyCode, out List<Button>? buttons))
        {
            return;
        }

        string text = label ?? string.Empty;
        for (int i = 0; i < buttons.Count; i++)
        {
            if (!string.Equals(buttons[i].Text, text, StringComparison.Ordinal))
            {
                buttons[i].Text = text;
            }
        }
    }

    private bool CanReuseLayoutStructure(KeyboardLayoutModel layoutModel)
    {
        if (_rowStates.Count != layoutModel.Rows.Count)
        {
            return false;
        }

        for (int rowIndex = 0; rowIndex < layoutModel.Rows.Count; rowIndex++)
        {
            if (_rowStates[rowIndex].Keys.Count != layoutModel.Rows[rowIndex].Keys.Count)
            {
                return false;
            }
        }

        return _rowStates.Count > 0;
    }

    private static float ComputeRowWeightSum(KeyboardRowModel rowModel)
    {
        return Math.Max(1f, rowModel.Keys.Sum(key => Math.Max(0.01f, key.WidthPercent)));
    }

    private static float ComputeMaxRowWeightSum(KeyboardLayoutModel layoutModel)
    {
        float max = 1f;
        for (int i = 0; i < layoutModel.Rows.Count; i++)
        {
            max = Math.Max(max, ComputeRowWeightSum(layoutModel.Rows[i]));
        }

        return max;
    }

    private static bool ShouldUseSogouSecondRowNoGap(IKeyboardType layoutType, int rowIndex)
    {
        return rowIndex == 1 && (layoutType == IKeyboardType.LowerCase || layoutType == IKeyboardType.UpperCase);
    }

    private void RebuildLayout(KeyboardLayoutModel layoutModel, int keyHeightPx, int verticalGapPx, int horizontalGapPx)
    {
        RemoveAllViews();
        _rowStates.Clear();

        float maxRowWeightSum = ComputeMaxRowWeightSum(layoutModel);

        for (int rowIndex = 0; rowIndex < layoutModel.Rows.Count; rowIndex++)
        {
            KeyboardRowModel rowModel = layoutModel.Rows[rowIndex];
            LinearLayout rowView = new(Context)
            {
                Orientation = Orientation.Horizontal
            };

            ApplyRowLayout(rowView, rowModel, rowIndex, verticalGapPx, maxRowWeightSum);

            var rowState = new RowViewState(rowView);
            for (int keyIndex = 0; keyIndex < rowModel.Keys.Count; keyIndex++)
            {
                KeyboardKeyModel keyModel = rowModel.Keys[keyIndex];
                KeyViewState keyState = CreateKeyViewState();

                ApplyKeyLayout(keyState, keyModel, rowIndex, keyIndex, rowModel.Keys.Count, keyHeightPx, horizontalGapPx, layoutModel.LayoutType);
                ApplyKeyAppearance(keyState.Button, keyModel);
                RegisterButton(keyModel.Code, keyState.Button, rowIndex);

                rowView.AddView(keyState.Container);
                rowState.Keys.Add(keyState);
            }

            AddView(rowView);
            _rowStates.Add(rowState);
        }
    }

    private void UpdateExistingLayout(KeyboardLayoutModel layoutModel, int keyHeightPx, int verticalGapPx, int horizontalGapPx)
    {
        float maxRowWeightSum = ComputeMaxRowWeightSum(layoutModel);

        for (int rowIndex = 0; rowIndex < layoutModel.Rows.Count; rowIndex++)
        {
            KeyboardRowModel rowModel = layoutModel.Rows[rowIndex];
            RowViewState rowState = _rowStates[rowIndex];

            ApplyRowLayout(rowState.RowView, rowModel, rowIndex, verticalGapPx, maxRowWeightSum);

            for (int keyIndex = 0; keyIndex < rowModel.Keys.Count; keyIndex++)
            {
                KeyboardKeyModel keyModel = rowModel.Keys[keyIndex];
                KeyViewState keyState = rowState.Keys[keyIndex];

                ApplyKeyLayout(keyState, keyModel, rowIndex, keyIndex, rowModel.Keys.Count, keyHeightPx, horizontalGapPx, layoutModel.LayoutType);
                ApplyKeyAppearance(keyState.Button, keyModel);
                RegisterButton(keyModel.Code, keyState.Button, rowIndex);
            }
        }
    }

    private KeyViewState CreateKeyViewState()
    {
        FrameLayout container = new(Context);
        Button button = BuildKeyButton();
        container.AddView(button);
        return new KeyViewState(container, button);
    }

    private Button BuildKeyButton()
    {
        var button = new Button(Context)
        {
            Text = string.Empty
        };

        button.SetAllCaps(false);
        button.SetPadding(0, 0, 0, 0);
        button.SetMinHeight(0);
        button.SetMinWidth(0);
        button.Gravity = GravityFlags.Center;
        button.Clickable = false;
        button.Focusable = false;
        button.FocusableInTouchMode = false;
        return button;
    }

    private void ApplyRowLayout(LinearLayout rowView, KeyboardRowModel rowModel, int rowIndex, int verticalGapPx, float maxRowWeightSum)
    {
        rowView.WeightSum = maxRowWeightSum;
        rowView.SetGravity(GravityFlags.CenterHorizontal);

        if (rowView.LayoutParameters is not LayoutParams rowLayoutParams)
        {
            rowLayoutParams = new LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
            rowView.LayoutParameters = rowLayoutParams;
        }

        rowLayoutParams.Width = ViewGroup.LayoutParams.MatchParent;
        rowLayoutParams.Height = ViewGroup.LayoutParams.WrapContent;
        rowLayoutParams.TopMargin = rowIndex == 0 ? 0 : verticalGapPx;
    }

    private void ApplyKeyLayout(KeyViewState keyState, KeyboardKeyModel keyModel, int rowIndex, int keyIndex, int keyCount, int keyHeightPx, int horizontalGapPx, IKeyboardType layoutType)
    {
        float keyWidth = Math.Max(0.01f, keyModel.WidthPercent);

        if (keyState.Container.LayoutParameters is not LayoutParams containerLayoutParams)
        {
            containerLayoutParams = new LayoutParams(0, keyHeightPx, keyWidth);
            keyState.Container.LayoutParameters = containerLayoutParams;
        }

        containerLayoutParams.Width = 0;
        containerLayoutParams.Height = keyHeightPx;
        containerLayoutParams.Weight = keyWidth;

        if (keyState.Button.LayoutParameters is not FrameLayout.LayoutParams buttonLayoutParams)
        {
            buttonLayoutParams = new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent);
            keyState.Button.LayoutParameters = buttonLayoutParams;
        }

        buttonLayoutParams.Width = ViewGroup.LayoutParams.MatchParent;
        buttonLayoutParams.Height = ViewGroup.LayoutParams.MatchParent;

        bool useNoGap = ShouldUseSogouSecondRowNoGap(layoutType, rowIndex);
        int effectiveGapPx = useNoGap ? 0 : horizontalGapPx;

        if (effectiveGapPx > 0)
        {
            int halfGap = effectiveGapPx / 2;
            buttonLayoutParams.LeftMargin = keyIndex == 0 ? 0 : halfGap;
            buttonLayoutParams.RightMargin = keyIndex == keyCount - 1 ? 0 : halfGap;
        }
        else
        {
            buttonLayoutParams.LeftMargin = 0;
            buttonLayoutParams.RightMargin = 0;
        }
    }

    private void ApplyKeyAppearance(Button button, KeyboardKeyModel keyModel)
    {
        string label = ResolveKeyLabel(keyModel);
        if (!string.Equals(button.Text, label, StringComparison.Ordinal))
        {
            button.Text = label;
        }

        bool isFunctionKey = IsFunctionKey(keyModel.Code);
        int background = isFunctionKey ? Resource.Drawable.key_function_background : Resource.Drawable.key_background;
        int textColor = isFunctionKey ? Resource.Color.key_text_secondary : Resource.Color.key_text;

        button.SetBackgroundResource(background);
        button.SetTextColor(new Color(ContextCompat.GetColor(Context, textColor)));

        bool isMultiLineKey = !isFunctionKey && label.Contains('\n');
        button.SetTextSize(ComplexUnitType.Sp, isFunctionKey ? 15f : (isMultiLineKey ? 14f : 18f));
    }

    private void RegisterButton(int keyCode, Button button, int rowIndex)
    {
        if (!_buttonsByCode.TryGetValue(keyCode, out List<Button>? buttons))
        {
            buttons = new List<Button>();
            _buttonsByCode[keyCode] = buttons;
        }

        buttons.Add(button);
        _buttonCodeMap[button] = keyCode;
        _buttonRowMap[button] = rowIndex;
    }

    public override bool OnInterceptTouchEvent(MotionEvent? ev)
    {
        return true;
    }

    public override bool OnTouchEvent(MotionEvent? e)
    {
        if (e == null)
        {
            return false;
        }

        switch (e.ActionMasked)
        {
            case MotionEventActions.Down:
            case MotionEventActions.PointerDown:
                HandlePointerDown(e, e.ActionIndex);
                break;
            case MotionEventActions.Move:
                for (int i = 0; i < e.PointerCount; i++)
                {
                    HandlePointerMove(e, i);
                }
                break;
            case MotionEventActions.Up:
            case MotionEventActions.PointerUp:
                HandlePointerUp(e, e.ActionIndex);
                break;
            case MotionEventActions.Cancel:
                HandleCancel();
                break;
        }

        return true;
    }

    private void HandlePointerDown(MotionEvent motionEvent, int pointerIndex)
    {
        RefreshTapFallbackSettings();

        int pointerId = motionEvent.GetPointerId(pointerIndex);
        GetRawCoordinates(motionEvent, pointerIndex, out float rawX, out float rawY);

        Button? targetButton = FindButtonAtRawPosition(rawX, rawY);
        if (targetButton == null || !_buttonCodeMap.TryGetValue(targetButton, out int keyCode))
        {
            _pointerStates.Remove(pointerId);
            return;
        }

        var state = new PointerState(pointerId, targetButton, keyCode, rawX, rawY, motionEvent.EventTime)
        {
            CurrentButton = targetButton,
            CurrentKeyCode = keyCode
        };

        _pointerStates[pointerId] = state;
        IncrementPressed(targetButton);
        LogTouch(BuildDownLog(pointerId, keyCode, rawX, rawY, motionEvent.GetX(pointerIndex), motionEvent.GetY(pointerIndex), targetButton));
        Press?.Invoke(this, new KeyboardKeyEventArgs(keyCode));
    }

    private void HandlePointerMove(MotionEvent motionEvent, int pointerIndex)
    {
        int pointerId = motionEvent.GetPointerId(pointerIndex);
        if (!_pointerStates.TryGetValue(pointerId, out PointerState? state))
        {
            return;
        }

        if (LockToDownKey)
        {
            return;
        }

        GetRawCoordinates(motionEvent, pointerIndex, out float rawX, out float rawY);
        Button? currentButton = state.CurrentButton;
        if (currentButton != null && IsRawPointInsideView(currentButton, rawX, rawY, _keyReleaseSlopPx))
        {
            return;
        }

        Button? targetButton = FindButtonAtRawPosition(rawX, rawY);
        if (ReferenceEquals(targetButton, state.CurrentButton))
        {
            return;
        }

        int? sourceRow = GetButtonRow(state.SourceButton);
        int? targetRow = GetButtonRow(targetButton);
        bool lockToRow = targetButton != null
            && state.SourceButton != null
            && sourceRow.HasValue
            && targetRow.HasValue
            && sourceRow.Value != targetRow.Value
            && ShouldLockToSourceRow(state, rawY, motionEvent.EventTime);

        if (sourceRow.HasValue && targetRow.HasValue && sourceRow.Value != targetRow.Value)
        {
            LogTouch($"MOVE id={pointerId} raw=({rawX:F1},{rawY:F1}) sourceRow={sourceRow} targetRow={targetRow} lock={lockToRow}");
        }

        if (lockToRow)
        {
            return;
        }

        UpdatePressedButton(state, targetButton);
    }

    private void HandlePointerUp(MotionEvent motionEvent, int pointerIndex)
    {
        int pointerId = motionEvent.GetPointerId(pointerIndex);
        if (!_pointerStates.TryGetValue(pointerId, out PointerState? state))
        {
            return;
        }

        GetRawCoordinates(motionEvent, pointerIndex, out float rawX, out float rawY);
        Button? targetButton = FindButtonAtRawPosition(rawX, rawY);
        int? resolvedKeyCode = ResolveReleasedKeyCode(state, rawX, rawY);
        bool shouldFallback = ShouldFallbackToSourceKey(state, rawX, rawY, motionEvent.EventTime);

        if (LockToDownKey)
        {
            resolvedKeyCode = state.SourceKeyCode;
            shouldFallback = true;
            targetButton = state.SourceButton;
        }
        else if (!shouldFallback
            && targetButton != null
            && resolvedKeyCode.HasValue
            && resolvedKeyCode.Value != state.SourceKeyCode
            && state.SourceButton != null
            && _buttonRowMap.TryGetValue(state.SourceButton, out int sourceRow)
            && _buttonRowMap.TryGetValue(targetButton, out int targetRow)
            && sourceRow != targetRow
            && ShouldLockToSourceRow(state, rawY, motionEvent.EventTime))
        {
            shouldFallback = true;
        }

        if (shouldFallback && (!resolvedKeyCode.HasValue || resolvedKeyCode.Value != state.SourceKeyCode))
        {
            resolvedKeyCode = state.SourceKeyCode;
        }

        if (resolvedKeyCode.HasValue)
        {
            Key?.Invoke(this, new KeyboardKeyEventArgs(resolvedKeyCode.Value));
        }

        LogTouch(BuildUpLog(state, rawX, rawY, motionEvent.GetX(pointerIndex), motionEvent.GetY(pointerIndex), targetButton, resolvedKeyCode, shouldFallback));
        Release?.Invoke(this, new KeyboardKeyEventArgs(state.SourceKeyCode));
        RemovePointerState(pointerId);
    }

    private void HandleCancel()
    {
        foreach (int pointerId in _pointerStates.Keys.ToList())
        {
            RemovePointerState(pointerId, fireRelease: true);
        }
    }

    private void RemovePointerState(int pointerId, bool fireRelease = false)
    {
        if (!_pointerStates.TryGetValue(pointerId, out PointerState? state))
        {
            return;
        }

        if (fireRelease)
        {
            Release?.Invoke(this, new KeyboardKeyEventArgs(state.SourceKeyCode));
        }

        if (state.CurrentButton != null)
        {
            DecrementPressed(state.CurrentButton);
        }

        _pointerStates.Remove(pointerId);
    }

    private void UpdatePressedButton(PointerState state, Button? targetButton)
    {
        Button? previous = state.CurrentButton;
        if (previous != null)
        {
            DecrementPressed(previous);
        }

        state.CurrentButton = targetButton;
        state.CurrentKeyCode = null;

        if (targetButton != null && _buttonCodeMap.TryGetValue(targetButton, out int keyCode))
        {
            state.CurrentKeyCode = keyCode;
            IncrementPressed(targetButton);
        }
    }

    private void IncrementPressed(Button button)
    {
        if (_pressedButtonCounts.TryGetValue(button, out int count))
        {
            _pressedButtonCounts[button] = count + 1;
        }
        else
        {
            _pressedButtonCounts[button] = 1;
            button.Pressed = true;
        }
    }

    private void DecrementPressed(Button button)
    {
        if (!_pressedButtonCounts.TryGetValue(button, out int count))
        {
            return;
        }

        count--;
        if (count <= 0)
        {
            _pressedButtonCounts.Remove(button);
            button.Pressed = false;
            return;
        }

        _pressedButtonCounts[button] = count;
    }

    private void GetRawCoordinates(MotionEvent motionEvent, int pointerIndex, out float rawX, out float rawY)
    {
        float baseRawX = motionEvent.RawX;
        float baseRawY = motionEvent.RawY;
        float baseLocalX = motionEvent.GetX();
        float baseLocalY = motionEvent.GetY();
        rawX = baseRawX + (motionEvent.GetX(pointerIndex) - baseLocalX);
        rawY = baseRawY + (motionEvent.GetY(pointerIndex) - baseLocalY) + _touchYOffsetPx;
    }

    private int? GetButtonRow(Button? button)
    {
        if (button == null)
        {
            return null;
        }

        return _buttonRowMap.TryGetValue(button, out int row) ? row : null;
    }

    private string BuildDownLog(int pointerId, int keyCode, float rawX, float rawY, float localX, float localY, Button targetButton)
    {
        int? row = GetButtonRow(targetButton);
        string bounds = FormatBounds(targetButton);
        return $"DOWN id={pointerId} raw=({rawX:F1},{rawY:F1}) local=({localX:F1},{localY:F1}) code={keyCode} row={row} bounds={bounds}";
    }

    private string BuildUpLog(PointerState state, float rawX, float rawY, float localX, float localY, Button? targetButton, int? resolvedKeyCode, bool shouldFallback)
    {
        int? sourceRow = GetButtonRow(state.SourceButton);
        int? targetRow = GetButtonRow(targetButton);
        string sourceBounds = FormatBounds(state.SourceButton);
        string targetBounds = FormatBounds(targetButton);
        return $"UP id={state.PointerId} raw=({rawX:F1},{rawY:F1}) local=({localX:F1},{localY:F1}) sourceCode={state.SourceKeyCode} sourceRow={sourceRow} targetCode={resolvedKeyCode} targetRow={targetRow} fallback={shouldFallback} sourceBounds={sourceBounds} targetBounds={targetBounds}";
    }

    private string FormatBounds(View? view)
    {
        if (view == null)
        {
            return "null";
        }

        view.GetLocationOnScreen(_tempLocation);
        int left = _tempLocation[0];
        int top = _tempLocation[1];
        int right = left + view.Width;
        int bottom = top + view.Height;
        return $"{left},{top},{right},{bottom}";
    }

    [Conditional("DEBUG")]
    private void LogTouch(string message)
    {
        Log.Debug(TouchLogTag, message);
    }

    private bool ShouldFallbackToSourceKey(PointerState state, float rawX, float rawY, long eventTime)
    {
        if (!_tapFallbackEnabled)
        {
            return false;
        }

        long duration = eventTime - state.DownEventTime;
        if (duration > _tapFallbackMaxDurationMs)
        {
            return false;
        }

        float dx = rawX - state.DownRawX;
        float dy = rawY - state.DownRawY;
        float distanceSquared = (dx * dx) + (dy * dy);
        float moveLimit = _tapFallbackMoveSlopPx;
        return distanceSquared <= (moveLimit * moveLimit);
    }

    private bool ShouldLockToSourceRow(PointerState state, float rawY, long eventTime)
    {
        if (!_tapFallbackEnabled)
        {
            return false;
        }

        long duration = eventTime - state.DownEventTime;
        if (duration > _tapFallbackMaxDurationMs)
        {
            return false;
        }

        int rowSwitchSlopPx = Math.Max(_keyReleaseSlopPx, (int)(_lastKeyHeightPx * RowSwitchSlopFactor));
        float dy = Math.Abs(rawY - state.DownRawY);
        return dy <= rowSwitchSlopPx;
    }

    private void RefreshTapFallbackSettings(bool force = false)
    {
        long now = Environment.TickCount64;
        if (!force && (now - _lastTapFallbackSettingsRefreshAt) < TapFallbackSettingsRefreshIntervalMs)
        {
            return;
        }

        _lastTapFallbackSettingsRefreshAt = now;
        int durationMs = SettingsActivity.GetTapFallbackMaxDurationMs(Context);
        int moveSlopDp = SettingsActivity.GetTapFallbackMoveSlopDp(Context);

        _tapFallbackMaxDurationMs = durationMs;
        _tapFallbackMoveSlopPx = DpToPx(moveSlopDp);
        _tapFallbackEnabled = durationMs > 0 && _tapFallbackMoveSlopPx > 0;
    }

    private int? ResolveReleasedKeyCode(PointerState state, float rawX, float rawY)
    {
        if (state.SourceButton != null && IsRawPointInsideView(state.SourceButton, rawX, rawY, _keyReleaseSlopPx))
        {
            return state.SourceKeyCode;
        }

        return FindKeyCodeAtRawPosition(rawX, rawY);
    }

    private void EnsureBoundsCache()
    {
        if (_boundsValid) return;
        _boundsValid = true;
        _buttonBoundsCache.Clear();

        foreach ((Button button, int _) in _buttonCodeMap)
        {
            if (!button.IsShown || button.Width <= 0 || button.Height <= 0)
                continue;

            button.GetLocationOnScreen(_tempLocation);
            _buttonBoundsCache[button] = new Rect(
                _tempLocation[0], _tempLocation[1],
                _tempLocation[0] + button.Width,
                _tempLocation[1] + button.Height);
        }
    }

    private Button? FindButtonAtRawPosition(float rawX, float rawY)
    {
        EnsureBoundsCache();

        Button? exactMatch = null;
        double exactMatchDistance = double.MaxValue;

        Button? nearMatch = null;
        double nearMatchDistance = double.MaxValue;

        foreach ((Button button, Rect bounds) in _buttonBoundsCache)
        {
            float centerX = bounds.Left + (bounds.Width() / 2f);
            float centerY = bounds.Top + (bounds.Height() / 2f);
            double distance = ((rawX - centerX) * (rawX - centerX)) + ((rawY - centerY) * (rawY - centerY));

            bool isInsideExact = rawX >= bounds.Left && rawX <= bounds.Right && rawY >= bounds.Top && rawY <= bounds.Bottom;
            if (isInsideExact)
            {
                if (distance < exactMatchDistance)
                {
                    exactMatchDistance = distance;
                    exactMatch = button;
                }

                continue;
            }

            int nearLeft = bounds.Left - _crossKeyReleaseSlopPx;
            int nearRight = bounds.Right + _crossKeyReleaseSlopPx;

            bool isInsideNear = rawX >= nearLeft && rawX <= nearRight && rawY >= bounds.Top && rawY <= bounds.Bottom;
            if (isInsideNear && distance < nearMatchDistance)
            {
                nearMatchDistance = distance;
                nearMatch = button;
            }
        }

        return exactMatch ?? nearMatch;
    }

    private int? FindKeyCodeAtRawPosition(float rawX, float rawY)
    {
        Button? button = FindButtonAtRawPosition(rawX, rawY);
        if (button == null)
        {
            return null;
        }

        return _buttonCodeMap.TryGetValue(button, out int keyCode) ? keyCode : null;
    }

    private int DpToPx(float dp)
    {
        return (int)TypedValue.ApplyDimension(ComplexUnitType.Dip, dp, Resources?.DisplayMetrics);
    }

    private bool IsRawPointInsideView(View view, float rawX, float rawY, int slopPx)
    {
        EnsureBoundsCache();
        if (view is Button btn && _buttonBoundsCache.TryGetValue(btn, out var bounds))
        {
            return rawX >= bounds.Left - slopPx
                   && rawX <= bounds.Right + slopPx
                   && rawY >= bounds.Top - slopPx
                   && rawY <= bounds.Bottom + slopPx;
        }

        view.GetLocationOnScreen(_tempLocation);
        int left = _tempLocation[0];
        int top = _tempLocation[1];
        int right = left + view.Width;
        int bottom = top + view.Height;

        return rawX >= left - slopPx
               && rawX <= right + slopPx
               && rawY >= top - slopPx
               && rawY <= bottom + slopPx;
    }

    private static string ResolveKeyLabel(KeyboardKeyModel keyModel)
    {
        if (!string.IsNullOrWhiteSpace(keyModel.Label))
        {
            return keyModel.Label;
        }

        return keyModel.Code switch
        {
            -1 => "?",
            -2 => "123",
            -3 => "?",
            -4 => "?/?",
            -5 => "?",
            -6 => "?",
            -7 => "ABC",
            32 => "??",
            _ when keyModel.Code > 0 => char.ConvertFromUtf32(keyModel.Code),
            _ => string.Empty
        };
    }

    private static bool IsFunctionKey(int keyCode)
    {
        return keyCode < 0 || keyCode == 32;
    }

    private sealed class PointerState
    {
        public PointerState(int pointerId, Button sourceButton, int sourceKeyCode, float rawX, float rawY, long eventTime)
        {
            PointerId = pointerId;
            SourceButton = sourceButton;
            SourceKeyCode = sourceKeyCode;
            DownRawX = rawX;
            DownRawY = rawY;
            DownEventTime = eventTime;
        }

        public int PointerId { get; }

        public Button? SourceButton { get; }

        public int SourceKeyCode { get; }

        public float DownRawX { get; }

        public float DownRawY { get; }

        public long DownEventTime { get; }

        public Button? CurrentButton { get; set; }

        public int? CurrentKeyCode { get; set; }
    }

    private sealed class RowViewState
    {
        public RowViewState(LinearLayout rowView)
        {
            RowView = rowView;
            Keys = new List<KeyViewState>();
        }

        public LinearLayout RowView { get; }

        public List<KeyViewState> Keys { get; }
    }

    private sealed class KeyViewState
    {
        public KeyViewState(FrameLayout container, Button button)
        {
            Container = container;
            Button = button;
        }

        public FrameLayout Container { get; }

        public Button Button { get; }
    }
}
