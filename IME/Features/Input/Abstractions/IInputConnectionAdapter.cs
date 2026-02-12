using Android.Views.InputMethods;

namespace IME.Features.Input.Abstractions;

public interface IInputConnectionAdapter
{
    bool IsValid { get; }
    void CommitText(string text, int newCursorPosition);
    void SetComposingText(string text, int newCursorPosition);
    void DeleteSurroundingText(int beforeLength, int afterLength);
    bool PerformEditorAction(ImeAction action);
}
