using Android.InputMethodServices;
using Android.Views.InputMethods;
using IME.Features.Input.Abstractions;

namespace IME.Features.Input;

public sealed class InputConnectionAdapter : IInputConnectionAdapter
{
    private readonly InputMethodService _service;

    public InputConnectionAdapter(InputMethodService service)
    {
        _service = service;
    }

    private IInputConnection Connection => _service?.CurrentInputConnection;

    public bool IsValid => Connection != null;

    public void CommitText(string text, int newCursorPosition)
    {
        Connection?.CommitText(text ?? string.Empty, newCursorPosition);
    }

    public void SetComposingText(string text, int newCursorPosition)
    {
        Connection?.SetComposingText(text ?? string.Empty, newCursorPosition);
    }

    public void DeleteSurroundingText(int beforeLength, int afterLength)
    {
        Connection?.DeleteSurroundingText(beforeLength, afterLength);
    }

    public bool PerformEditorAction(ImeAction action)
    {
        return Connection?.PerformEditorAction(action) ?? false;
    }
}
