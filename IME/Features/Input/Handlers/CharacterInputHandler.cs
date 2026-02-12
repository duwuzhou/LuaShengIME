using IME.Features.Input.Abstractions;
using IME.Features.Keyboard;

namespace IME.Features.Input.Handlers;

public class CharacterInputHandler
{
    private readonly IInputEngineHost _engineHost;
    private readonly IInputConnectionAdapter _connection;
    private readonly KeyboardHandler _keyboardHandler;

    public CharacterInputHandler(IInputEngineHost engineHost, IInputConnectionAdapter connection, KeyboardHandler keyboardHandler)
    {
        _engineHost = engineHost;
        _connection = connection;
        _keyboardHandler = keyboardHandler;
    }

    public void HandleCharacter(int keyCode, bool isShiftActive)
    {
        if (_engineHost == null || _connection == null || !_connection.IsValid)
        {
            return;
        }

        if (_engineHost.IsAsciiMode)
        {
            HandleAsciiModeCharacter(keyCode, isShiftActive);
        }
        else
        {
            HandleChineseModeCharacter(keyCode);
        }
    }

    private void HandleAsciiModeCharacter(int keyCode, bool isShiftActive)
    {
        char keyChar = (char)keyCode;

        if (keyChar >= 'A' && keyChar <= 'Z')
        {
            if (!isShiftActive)
            {
                keyChar = char.ToLower(keyChar);
            }
        }
        else if (keyChar >= 'a' && keyChar <= 'z')
        {
            if (isShiftActive)
            {
                keyChar = char.ToUpper(keyChar);
            }
        }

        _connection.CommitText(keyChar.ToString(), 1);
        _engineHost.NotifyCommittedText(keyChar.ToString(), false);
    }

    private void HandleChineseModeCharacter(int keyCode)
    {
        char keyChar = (char)keyCode;
        bool isT9Mode = _keyboardHandler?.IsT9Mode ?? false;

        // T9 模式下，数字 2-9 发送给 Rime 引擎
        if (isT9Mode && keyChar >= '2' && keyChar <= '9')
        {
            var engine = _engineHost.CurrentEngine;
            if (engine?.ProcessKey(keyCode) == true)
            {
                return;
            }
            _connection.CommitText(keyChar.ToString(), 1);
            _engineHost.NotifyCommittedText(keyChar.ToString(), false);
            return;
        }

        // T9 模式下，1 键输入中文逗号（最常用标点）
        if (isT9Mode && keyChar == '1')
        {
            CommitComposingIfAny();
            _connection.CommitText("，", 1);
            _engineHost.NotifyCommittedText("，", false);
            return;
        }

        // T9 模式下，0 键等同空格（有编辑中文本时选词，无编辑中文本时输入空格）
        if (isT9Mode && keyChar == '0')
        {
            var engine = _engineHost.CurrentEngine;
            if (engine != null)
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
            return;
        }

        if (IsDirectCommitChar(keyChar))
        {
            CommitComposingIfAny();
            _connection.CommitText(keyChar.ToString(), 1);
            _engineHost.NotifyCommittedText(keyChar.ToString(), false);
            return;
        }

        if ((keyChar >= 'a' && keyChar <= 'z') || (keyChar >= 'A' && keyChar <= 'Z'))
        {
            keyChar = char.ToLower(keyChar);
            var engine = _engineHost.CurrentEngine;
            if (engine?.ProcessKey((int)keyChar) == true)
            {
                // Candidates are updated by the keyboard handler after key events.
            }
            else
            {
                _connection.CommitText(keyChar.ToString(), 1);
                _engineHost.NotifyCommittedText(keyChar.ToString(), false);
            }
            return;
        }

        _connection.CommitText(keyChar.ToString(), 1);
        _engineHost.NotifyCommittedText(keyChar.ToString(), false);
    }

    public bool IsDirectCommitChar(char c)
    {
        if (c >= '0' && c <= '9') return true;
        if ("!@#$%^&*()-_=+[]{}\\|;:'\",.<>?/`~".IndexOf(c) >= 0) return true;
        if (c == '\u00D7' || c == '\u00F7') return true;
        return false;
    }

    public void CommitComposingIfAny()
    {
        var engine = _engineHost.CurrentEngine;
        if (engine == null)
        {
            return;
        }

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
        }
    }
}
