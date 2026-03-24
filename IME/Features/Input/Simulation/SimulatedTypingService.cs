using Android.OS;
using Android.Util;
using Android.Views;
using Android.Views.InputMethods;
using IME.Features.Keyboard;
using IME.Shared.Abstractions;

namespace IME.Features.Input.Simulation;

internal sealed class SimulatedTypingService : IDisposable
{
    private const string Tag = "SimulatedTyping";
    private const int KeyDelayMs = 48;
    private const int CandidateDelayMs = 96;
    private const int MaxPageSearchCount = 12;

    private readonly Ime _imeService;
    private readonly Handler _mainHandler;
    private readonly object _lookupLock = new();

    private Task<PinyinReverseLookup>? _lookupTask;
    private int _requestVersion;

    public SimulatedTypingService(Ime imeService)
    {
        _imeService = imeService;
        _mainHandler = new Handler(Looper.MainLooper);
    }

    public void StartTyping(string text, bool sendAfterCommit)
    {
        int requestVersion = Interlocked.Increment(ref _requestVersion);
        _ = RunTypingAsync(requestVersion, text ?? string.Empty, sendAfterCommit);
    }

    public void Dispose()
    {
        Interlocked.Increment(ref _requestVersion);
        _mainHandler.RemoveCallbacksAndMessages(null);
    }

    private async Task RunTypingAsync(int requestVersion, string text, bool sendAfterCommit)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        try
        {
            PinyinReverseLookup lookup = await GetLookupAsync().ConfigureAwait(false);
            IReadOnlyList<PinyinLookupToken> tokens = lookup.Tokenize(text);

            for (int i = 0; i < tokens.Count; i++)
            {
                if (!IsCurrentRequest(requestVersion))
                {
                    return;
                }

                PinyinLookupToken token = tokens[i];
                if (string.IsNullOrEmpty(token.Text))
                {
                    continue;
                }

                bool committedByPinyin = false;
                if (!string.IsNullOrEmpty(token.Pinyin))
                {
                    committedByPinyin = await TryCommitByPinyinAsync(requestVersion, token.Text, token.Pinyin!).ConfigureAwait(false);
                }

                if (!committedByPinyin)
                {
                    await CommitRawAsync(requestVersion, token.Text).ConfigureAwait(false);
                }
            }

            if (sendAfterCommit && IsCurrentRequest(requestVersion))
            {
                await DelayAsync(CandidateDelayMs).ConfigureAwait(false);
                await RunOnMainThreadAsync(() =>
                {
                    if (!IsCurrentRequest(requestVersion))
                    {
                        return;
                    }

                    var inputConnection = _imeService.CurrentInputConnection;
                    if (inputConnection == null)
                    {
                        return;
                    }

                    inputConnection.FinishComposingText();
                    if (inputConnection.PerformEditorAction(ImeAction.Send))
                    {
                        return;
                    }

                    long now = Java.Lang.JavaSystem.CurrentTimeMillis();
                    var downEvent = new KeyEvent(now, now, KeyEventActions.Down, Keycode.Enter, 0);
                    var upEvent = new KeyEvent(now, now, KeyEventActions.Up, Keycode.Enter, 0);
                    inputConnection.SendKeyEvent(downEvent);
                    inputConnection.SendKeyEvent(upEvent);
                }).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Log.Warn(Tag, $"Simulated typing failed, fallback to direct commit: {ex.Message}");
            if (!IsCurrentRequest(requestVersion))
            {
                return;
            }

            await RunOnMainThreadAsync(() =>
            {
                if (!IsCurrentRequest(requestVersion))
                {
                    return;
                }

                _imeService.CommitText(text, false);
            }).ConfigureAwait(false);
        }
    }

