using Android.OS;
using Android.Util;
using Android.Views.InputMethods;
using IME.Features.Input.Abstractions;
using IME.Features.Keyboard;
using IME.Shared.Utils.Enums;
using Java.Lang;

namespace IME.Features.Input.Handlers;

public class SpecialKeyHandler
{
    private readonly IInputEngineHost _engineHost;
    private readonly IInputConnectionAdapter _connection;
    private readonly KeyboardHandler _keyboardHandler;
    private readonly Android.Content.Context _context;
    private Handler? _handler;
    private Java.Lang.IRunnable? _backspaceRunnable;

    private bool _isShiftActive;
    private IKeyboardType _keyboardType = IKeyboardType.LowerCase;

    public bool IsShiftActive => _isShiftActive;
    public IKeyboardType KeyboardType => _keyboardType;

    public SpecialKeyHandler(IInputEngineHost engineHost, IInputConnectionAdapter connection, KeyboardHandler keyboardHandler, Android.Content.Context context)
    {
        _engineHost = engineHost;
        _connection = connection;
        _keyboardHandler = keyboardHandler;
        _context = context;
        _handler = new Handler(Looper.MainLooper);
    }

    public void HandleBackspace()
    {
        if (_connection == null || !_connection.IsValid) return;
        if (_keyboardHandler == null) return;

        bool isAsciiMode = _keyboardHandler.GetAsciiMode();
        var engine = _engineHost.CurrentEngine;

        if (!isAsciiMode && engine != null)
        {
            if (engine.ProcessKey(8))
            {
                _engineHost.UpdateCandidates();
                return;
            }
        }

        _connection.DeleteSurroundingText(1, 0);
    }

    public void HandleEnter()
    {
        if (_connection == null || !_connection.IsValid) return;

        int enterAction = IME.Features.Settings.SettingsActivity.GetEnterAction(_context);

        if (enterAction == IME.Features.Settings.SettingsActivity.EnterActionSend)
        {
            if (_connection.PerformEditorAction(ImeAction.Send))
            {
                return;
            }
        }

        _connection.CommitText("\n", 1);
        _engineHost.NotifyCommittedText("\n", false);
    }

    public void HandleSpace()
    {
        if (_connection == null || !_connection.IsValid) return;
        if (_keyboardHandler == null) return;

        bool isAsciiMode = _keyboardHandler.GetAsciiMode();
        var engine = _engineHost.CurrentEngine;

        if (!isAsciiMode && engine != null)
        {
            string composingText = engine.GetComposingText();
            if (!string.IsNullOrEmpty(composingText))
            {
                string committed = engine.SelectCandidate(0);
                if (!string.IsNullOrEmpty(committed))
                {
                    _connection.CommitText(committed, 1);
                    _engineHost.RecordUserLexicon(committed, composingText);
                    _engineHost.NotifyCommittedText(committed, false);
                }

                _engineHost.ClearCandidates();
                return;
            }
        }

        _connection.CommitText(" ", 1);
        _engineHost.NotifyCommittedText(" ", false);
    }

    public void HandleShift()
    {
        if (_keyboardHandler == null) return;

        if (TryHandleManualSegmentation())
        {
            return;
        }

        bool isAsciiMode = _keyboardHandler.GetAsciiMode();

        if (isAsciiMode)
        {
            _isShiftActive = !_isShiftActive;
            if (_isShiftActive)
            {
                _keyboardType = IKeyboardType.UpperCase;
                Log.Info("SpecialKeyHandler", "Shift enabled: uppercase mode");
            }
            else
            {
                _keyboardType = IKeyboardType.LowerCase;
                Log.Info("SpecialKeyHandler", "Shift disabled: lowercase mode");
            }
            _keyboardHandler.SwitchKeyboard(_keyboardType);
        }
        else
        {
            if (_keyboardType == IKeyboardType.LowerCase)
            {
                _keyboardType = IKeyboardType.UpperCase;
                Log.Info("SpecialKeyHandler", "Chinese mode: switched to uppercase layout");
            }
            else
            {
                _keyboardType = IKeyboardType.LowerCase;
                Log.Info("SpecialKeyHandler", "Chinese mode: switched to lowercase layout");
            }
            _keyboardHandler.SwitchKeyboard(_keyboardType);
        }
    }

    private bool TryHandleManualSegmentation()
    {
        if (_keyboardHandler.GetAsciiMode())
        {
            return false;
        }

        var engine = _engineHost.CurrentEngine;
        if (engine == null)
        {
            return false;
        }

        var composingText = engine.GetComposingText();
        if (string.IsNullOrEmpty(composingText))
        {
            return false;
        }

        // T9 模式用空格作为分隔符，QWERTY 模式用单引号
        int segmentKey = _keyboardHandler.IsT9Mode ? ' ' : '\'';

        if (!engine.ProcessKey(segmentKey))
        {
            return false;
        }

        Log.Info("SpecialKeyHandler", "Manual segmentation inserted by Shift.");
        _engineHost.UpdateCandidates();
        return true;
    }

    public void StartBackspaceRepeating()
    {
        if (_connection == null || !_connection.IsValid) return;
        if (_keyboardHandler == null) return;

        // In Chinese mode, long-press backspace clears the whole composing buffer in one action.
        if (TryClearChineseComposingOnLongPress())
        {
            return;
        }

        bool isAsciiMode = _keyboardHandler.GetAsciiMode();
        var engine = _engineHost.CurrentEngine;

        if (!isAsciiMode && engine != null)
        {
            if (engine.ProcessKey(8))
            {
                _engineHost.UpdateCandidates();
                ScheduleBackspaceRepeat();
                return;
            }
        }

        _connection.DeleteSurroundingText(1, 0);

        Log.Info("SpecialKeyHandler", "Start repeating backspace");
        ScheduleBackspaceRepeat();
    }

    public void StopBackspaceRepeating()
    {
        if (_backspaceRunnable != null)
            _handler?.RemoveCallbacks(_backspaceRunnable);
        Log.Info("SpecialKeyHandler", "Stop repeating backspace");
    }

    private void ScheduleBackspaceRepeat()
    {
        _backspaceRunnable ??= new Runnable(() => StartBackspaceRepeating());
        _handler?.RemoveCallbacks(_backspaceRunnable);
        _handler?.PostDelayed(_backspaceRunnable, 100);
    }

    private bool TryClearChineseComposingOnLongPress()
    {
        if (_keyboardHandler == null || _keyboardHandler.GetAsciiMode())
        {
            return false;
        }

        var engine = _engineHost.CurrentEngine;
        if (engine == null)
        {
            return false;
        }

        string composingText = engine.GetComposingText();
        if (string.IsNullOrEmpty(composingText))
        {
            return false;
        }

        engine.Reset();
        _engineHost.ClearCandidates();

        // 显式重置 T9 buffer 作为安全网
        _keyboardHandler.NotifyT9BufferShouldReset();

        Log.Info("SpecialKeyHandler", $"Long press backspace cleared composing text, length={composingText.Length}");
        return true;
    }

    public void ResetShiftState()
    {
        _isShiftActive = false;
    }

    public void SetKeyboardType(IKeyboardType type)
    {
        _keyboardType = type;
    }

    public void Cleanup()
    {
        if (_handler != null)
        {
            _handler.RemoveCallbacksAndMessages(null);
            _handler = null;
        }
        _backspaceRunnable = null;
    }
}

