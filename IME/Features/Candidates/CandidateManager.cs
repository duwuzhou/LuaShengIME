using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Android.Util;
using IME.Features.UserLexicon;
using IME.Shared.Abstractions;

namespace IME.Features.Candidates;

public class CandidateManager
{
    private const string Tag = "CandidateManager";
    private const int LocalExtrasCacheCapacity = 48;

    private readonly object _cacheLock = new();
    private readonly Dictionary<string, List<CandidateEntry>> _localExtrasCache = new(StringComparer.Ordinal);
    private readonly HashSet<string> _localQueryInFlight = new(StringComparer.Ordinal);

    private Candidate? _candidateView;
    private IInputEngine? _inputEngine;
    private LocalUserLexiconStore? _localLexiconStore;

    private string _lastRenderedComposing = string.Empty;
    private string _lastRenderedRawComposing = string.Empty;
    private int _lastRenderedCandidatesHash;
    private int _lastRenderedCommentsHash;
    private int _lastRenderedExtrasHash;

    public void SetCandidateView(Candidate candidateView)
    {
        _candidateView = candidateView;
        if (_candidateView != null)
        {
            _localLexiconStore = new LocalUserLexiconStore(_candidateView.Context);
        }

        ResetRenderCache();
    }

    public void SetInputEngine(IInputEngine inputEngine)
    {
        _inputEngine = inputEngine;
        _candidateView?.SetInputEngine(inputEngine);
        ResetRenderCache();
    }

    public void NotifyEngineChanged(IInputEngine newEngine)
    {
        _inputEngine = newEngine;
        _candidateView?.SetInputEngine(newEngine);
        ResetRenderCache();
        Log.Info(Tag, "Input engine updated.");
    }

    public void UpdateChineseCandidates(IInputEngine engine, string? displayComposingOverride = null)
    {
        UpdateChineseCandidates(engine, displayComposingOverride, hideNumericCandidates: false);
    }

    public void UpdateChineseCandidates(IInputEngine engine, string? displayComposingOverride, bool hideNumericCandidates)
    {
        if (_candidateView == null || engine == null)
        {
            return;
        }

        try
        {
            string composingText = engine.GetComposingText() ?? string.Empty;
            string displayComposingText = displayComposingOverride is null
                ? composingText
                : displayComposingOverride;
            List<string> candidates = engine.GetCandidates() ?? new List<string>();
            List<string> comments = engine.GetCandidateComments() ?? new List<string>();

            if (hideNumericCandidates)
            {
                if (displayComposingOverride is null && IsNumericLikeText(composingText))
                {
                    displayComposingText = string.Empty;
                }
            }

            List<CandidateEntry> extras = GetCachedLocalExtras(composingText) ?? new List<CandidateEntry>();
            bool shouldQueueLocalExtras = extras.Count == 0
                                          && !string.IsNullOrWhiteSpace(composingText)
                                          && !(hideNumericCandidates && displayComposingOverride is not null);
            if (shouldQueueLocalExtras)
            {
                QueueLocalExtrasFetch(composingText, candidates, displayComposingOverride, hideNumericCandidates);
            }

            int candidatesHash = ComputeStringListHash(candidates);
            int commentsHash = ComputeStringListHash(comments);
            int extrasHash = ComputeEntryListHash(extras);

            if (string.Equals(_lastRenderedComposing, displayComposingText, StringComparison.Ordinal)
                && string.Equals(_lastRenderedRawComposing, composingText, StringComparison.Ordinal)
                && _lastRenderedCandidatesHash == candidatesHash
                && _lastRenderedCommentsHash == commentsHash
                && _lastRenderedExtrasHash == extrasHash)
            {
                return;
            }

            _candidateView.SetInputPreview(displayComposingText);
            _candidateView.SetCandidatesWithExtras(candidates, comments, extras);

            _lastRenderedComposing = displayComposingText;
            _lastRenderedRawComposing = composingText;
            _lastRenderedCandidatesHash = candidatesHash;
            _lastRenderedCommentsHash = commentsHash;
            _lastRenderedExtrasHash = extrasHash;
        }
        catch (Exception ex)
        {
            Log.Error(Tag, $"Update candidates failed: {ex.Message}");
        }
    }

    public bool TryHandlePinyinEditKey(int keyCode)
    {
        if (_candidateView == null)
        {
            return false;
        }

        return _candidateView.TryHandleInlineEditKey(keyCode);
    }

    public void Clear()
    {
        _candidateView?.Clear();
        ResetRenderCache();
    }

    public void ShowPredictionCandidates(IReadOnlyList<string> predictions, int maxResults)
    {
        if (_candidateView == null)
        {
            return;
        }

        if (predictions == null || predictions.Count == 0 || maxResults <= 0)
        {
            _candidateView.Clear();
            ResetRenderCache();
            return;
        }

        int limit = Math.Min(predictions.Count, maxResults);
        var extras = new List<CandidateEntry>(limit);
        for (int i = 0; i < limit; i++)
        {
            string text = predictions[i];
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            extras.Add(new CandidateEntry(text, "预测", 0, i, true, true));
        }

        if (extras.Count == 0)
        {
            _candidateView.Clear();
            ResetRenderCache();
            return;
        }

        _candidateView.SetInputPreview(string.Empty);
        _candidateView.SetCandidatesWithExtras(new List<string>(), new List<string>(), extras);

        _lastRenderedComposing = string.Empty;
        _lastRenderedCandidatesHash = 0;
        _lastRenderedCommentsHash = 0;
        _lastRenderedExtrasHash = ComputeEntryListHash(extras);
    }

