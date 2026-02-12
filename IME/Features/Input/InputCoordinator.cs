using Android.Util;
using IME.Features.Input.Abstractions;
using IME.Features.Input.Feedback;
using IME.Features.Input.Handlers;
using IME.Features.Keyboard;

namespace IME.Features.Input;

public sealed class InputCoordinator : IKeyEventProcessor
{
    private static class KeyCodes
    {
        public const int Backspace = -5;
        public const int Enter = -3;
        public const int Space = 32;
        public const int Shift = -1;
        public const int SwitchNumber = -2;
        public const int SwitchSymbol = -6;
        public const int SwitchLanguage = -4;
        public const int SwitchLetter = -7;
        public const int SwitchToT9 = -8;
        public const int SwitchToQwerty = -9;
    }

    private readonly IInputConnectionAdapter _connection;
    private readonly KeyFeedbackManager _feedbackManager;
    private readonly CharacterInputHandler _characterHandler;
    private readonly SpecialKeyHandler _specialKeyHandler;
    private readonly KeyboardSwitchHandler _switchHandler;
    private readonly KeyboardHandler _keyboardHandler;

    public InputCoordinator(
        IInputConnectionAdapter connection,
        KeyFeedbackManager feedbackManager,
        CharacterInputHandler characterHandler,
        SpecialKeyHandler specialKeyHandler,
        KeyboardSwitchHandler switchHandler,
        KeyboardHandler keyboardHandler)
    {
        _connection = connection;
        _feedbackManager = feedbackManager;
        _characterHandler = characterHandler;
        _specialKeyHandler = specialKeyHandler;
        _switchHandler = switchHandler;
        _keyboardHandler = keyboardHandler;
    }

    public void OnKeyPressed(int keyCode)
    {
        _feedbackManager?.PerformKeyFeedback();

        if (_connection == null || !_connection.IsValid)
        {
            Log.Warn("InputCoordinator", "No valid input connection; skipping key press.");
            return;
        }

        if (_keyboardHandler?.TryHandlePinyinEditKey(keyCode) == true)
        {
            return;
        }

        switch (keyCode)
        {
            case KeyCodes.Backspace:
                _specialKeyHandler?.HandleBackspace();
                break;
            case KeyCodes.Enter:
                _specialKeyHandler?.HandleEnter();
                break;
            case KeyCodes.Space:
                _specialKeyHandler?.HandleSpace();
                break;
            case KeyCodes.Shift:
                _specialKeyHandler?.HandleShift();
                break;
            case KeyCodes.SwitchNumber:
                _switchHandler?.HandleSwitchToNumber();
                break;
            case KeyCodes.SwitchSymbol:
                _switchHandler?.HandleSwitchToSymbol();
                break;
            case KeyCodes.SwitchLanguage:
                _switchHandler?.HandleSwitchLanguage();
                break;
            case KeyCodes.SwitchLetter:
                _switchHandler?.HandleSwitchToLetter();
                break;
            case KeyCodes.SwitchToT9:
                _keyboardHandler?.SwitchToT9Mode();
                break;
            case KeyCodes.SwitchToQwerty:
                _keyboardHandler?.SwitchFromT9ToQwerty();
                break;
            default:
                bool isShiftActive = _specialKeyHandler?.IsShiftActive ?? false;
                _characterHandler?.HandleCharacter(keyCode, isShiftActive);
                break;
        }
    }

    public void OnKeyLongPress(int keyCode)
    {
        Log.Info("InputCoordinator", $"Long press: {(char)keyCode}");

        switch (keyCode)
        {
            case KeyCodes.Backspace:
                _specialKeyHandler?.StartBackspaceRepeating();
                break;
        }
    }

    public void OnKeyRelease(int keyCode)
    {
        switch (keyCode)
        {
            case KeyCodes.Backspace:
                _specialKeyHandler?.StopBackspaceRepeating();
                break;
        }
    }

    public void OnTextCommit(string text)
    {
        _feedbackManager?.PerformKeyFeedback();

        if (_connection == null || !_connection.IsValid)
        {
            return;
        }

        _characterHandler?.CommitComposingIfAny();
        _connection.CommitText(text, 1);
    }

    public void Cleanup()
    {
        _feedbackManager?.Cleanup();
        _specialKeyHandler?.Cleanup();
    }
}
