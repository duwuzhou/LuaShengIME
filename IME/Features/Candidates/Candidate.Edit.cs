using Android.Graphics;
using Android.Text;
using Android.Text.Style;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidX.Core.Content;
using Java.Lang;
using System;
using System.Text;
using Math = System.Math;
using StringBuilder = System.Text.StringBuilder;

namespace IME.Features.Candidates;

public partial class Candidate
{
    private const int KeyCodeShift = -1;
    private const int KeyCodeBackspace = -5;
    private const int CursorBlinkIntervalMs = 480;
    private const string CursorGlyph = "|";

    private void OnInputPreviewTouch(object? sender, TouchEventArgs e)
    {
        var motionEvent = e.Event;
        if (motionEvent != null)
        {
            _lastInputPreviewTouchX = motionEvent.GetX();
        }

        e.Handled = false;
    }

    private void OnInputPreviewClick(object? sender, EventArgs e)
    {
        if (_inputEngine == null || string.IsNullOrWhiteSpace(_inputPreviewRawText))
        {
            return;
        }

        if (_keyboardHandler?.IsT9Mode == true && !(_keyboardHandler?.GetAsciiMode() ?? false))
        {
            return;
        }

        if (!_isPinyinEditing)
        {
            EnterPinyinEditMode();
            if (!_isPinyinEditing)
            {
                return;
            }
        }

        MoveCursorByTouchX(_lastInputPreviewTouchX);
        RefreshCursorBlink();
        RenderInputPreviewText();
    }

    private void OnPinyinEditBackClick(object? sender, EventArgs e)
    {
        ExitPinyinEditMode();
    }

    internal bool TryHandleInlineEditKey(int keyCode)
    {
        if (!_isPinyinEditing)
        {
            return false;
        }

        if (_keyboardHandler?.IsT9Mode == true && !(_keyboardHandler?.GetAsciiMode() ?? false))
        {
            ExitPinyinEditMode();
            return false;
        }

        switch (keyCode)
        {
            case KeyCodeBackspace:
                if (_editCursorIndex <= 0 || _editablePinyin.Length == 0)
                {
                    RefreshCursorBlink();
                    return true;
                }

                _editablePinyin = _editablePinyin.Remove(_editCursorIndex - 1, 1);
                _editCursorIndex--;
                ReplayEditablePinyinToEngine();
                RefreshCursorBlink();
                return true;

            case KeyCodeShift:
            case 39:
                InsertEditableChar('\'');
                RefreshCursorBlink();
                return true;

            default:
                if (IsAsciiLetterKey(keyCode))
                {
                    InsertEditableChar(char.ToLowerInvariant((char)keyCode));
                    RefreshCursorBlink();
                    return true;
                }

                ExitPinyinEditMode();
                return false;
        }
    }

    internal void ExitPinyinEditMode()
    {
        if (!_isPinyinEditing)
        {
            return;
        }

        _isPinyinEditing = false;
        _editablePinyin = string.Empty;
        _editCursorIndex = 0;
        _lastRenderedPinyin = string.Empty;
        _lastRenderedCursor = -1;
        StopCursorBlink();
        RenderInputPreviewText();
    }

    private void EnterPinyinEditMode()
    {
        string normalized = NormalizeEditablePinyin(_inputPreviewRawText);
        if (normalized.Length == 0)
        {
            return;
        }

        _isPinyinEditing = true;
        _editablePinyin = normalized;
        _editCursorIndex = _editablePinyin.Length;
        StartCursorBlink();
        RenderInputPreviewText();
    }

    private void MoveCursorByTouchX(float touchX)
    {
        if (!_isPinyinEditing)
        {
            return;
        }

        if (_editablePinyin.Length == 0 || _inputPreviewText == null)
        {
            _editCursorIndex = _editablePinyin.Length;
            return;
        }

        var paint = _inputPreviewText.Paint;
        var contentX = touchX - _inputPreviewText.TotalPaddingLeft + _inputPreviewText.ScrollX;
        if (contentX <= 0)
        {
            _editCursorIndex = 0;
            return;
        }

        var totalWidth = paint.MeasureText(_editablePinyin);
        if (contentX >= totalWidth)
        {
            _editCursorIndex = _editablePinyin.Length;
            return;
        }

        var offset = 0f;
        for (var i = 0; i < _editablePinyin.Length; i++)
        {
            var charWidth = paint.MeasureText(_editablePinyin[i].ToString());
            var boundary = offset + charWidth / 2f;
            if (contentX <= boundary)
            {
                _editCursorIndex = i;
                return;
            }

            offset += charWidth;
        }

        _editCursorIndex = _editablePinyin.Length;
    }

    private void InsertEditableChar(char ch)
    {
        if (!_isPinyinEditing)
        {
            return;
        }

        _editablePinyin = _editablePinyin.Insert(_editCursorIndex, ch.ToString());
        _editCursorIndex++;
        ReplayEditablePinyinToEngine();
    }

    private void ReplayEditablePinyinToEngine()
    {
        if (_inputEngine == null)
        {
            return;
        }

        _editablePinyin = NormalizeEditablePinyin(_editablePinyin);
        _editCursorIndex = Math.Clamp(_editCursorIndex, 0, _editablePinyin.Length);

        _inputEngine.Reset();
        _inputPreviewRawText = _editablePinyin;

        if (_editablePinyin.Length == 0)
        {
            ExitPinyinEditMode();
            _keyboardHandler?.ClearCandidates();
            return;
        }

        for (var i = 0; i < _editablePinyin.Length; i++)
        {
            _inputEngine.ProcessKey(_editablePinyin[i]);
        }

        _keyboardHandler?.UpdateChineseCandidates();
        RenderInputPreviewText();
    }