    private async Task<bool> TryCommitByPinyinAsync(int requestVersion, string text, string pinyin)
    {
        bool canUsePinyin = false;
        await RunOnMainThreadAsync(() =>
        {
            KeyboardHandler? keyboardHandler = _imeService.CurrentKeyboardHandler;
            IInputEngine? engine = _imeService._currentInputEngine;

            canUsePinyin = keyboardHandler != null
                           && engine != null
                           && !keyboardHandler.GetAsciiMode()
                           && !keyboardHandler.IsT9Mode;

            if (canUsePinyin)
            {
                ResetComposition(engine, keyboardHandler);
            }
        }).ConfigureAwait(false);

        if (!canUsePinyin)
        {
            return false;
        }

        for (int i = 0; i < pinyin.Length; i++)
        {
            if (!IsCurrentRequest(requestVersion))
            {
                return false;
            }

            char key = pinyin[i];
            await RunOnMainThreadAsync(() =>
            {
                IInputEngine? engine = _imeService._currentInputEngine;
                KeyboardHandler? keyboardHandler = _imeService.CurrentKeyboardHandler;
                if (engine == null || keyboardHandler == null)
                {
                    return;
                }

                engine.ProcessKey(key);
                keyboardHandler.UpdateChineseCandidates();
            }).ConfigureAwait(false);

            await DelayAsync(KeyDelayMs).ConfigureAwait(false);
        }

        bool committed = false;
        await RunOnMainThreadAsync(() =>
        {
            IInputEngine? engine = _imeService._currentInputEngine;
            KeyboardHandler? keyboardHandler = _imeService.CurrentKeyboardHandler;
            if (engine == null || keyboardHandler == null)
            {
                return;
            }

            committed = TryCommitCandidate(engine, keyboardHandler, text);
            if (!committed)
            {
                ResetComposition(engine, keyboardHandler);
            }
        }).ConfigureAwait(false);

        if (committed)
        {
            await DelayAsync(CandidateDelayMs).ConfigureAwait(false);
        }

        return committed;
    }

    private async Task CommitRawAsync(int requestVersion, string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            if (!IsCurrentRequest(requestVersion))
            {
                return;
            }

            string chunk = text[i].ToString();
            await RunOnMainThreadAsync(() =>
            {
                KeyboardHandler? keyboardHandler = _imeService.CurrentKeyboardHandler;
                IInputEngine? engine = _imeService._currentInputEngine;
                if (engine != null && keyboardHandler != null)
                {
                    ResetComposition(engine, keyboardHandler);
                }

                _imeService.CommitText(chunk, false);
            }).ConfigureAwait(false);

            await DelayAsync(KeyDelayMs).ConfigureAwait(false);
        }
    }

    private bool TryCommitCandidate(IInputEngine engine, KeyboardHandler keyboardHandler, string expectedText)
    {
        for (int page = 0; page < MaxPageSearchCount; page++)
        {
            List<string> candidates = engine.GetCandidates();
            int candidateIndex = IndexOfCandidate(candidates, expectedText);
            if (candidateIndex >= 0)
            {
                string composingText = engine.GetComposingText();
                string committed = engine.SelectCandidate(candidateIndex);
                if (!string.IsNullOrEmpty(committed))
                {
                    _imeService.CommitText(committed, false);
                    if (!string.IsNullOrWhiteSpace(composingText))
                    {
                        _imeService.RecordUserLexicon(committed, composingText);
                    }

                    ResetComposition(engine, keyboardHandler);
                    return true;
                }

                break;
            }

            if (!engine.SupportsPaging
                || !engine.TryGetPagingInfo(out _, out bool isLastPage, out _)
                || isLastPage
                || !engine.ChangePage(backward: false))
            {
                break;
            }

            keyboardHandler.UpdateChineseCandidates();
        }

        return false;
    }

    private static int IndexOfCandidate(IReadOnlyList<string> candidates, string expectedText)
    {
        for (int i = 0; i < candidates.Count; i++)
        {
            if (string.Equals(candidates[i], expectedText, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static void ResetComposition(IInputEngine engine, KeyboardHandler keyboardHandler)
    {
        engine.Reset();
        keyboardHandler.ClearCandidates();
        keyboardHandler.NotifyT9BufferShouldReset();
    }

    private Task<PinyinReverseLookup> GetLookupAsync()
    {
        lock (_lookupLock)
        {
            _lookupTask ??= PinyinReverseLookup.LoadAsync(_imeService);
            return _lookupTask;
        }
    }

    private bool IsCurrentRequest(int requestVersion)
    {
        return requestVersion == Volatile.Read(ref _requestVersion);
    }

    private Task DelayAsync(int delayMs)
    {
        return Task.Delay(delayMs);
    }

    private Task RunOnMainThreadAsync(Action action)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _mainHandler.Post(() =>
        {
            try
            {
                action();
                tcs.TrySetResult(true);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        return tcs.Task;
    }
}
