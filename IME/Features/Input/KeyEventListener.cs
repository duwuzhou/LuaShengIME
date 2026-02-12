using System;
using IME.Features.Input.Feedback;
using IME.Features.Input.Handlers;
using IME.Features.Keyboard;

namespace IME.Features.Input;

[Obsolete("Use InputCoordinator instead.")]
public class KeyEventListener : IKeyEventProcessor
{
    private readonly InputCoordinator _coordinator;

    public KeyEventListener(Ime imeService)
        : this(imeService, new KeyboardHandler(imeService, imeService))
    {
    }

    public KeyEventListener(Ime imeService, KeyboardHandler keyboardHandler)
    {
        var connection = new InputConnectionAdapter(imeService);
        var feedback = new KeyFeedbackManager(imeService);
        var characterHandler = new CharacterInputHandler(imeService, connection, keyboardHandler);
        var specialKeyHandler = new SpecialKeyHandler(imeService, connection, keyboardHandler, imeService);
        var switchHandler = new KeyboardSwitchHandler(keyboardHandler, specialKeyHandler);

        _coordinator = new InputCoordinator(
            connection,
            feedback,
            characterHandler,
            specialKeyHandler,
            switchHandler,
            keyboardHandler);
    }

    public void OnKeyPressed(int keyCode)
    {
        _coordinator?.OnKeyPressed(keyCode);
    }

    public void OnKeyLongPress(int keyCode)
    {
        _coordinator?.OnKeyLongPress(keyCode);
    }

    public void OnKeyRelease(int keyCode)
    {
        _coordinator?.OnKeyRelease(keyCode);
    }

    public void OnTextCommit(string text)
    {
        _coordinator?.OnTextCommit(text);
    }

    public void Cleanup()
    {
        _coordinator?.Cleanup();
    }
}