    private void RenderInputPreviewText()
    {
        if (_inputPreviewText == null)
        {
            return;
        }

        if (!_isPinyinEditing)
        {
            _lastRenderedPinyin = string.Empty;
            _lastRenderedCursor = -1;
            _inputPreviewText.Text = _inputPreviewRawText;
            UpdatePinyinEditVisualState();
            return;
        }

        var cursor = Math.Clamp(_editCursorIndex, 0, _editablePinyin.Length);

        if (_editablePinyin == _lastRenderedPinyin
            && cursor == _lastRenderedCursor
            && _isEditCursorVisible == _lastRenderedCursorVisible)
        {
            return;
        }

        _lastRenderedPinyin = _editablePinyin;
        _lastRenderedCursor = cursor;
        _lastRenderedCursorVisible = _isEditCursorVisible;

        var preview = _editablePinyin.Insert(cursor, CursorGlyph);
        var spannable = new SpannableString(preview);
        var accent = new Color(ContextCompat.GetColor(Context, Resource.Color.colorPrimary));
        var cursorArgb = _isEditCursorVisible
            ? accent.ToArgb()
            : Color.Argb(0, accent.R, accent.G, accent.B);

        spannable.SetSpan(new ForegroundColorSpan(new Color(cursorArgb)), cursor, cursor + 1, SpanTypes.ExclusiveExclusive);
        spannable.SetSpan(new StyleSpan(TypefaceStyle.Bold), cursor, cursor + 1, SpanTypes.ExclusiveExclusive);
        spannable.SetSpan(new RelativeSizeSpan(1.2f), cursor, cursor + 1, SpanTypes.ExclusiveExclusive);
        _inputPreviewText.TextFormatted = spannable;

        UpdatePinyinEditVisualState();
    }

    private void UpdatePinyinEditVisualState()
    {
        if (_inputPreviewContainer == null || _inputPreviewText == null || _candidatePreviewText == null || _btnPinyinEditBack == null)
        {
            return;
        }

        if (_isPinyinEditing)
        {
            _btnPinyinEditBack.Visibility = ViewStates.Visible;
            _inputPreviewContainer.SetBackgroundResource(Resource.Drawable.bg_field);

            if (!_isEditVisualApplied)
            {
                _inputPreviewContainer.Animate().TranslationY(-Dp(6)).SetDuration(120).Start();
                _inputPreviewContainer.Animate().TranslationZ(Dp(6)).SetDuration(120).Start();
                _inputPreviewContainer.Elevation = Dp(8);
                _isEditVisualApplied = true;
            }

            _inputPreviewText.SetTextColor(new Color(ContextCompat.GetColor(Context, Resource.Color.text_primary)));
            _inputPreviewText.SetTypeface(Typeface.Default, TypefaceStyle.Bold);
            _candidatePreviewText.Visibility = ViewStates.Gone;
            return;
        }

        _btnPinyinEditBack.Visibility = ViewStates.Gone;
        _inputPreviewContainer.SetBackgroundColor(new Color(ContextCompat.GetColor(Context, Resource.Color.surface_alt)));

        if (_isEditVisualApplied)
        {
            _inputPreviewContainer.Animate().TranslationY(0).SetDuration(120).Start();
            _inputPreviewContainer.Animate().TranslationZ(0).SetDuration(120).Start();
            _inputPreviewContainer.Elevation = 0;
            _isEditVisualApplied = false;
        }

        _inputPreviewText.SetTextColor(new Color(ContextCompat.GetColor(Context, Resource.Color.input_pinyin)));
        _inputPreviewText.SetTypeface(Typeface.Default, TypefaceStyle.Italic);
        ApplyCandidatePreviewVisibility();
    }

    private void StartCursorBlink()
    {
        _isEditCursorVisible = true;
        _cursorBlinkHandler ??= new Android.OS.Handler(Android.OS.Looper.MainLooper);
        _cursorBlinkRunnable ??= new Runnable(() =>
        {
            if (!_isPinyinEditing)
            {
                return;
            }

            _isEditCursorVisible = !_isEditCursorVisible;
            RenderInputPreviewText();
            _cursorBlinkHandler?.PostDelayed(_cursorBlinkRunnable, CursorBlinkIntervalMs);
        });

        _cursorBlinkHandler.RemoveCallbacks(_cursorBlinkRunnable);
        _cursorBlinkHandler.PostDelayed(_cursorBlinkRunnable, CursorBlinkIntervalMs);
    }

    private void StopCursorBlink()
    {
        if (_cursorBlinkHandler != null && _cursorBlinkRunnable != null)
        {
            _cursorBlinkHandler.RemoveCallbacks(_cursorBlinkRunnable);
        }

        _isEditCursorVisible = true;
    }

    private void RefreshCursorBlink()
    {
        _isEditCursorVisible = true;

        if (_cursorBlinkHandler == null || _cursorBlinkRunnable == null)
        {
            return;
        }

        _cursorBlinkHandler.RemoveCallbacks(_cursorBlinkRunnable);
        _cursorBlinkHandler.PostDelayed(_cursorBlinkRunnable, CursorBlinkIntervalMs);
    }

    private int Dp(int value)
    {
        return (int)TypedValue.ApplyDimension(ComplexUnitType.Dip, value, Context.Resources.DisplayMetrics);
    }

    private static bool IsAsciiLetterKey(int keyCode)
    {
        return (keyCode >= 'a' && keyCode <= 'z') || (keyCode >= 'A' && keyCode <= 'Z');
    }

    private static string NormalizeEditablePinyin(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        foreach (var raw in text)
        {
            var ch = char.ToLowerInvariant(raw switch
            {
                '’' => '\'',
                'ü' => 'v',
                _ => raw
            });

            if ((ch >= 'a' && ch <= 'z') || ch == '\'')
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }
}
