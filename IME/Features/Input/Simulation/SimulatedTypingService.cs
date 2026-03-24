using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Android.OS;
using Android.Util;
using IME.Features.Keyboard;
using IME.Features.Settings;
using IME.Shared.Abstractions;

namespace IME.Features.Input.Simulation;

internal sealed class SimulatedTypingService : IDisposable
{
    private const string Tag = "SimulatedTyping";
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

        int startDelayMs = SettingsActivity.GetSimulatedTypingStartDelayMs(_imeService);
        int keyDelayMs = SettingsActivity.GetSimulatedTypingSpeedMs(_imeService);
        int candidateDelayMs = Math.Max(48, keyDelayMs * 2);
        List<(string Text, bool ShouldSendAfter)> segments = BuildSegments(text, sendAfterCommit);
        if (segments.Count == 0)
        {
            return;
        }

        try
        {
            if (startDelayMs > 0)
            {
                await DelayAsync(startDelayMs).ConfigureAwait(false);
                if (!IsCurrentRequest(requestVersion))
                {
                    return;
                }
            }

            PinyinReverseLookup lookup = await GetLookupAsync().ConfigureAwait(false);
            for (int segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++)
            {
                IReadOnlyList<PinyinLookupToken> tokens = lookup.Tokenize(segments[segmentIndex].Text);
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
                        committedByPinyin = await TryCommitByPinyinAsync(requestVersion, token.Text, token.Pinyin!, keyDelayMs, candidateDelayMs).ConfigureAwait(false);
                    }

                    if (!committedByPinyin)
                    {
                        await CommitRawAsync(requestVersion, token.Text, keyDelayMs).ConfigureAwait(false);
                    }
                }

                if (segments[segmentIndex].ShouldSendAfter && IsCurrentRequest(requestVersion))
                {
                    await DelayAsync(candidateDelayMs).ConfigureAwait(false);
                    await SendCurrentInputAsync(requestVersion).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn(Tag, $"Simulated typing failed, fallback to segmented commit: {ex.Message}");
            if (!IsCurrentRequest(requestVersion))
            {
                return;
            }

            await FallbackCommitAsync(requestVersion, segments, keyDelayMs, candidateDelayMs).ConfigureAwait(false);
        }
    }

    private async Task<bool> TryCommitByPinyinAsync(int requestVersion, string text, string pinyin, int keyDelayMs, int candidateDelayMs)
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

            await DelayAsync(keyDelayMs).ConfigureAwait(false);
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
            await DelayAsync(candidateDelayMs).ConfigureAwait(false);
        }

        return committed;
    }

    private async Task CommitRawAsync(int requestVersion, string text, int keyDelayMs)
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

            await DelayAsync(keyDelayMs).ConfigureAwait(false);
        }
    }

    private async Task FallbackCommitAsync(int requestVersion, IReadOnlyList<(string Text, bool ShouldSendAfter)> segments, int keyDelayMs, int candidateDelayMs)
    {
        for (int i = 0; i < segments.Count; i++)
        {
            if (!IsCurrentRequest(requestVersion))
            {
                return;
            }

            await CommitRawAsync(requestVersion, segments[i].Text, keyDelayMs).ConfigureAwait(false);
            if (segments[i].ShouldSendAfter)
            {
                await DelayAsync(candidateDelayMs).ConfigureAwait(false);
                await SendCurrentInputAsync(requestVersion).ConfigureAwait(false);
            }
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

    private Task SendCurrentInputAsync(int requestVersion)
    {
        return RunOnMainThreadAsync(() =>
        {
            if (!IsCurrentRequest(requestVersion))
            {
                return;
            }

            _imeService.SendCurrentInput();
        });
    }

    private static List<(string Text, bool ShouldSendAfter)> BuildSegments(string text, bool sendAfterCommit)
    {
        List<(string Text, bool ShouldSendAfter)> segments = new();
        string normalized = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
        string[] rawSegments = normalized.Split('\n');

        int lastNonEmptyIndex = -1;
        for (int i = 0; i < rawSegments.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(rawSegments[i]))
            {
                lastNonEmptyIndex = i;
            }
        }

        for (int i = 0; i < rawSegments.Length; i++)
        {
            string segmentText = rawSegments[i];
            if (string.IsNullOrWhiteSpace(segmentText))
            {
                continue;
            }

            bool shouldSendAfter = i < rawSegments.Length - 1;
            if (i == lastNonEmptyIndex && sendAfterCommit)
            {
                shouldSendAfter = true;
            }

            segments.Add((segmentText, shouldSendAfter));
        }

        return segments;
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
