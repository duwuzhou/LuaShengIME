using IME.Shared.Abstractions;

namespace IME.Features.Input.Abstractions;

public interface IInputEngineHost
{
    bool IsAsciiMode { get; }
    IInputEngine? CurrentEngine { get; }
    void SetCurrentEngine(IInputEngine engine);
    void UpdateCandidates();
    void ClearCandidates();
    void RecordUserLexicon(string word, string pinyin);
    void NotifyCommittedText(string text, bool isPredictionCommit);
}
