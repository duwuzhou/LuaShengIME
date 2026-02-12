namespace IME.Features.Input;

public interface IKeyEventProcessor
{
    void OnKeyPressed(int keyCode);
    void OnKeyLongPress(int keyCode);
    void OnKeyRelease(int keyCode);
    void OnTextCommit(string text);
}