    public void Cleanup()
    {
        _inputEngine = null;
        _candidateView = null;
        _localLexiconStore?.Dispose();
        _localLexiconStore = null;
        ResetRenderCache();
    }

    private List<CandidateEntry>? GetCachedLocalExtras(string composingText)
    {
        if (string.IsNullOrWhiteSpace(composingText))
        {
            return null;
        }

        lock (_cacheLock)
        {
            if (_localExtrasCache.TryGetValue(composingText, out List<CandidateEntry>? extras))
            {
                return extras;
            }
        }

        return null;
    }

    private void QueueLocalExtrasFetch(
        string composingText,
        List<string> engineCandidates,
        string? displayComposingOverride,
        bool hideNumericCandidates)
    {
        if (_localLexiconStore == null || string.IsNullOrWhiteSpace(composingText))
        {
            return;
        }

        lock (_cacheLock)
        {
            if (_localExtrasCache.ContainsKey(composingText) || _localQueryInFlight.Contains(composingText))
            {
                return;
            }

            _localQueryInFlight.Add(composingText);
        }

        var candidateSnapshot = engineCandidates?.Where(item => !string.IsNullOrWhiteSpace(item)).ToList() ?? new List<string>();

        _ = Task.Run(() => BuildLocalCandidates(composingText, candidateSnapshot))
            .ContinueWith(task =>
            {
                List<CandidateEntry> extras = task.Status == TaskStatus.RanToCompletion
                    ? task.Result
                    : new List<CandidateEntry>();

                lock (_cacheLock)
                {
                    _localQueryInFlight.Remove(composingText);
                    _localExtrasCache[composingText] = extras;
                    TrimLocalExtrasCache();
                }

                Candidate? candidateView = _candidateView;
                IInputEngine? engine = _inputEngine;
                if (candidateView == null || engine == null)
                {
                    return;
                }

                candidateView.Post(() =>
                {
                    if (_candidateView == null || _inputEngine == null)
                    {
                        return;
                    }

                    string activeComposing = _inputEngine.GetComposingText() ?? string.Empty;
                    if (!string.Equals(activeComposing, composingText, StringComparison.Ordinal))
                    {
                        return;
                    }

                    UpdateChineseCandidates(_inputEngine, displayComposingOverride, hideNumericCandidates);
                });
            }, TaskScheduler.Default);
    }

    private void TrimLocalExtrasCache()
    {
        while (_localExtrasCache.Count > LocalExtrasCacheCapacity)
        {
            string oldestKey = _localExtrasCache.Keys.First();
            _localExtrasCache.Remove(oldestKey);
        }
    }

    private List<CandidateEntry> BuildLocalCandidates(string composingText, List<string> engineCandidates)
    {
        var extras = new List<CandidateEntry>();
        if (_localLexiconStore == null || string.IsNullOrWhiteSpace(composingText))
        {
            return extras;
        }

        var existing = new HashSet<string>(engineCandidates ?? new List<string>(), StringComparer.Ordinal);
        var entries = _localLexiconStore.QueryByPinyinPrefix(composingText, 6);
        int index = 0;

        foreach (UserLexiconEntry entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Word))
            {
                continue;
            }

            if (existing.Contains(entry.Word))
            {
                continue;
            }

            extras.Add(new CandidateEntry(entry.Word, "本地", -1, index, true));
            index++;
        }

        return extras;
    }

    private void ResetRenderCache()
    {
        lock (_cacheLock)
        {
            _localExtrasCache.Clear();
            _localQueryInFlight.Clear();
        }

        _lastRenderedComposing = string.Empty;
        _lastRenderedRawComposing = string.Empty;
        _lastRenderedCandidatesHash = 0;
        _lastRenderedCommentsHash = 0;
        _lastRenderedExtrasHash = 0;
    }

    private static int ComputeStringListHash(IReadOnlyList<string>? values)
    {
        if (values == null || values.Count == 0)
        {
            return 0;
        }

        unchecked
        {
            int hash = 17;
            for (int i = 0; i < values.Count; i++)
            {
                string value = values[i] ?? string.Empty;
                hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(value);
            }

            return (hash * 31) + values.Count;
        }
    }

    private static int ComputeEntryListHash(IReadOnlyList<CandidateEntry>? values)
    {
        if (values == null || values.Count == 0)
        {
            return 0;
        }

        unchecked
        {
            int hash = 17;
            for (int i = 0; i < values.Count; i++)
            {
                CandidateEntry entry = values[i];
                hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(entry.Text ?? string.Empty);
                hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(entry.Comment ?? string.Empty);
                hash = (hash * 31) + entry.PageIndex;
                hash = (hash * 31) + entry.IndexOnPage;
                hash = (hash * 31) + (entry.IsLocal ? 1 : 0);
                hash = (hash * 31) + (entry.IsPrediction ? 1 : 0);
            }

            return (hash * 31) + values.Count;
        }
    }

    private static bool IsNumericLikeText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        bool hasDigit = false;
        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            if (char.IsDigit(ch))
            {
                hasDigit = true;
                continue;
            }

            if (char.IsLetter(ch))
            {
                return false;
            }

            if (char.IsWhiteSpace(ch) || char.IsPunctuation(ch) || char.IsSymbol(ch))
            {
                continue;
            }

            return false;
        }

        return hasDigit;
    }
}
