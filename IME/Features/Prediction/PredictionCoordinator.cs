using System;
using System.Collections.Generic;
using Android.Content;
using Android.OS;
using Android.Util;
using IME.Features.Input.Abstractions;
using IME.Features.Keyboard;
using IME.Features.Settings;
using IME.Shared.Abstractions;
using IME.Shared.InputEngine;

namespace IME.Features.Prediction;

internal sealed class PredictionCoordinator
{
    private const int DebounceMs = 80;
    private readonly Context _context;
    private readonly IInputEngineHost _engineHost;
    private readonly KeyboardHandler _keyboardHandler;
    private readonly LocalPredictionStore _localStore;
    private readonly CustomPredictionStore _customStore;
    private readonly Handler _handler;
    private readonly Action _runPrediction;
    private readonly object _lock = new object();
    private volatile bool _disposed;
    private string _lastToken = string.Empty;
    private string _pendingToken = string.Empty;
    private int _remainingRounds;

    public PredictionCoordinator(Context context, IInputEngineHost engineHost, KeyboardHandler keyboardHandler)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _engineHost = engineHost ?? throw new ArgumentNullException(nameof(engineHost));
        _keyboardHandler = keyboardHandler ?? throw new ArgumentNullException(nameof(keyboardHandler));
        _localStore = new LocalPredictionStore(context);
        _customStore = new CustomPredictionStore(context);
        _handler = new Handler(Looper.MainLooper);
        _runPrediction = RunPrediction;
    }

    public void OnCommittedText(string text, bool isPredictionCommit)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (!SettingsActivity.GetPredictionEnabled(_context))
        {
            return;
        }

        bool alwaysPredict = SettingsActivity.GetPredictionAlways(_context);
        int maxRounds = SettingsActivity.GetPredictionRounds(_context);
        if (maxRounds <= 0)
        {
            return;
        }

        var tokens = PredictionTokenizer.ExtractTokens(text);

        lock (_lock)
        {
            if (isPredictionCommit)
            {
                if (!alwaysPredict)
                {
                    if (_remainingRounds <= 0)
                    {
                        return;
                    }

                    _remainingRounds--;
                }
            }
            else
            {
                UpdateLocalModel(tokens);
                _remainingRounds = alwaysPredict ? int.MaxValue : maxRounds;
            }

            UpdateLastToken(tokens);
            _pendingToken = _lastToken;
        }

        if (string.IsNullOrWhiteSpace(_pendingToken))
        {
            return;
        }

        _handler.RemoveCallbacks(_runPrediction);
        _handler.PostDelayed(_runPrediction, DebounceMs);
    }

    public void Cleanup()
    {
        _disposed = true;
        _handler.RemoveCallbacks(_runPrediction);
        _localStore.Cleanup();
    }

    private void UpdateLocalModel(List<string> tokens)
    {
        if (tokens.Count == 0)
        {
            return;
        }

        if (!string.IsNullOrEmpty(_lastToken))
        {
            _localStore.RecordPair(_lastToken, tokens[0]);
        }

        for (int i = 0; i < tokens.Count - 1; i++)
        {
            _localStore.RecordPair(tokens[i], tokens[i + 1]);
        }
    }

    private void UpdateLastToken(List<string> tokens)
    {
        if (tokens.Count == 0)
        {
            return;
        }

        _lastToken = tokens[^1];
    }

    private void RunPrediction()
    {
        if (_disposed) return;

        if (!SettingsActivity.GetPredictionEnabled(_context))
        {
            _keyboardHandler.ClearCandidates();
            return;
        }

        string token;
        lock (_lock)
        {
            token = _pendingToken;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        var engine = _engineHost.CurrentEngine;
        if (engine != null && !string.IsNullOrEmpty(engine.GetComposingText()))
        {
            return;
        }

        int maxResults = Math.Max(1, SettingsActivity.GetCandidatePageSize(_context));
        var predictions = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        AppendDistinct(predictions, seen, _customStore.GetPredictions(token), maxResults);
        if (predictions.Count < maxResults)
        {
            AppendDistinct(predictions, seen, TryGetRimePredictions(maxResults), maxResults);
        }

        if (predictions.Count < maxResults)
        {
            AppendDistinct(predictions, seen, _localStore.PredictNext(token, maxResults), maxResults);
        }

        if (predictions.Count == 0)
        {
            _keyboardHandler.ClearCandidates();
            return;
        }

        _keyboardHandler.ShowPredictionCandidates(predictions, maxResults);
    }

    private List<string> TryGetRimePredictions(int maxResults)
    {
        var engine = _engineHost.CurrentEngine as RimeInputEngine;
        if (engine == null)
        {
            return new List<string>();
        }

        try
        {
            return engine.GetPredictionCandidates(maxResults);
        }
        catch (Exception ex)
        {
            Log.Warn("PredictionCoordinator", $"Rime prediction failed: {ex.Message}");
            return new List<string>();
        }
    }

    private static void AppendDistinct(List<string> target, HashSet<string> seen, List<string> source, int maxResults)
    {
        if (source == null || source.Count == 0)
        {
            return;
        }

        for (int i = 0; i < source.Count && target.Count < maxResults; i++)
        {
            string text = source[i];
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            if (seen.Add(text))
            {
                target.Add(text);
            }
        }
    }
}
