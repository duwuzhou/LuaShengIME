#if DEBUG
using System.Collections.Generic;
using System.Diagnostics;
using Android.Content;
using Android.Views.InputMethods;
using IME.Features.Input.Abstractions;
using IME.Features.Input.Handlers;
using IME.Shared.Abstractions;

namespace IME.Features.Input.Testing;

internal static class InputHandlerSmokeTests
{
    public static void Run()
    {
        TestAsciiShift();
        TestDirectCommit();
        TestEngineHandledKey();
        TestCommitComposing();
    }

    private static void TestAsciiShift()
    {
        var connection = new FakeConnection();
        var host = new FakeHost { IsAsciiModeValue = true };
        var handler = new CharacterInputHandler(host, connection, null!);

        handler.HandleCharacter('A', false);
        Debug.Assert(connection.LastCommitted == "a", "ASCII shift down should lowercase.");

        connection.Reset();
        handler.HandleCharacter('a', true);
        Debug.Assert(connection.LastCommitted == "A", "ASCII shift up should uppercase.");
    }

    private static void TestDirectCommit()
    {
        var connection = new FakeConnection();
        var host = new FakeHost { IsAsciiModeValue = false };
        var handler = new CharacterInputHandler(host, connection, null!);

        handler.HandleCharacter('1', false);
        Debug.Assert(connection.LastCommitted == "1", "Direct commit characters should be committed.");
    }

    private static void TestEngineHandledKey()
    {
        var engine = new FakeEngine { ProcessKeyResult = true };
        var connection = new FakeConnection();
        var host = new FakeHost { IsAsciiModeValue = false, CurrentEngineValue = engine };
        var handler = new CharacterInputHandler(host, connection, null!);

        handler.HandleCharacter('b', false);
        Debug.Assert(connection.LastCommitted == null, "Engine-handled keys should not commit directly.");
    }

    private static void TestCommitComposing()
    {
        var engine = new FakeEngine { ComposingText = "ni" };
        var connection = new FakeConnection();
        var host = new FakeHost { IsAsciiModeValue = false, CurrentEngineValue = engine };
        var handler = new CharacterInputHandler(host, connection, null!);

        handler.CommitComposingIfAny();
        Debug.Assert(connection.LastCommitted == "ni", "Composing text should be committed.");
        Debug.Assert(host.LastRecordedWord == "ni" && host.LastRecordedPinyin == "ni", "Lexicon should record committed word.");
    }

    private sealed class FakeConnection : IInputConnectionAdapter
    {
        public bool IsValid => true;
        public string? LastCommitted { get; private set; }

        public void CommitText(string text, int newCursorPosition)
        {
            LastCommitted = text;
        }

        public void SetComposingText(string text, int newCursorPosition)
        {
        }

        public void DeleteSurroundingText(int beforeLength, int afterLength)
        {
        }

        public bool PerformEditorAction(ImeAction action)
        {
            return true;
        }

        public void Reset()
        {
            LastCommitted = null;
        }
    }

    private sealed class FakeHost : IInputEngineHost
    {
        public bool IsAsciiModeValue { get; set; }
        public IInputEngine? CurrentEngineValue { get; set; }
        public string? LastRecordedWord { get; private set; }
        public string? LastRecordedPinyin { get; private set; }

        public bool IsAsciiMode => IsAsciiModeValue;
        public IInputEngine? CurrentEngine => CurrentEngineValue;

        public void SetCurrentEngine(IInputEngine engine)
        {
            CurrentEngineValue = engine;
        }

        public void UpdateCandidates()
        {
        }

        public void ClearCandidates()
        {
        }

        public void RecordUserLexicon(string word, string pinyin)
        {
            LastRecordedWord = word;
            LastRecordedPinyin = pinyin;
        }

        public void NotifyCommittedText(string text, bool isPredictionCommit)
        {
        }
    }

    private sealed class FakeEngine : IInputEngine
    {
        public bool ProcessKeyResult { get; set; }
        public string ComposingText { get; set; } = string.Empty;

        public bool Initialize(Context context) => true;

        public bool ProcessKey(int keyCode)
        {
            return ProcessKeyResult;
        }

        public string GetComposingText() => ComposingText;

        public List<string> GetCandidates() => new();

        public List<string> GetCandidateComments() => new();

        public string SelectCandidate(int index) => ComposingText;

        public void Reset()
        {
            ComposingText = string.Empty;
        }

        public void SetAsciiMode(bool asciiMode)
        {
        }

        public bool GetAsciiMode() => false;

        public void SetSimplification(bool simplified)
        {
        }

        public bool SupportsPaging => false;

        public bool TryGetPagingInfo(out int pageNo, out bool isLastPage, out int pageSize)
        {
            pageNo = 0;
            isLastPage = true;
            pageSize = 0;
            return false;
        }

        public bool ChangePage(bool backward) => false;

        public void Dispose()
        {
        }
    }
}
#endif
