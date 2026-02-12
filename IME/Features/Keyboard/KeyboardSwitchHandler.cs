using Android.Util;
using IME.Features.Input.Handlers;
using IME.Shared.Utils.Enums;

namespace IME.Features.Keyboard;

public class KeyboardSwitchHandler
{
    private readonly KeyboardHandler _keyboardHandler;
    private readonly SpecialKeyHandler _specialKeyHandler;

    public KeyboardSwitchHandler(KeyboardHandler keyboardHandler, SpecialKeyHandler specialKeyHandler)
    {
        _keyboardHandler = keyboardHandler;
        _specialKeyHandler = specialKeyHandler;
    }

    public void HandleSwitchToNumber()
    {
        if (_keyboardHandler == null) return;

        Log.Info("KeyboardSwitchHandler", "Switch to number keyboard");
        _keyboardHandler.PrepareNonT9Switch();
        _specialKeyHandler?.SetKeyboardType(IKeyboardType.Number);
        _keyboardHandler.SwitchKeyboard(IKeyboardType.Number);
    }

    public void HandleSwitchToSymbol()
    {
        if (_keyboardHandler == null) return;

        _keyboardHandler.PrepareNonT9Switch();

        bool isAsciiMode = _keyboardHandler.GetAsciiMode();
        IKeyboardType targetType;

        if (isAsciiMode)
        {
            Log.Info("KeyboardSwitchHandler", "Switch to ASCII symbol keyboard");
            targetType = IKeyboardType.Symbol;
        }
        else
        {
            Log.Info("KeyboardSwitchHandler", "Switch to Chinese symbol keyboard");
            targetType = IKeyboardType.SymbolCN;
        }

        _specialKeyHandler?.SetKeyboardType(targetType);
        _keyboardHandler.SwitchKeyboard(targetType);
    }

    public void HandleSwitchLanguage()
    {
        if (_keyboardHandler == null) return;

        Log.Info("KeyboardSwitchHandler", "Toggle ASCII/Chinese mode");
        bool isAsciiMode = _keyboardHandler.SwitchAsciiMode();

        var targetType = isAsciiMode ? IKeyboardType.UpperCase : IKeyboardType.LowerCase;
        _specialKeyHandler?.SetKeyboardType(targetType);
        _specialKeyHandler?.ResetShiftState();
    }

    public void HandleSwitchToLetter()
    {
        if (_keyboardHandler == null) return;

        Log.Info("KeyboardSwitchHandler", "Switch to letter keyboard");

        // 如果之前是 T9 模式，恢复 T9
        if (_keyboardHandler.TryRestoreT9())
        {
            return;
        }

        bool isAsciiMode = _keyboardHandler.GetAsciiMode();
        var targetType = isAsciiMode ? IKeyboardType.UpperCase : IKeyboardType.LowerCase;

        _specialKeyHandler?.SetKeyboardType(targetType);
        _specialKeyHandler?.ResetShiftState();
        _keyboardHandler.SwitchKeyboard(targetType);
    }
}
