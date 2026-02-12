using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Android.Content;
using Android.Util;

namespace IME.Features.Prediction;

internal sealed class CustomPredictionStore
{
    private readonly object _lock = new object();
    private readonly string _path;
    private Dictionary<string, List<string>> _data = new(StringComparer.Ordinal);
    private bool _loaded;

    public CustomPredictionStore(Context context)
    {
        string dir = Path.Combine(context.FilesDir.AbsolutePath, "prediction");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "custom_predictions.json");
    }

    public List<string> GetPredictions(string previousToken)
    {
        string key = PredictionTokenizer.NormalizeKey(previousToken);
        if (string.IsNullOrWhiteSpace(key))
        {
            return new List<string>();
        }

        lock (_lock)
        {
            EnsureLoaded();
            if (_data.TryGetValue(key, out var list))
            {
                return new List<string>(list);
            }
        }

        return new List<string>();
    }

    public List<PredictionPair> GetAllPairs()
    {
        var results = new List<PredictionPair>();
        lock (_lock)
        {
            EnsureLoaded();
            foreach (var kvp in _data)
            {
                string prev = kvp.Key;
                foreach (string next in kvp.Value)
                {
                    results.Add(new PredictionPair(prev, next));
                }
            }
        }

        return results;
    }

    public bool Add(string previousToken, string nextToken)
    {
        string prev = PredictionTokenizer.NormalizeKey(previousToken);
        string next = PredictionTokenizer.NormalizeKey(nextToken);
        if (string.IsNullOrWhiteSpace(prev) || string.IsNullOrWhiteSpace(next))
        {
            return false;
        }

        lock (_lock)
        {
            EnsureLoaded();
            if (!_data.TryGetValue(prev, out var list))
            {
                list = new List<string>();
                _data[prev] = list;
            }

            if (list.Contains(next))
            {
                return false;
            }

            list.Add(next);
            SaveLocked();
            return true;
        }
    }

    public bool Remove(string previousToken, string nextToken)
    {
        string prev = PredictionTokenizer.NormalizeKey(previousToken);
        string next = PredictionTokenizer.NormalizeKey(nextToken);
        if (string.IsNullOrWhiteSpace(prev) || string.IsNullOrWhiteSpace(next))
        {
            return false;
        }

        lock (_lock)
        {
            EnsureLoaded();
            if (!_data.TryGetValue(prev, out var list))
            {
                return false;
            }

            bool removed = list.Remove(next);
            if (list.Count == 0)
            {
                _data.Remove(prev);
            }

            if (removed)
            {
                SaveLocked();
            }

            return removed;
        }
    }

    public int Import(IEnumerable<PredictionPair> pairs)
    {
        int added = 0;
        foreach (var pair in pairs)
        {
            if (Add(pair.Previous, pair.Next))
            {
                added++;
            }
        }
        return added;
    }

    public void ReplaceAll(IEnumerable<PredictionPair> pairs)
    {
        lock (_lock)
        {
            EnsureLoaded();
            _data.Clear();
            foreach (var pair in pairs)
            {
                string prev = PredictionTokenizer.NormalizeKey(pair.Previous);
                string next = PredictionTokenizer.NormalizeKey(pair.Next);
                if (string.IsNullOrWhiteSpace(prev) || string.IsNullOrWhiteSpace(next))
                {
                    continue;
                }

                if (!_data.TryGetValue(prev, out var list))
                {
                    list = new List<string>();
                    _data[prev] = list;
                }

                if (!list.Contains(next))
                {
                    list.Add(next);
                }
            }

            SaveLocked();
        }
    }

    private void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        if (!File.Exists(_path))
        {
            return;
        }

        try
        {
            string json = File.ReadAllText(_path, Encoding.UTF8);
            var data = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json);
            if (data != null)
            {
                _data = new Dictionary<string, List<string>>(data, StringComparer.Ordinal);
            }
        }
        catch (Exception ex)
        {
            Log.Warn("CustomPredictionStore", $"Load failed: {ex.Message}");
        }
    }

    private void SaveLocked()
    {
        try
        {
            string json = JsonSerializer.Serialize(_data);
            File.WriteAllText(_path, json, new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            Log.Warn("CustomPredictionStore", $"Save failed: {ex.Message}");
        }
    }
}
