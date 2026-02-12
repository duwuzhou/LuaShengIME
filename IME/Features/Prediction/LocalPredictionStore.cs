using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using Android.Content;
using Android.Util;

namespace IME.Features.Prediction;

internal sealed class LocalPredictionStore
{
    private const int MaxNextPerToken = 32;
    private static readonly TimeSpan SaveDelay = TimeSpan.FromSeconds(2);
    private readonly object _lock = new object();
    private readonly string _path;

    private Dictionary<string, Dictionary<string, int>> _data = new(StringComparer.Ordinal);
    private bool _loaded;
    private Timer? _saveTimer;

    public LocalPredictionStore(Context context)
    {
        string dir = Path.Combine(context.FilesDir.AbsolutePath, "prediction");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "prediction.json");
    }

    public void RecordPair(string previousToken, string nextToken)
    {
        if (string.IsNullOrWhiteSpace(previousToken) || string.IsNullOrWhiteSpace(nextToken))
        {
            return;
        }

        lock (_lock)
        {
            EnsureLoaded();
            if (!_data.TryGetValue(previousToken, out var nextMap))
            {
                nextMap = new Dictionary<string, int>(StringComparer.Ordinal);
                _data[previousToken] = nextMap;
            }

            if (!nextMap.TryGetValue(nextToken, out int count))
            {
                count = 0;
            }

            nextMap[nextToken] = count + 1;
            TrimNextMap(nextMap);
            ScheduleSaveLocked();
        }
    }

    public List<string> PredictNext(string previousToken, int maxResults)
    {
        if (string.IsNullOrWhiteSpace(previousToken) || maxResults <= 0)
        {
            return new List<string>();
        }

        lock (_lock)
        {
            EnsureLoaded();
            if (!_data.TryGetValue(previousToken, out var nextMap) || nextMap.Count == 0)
            {
                return new List<string>();
            }

            var list = new List<KeyValuePair<string, int>>(nextMap);
            list.Sort((a, b) =>
            {
                int cmp = b.Value.CompareTo(a.Value);
                return cmp != 0 ? cmp : string.CompareOrdinal(a.Key, b.Key);
            });

            int take = Math.Min(maxResults, list.Count);
            var results = new List<string>(take);
            for (int i = 0; i < take; i++)
            {
                results.Add(list[i].Key);
            }

            return results;
        }
    }

    public void Cleanup()
    {
        lock (_lock)
        {
            _saveTimer?.Dispose();
            _saveTimer = null;
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
            var data = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, int>>>(json);
            if (data != null)
            {
                _data = new Dictionary<string, Dictionary<string, int>>(data, StringComparer.Ordinal);
            }
        }
        catch (Exception ex)
        {
            Log.Warn("LocalPredictionStore", $"Load failed: {ex.Message}");
        }
    }

    private void ScheduleSaveLocked()
    {
        _saveTimer?.Dispose();
        _saveTimer = new Timer(_ => Save(), null, SaveDelay, Timeout.InfiniteTimeSpan);
    }

    private void Save()
    {
        Dictionary<string, Dictionary<string, int>> snapshot;
        lock (_lock)
        {
            if (!_loaded)
            {
                return;
            }

            snapshot = new Dictionary<string, Dictionary<string, int>>(_data, StringComparer.Ordinal);
        }

        try
        {
            string json = JsonSerializer.Serialize(snapshot);
            string tmpPath = _path + ".tmp";
            File.WriteAllText(tmpPath, json, new UTF8Encoding(false));
            File.Move(tmpPath, _path, overwrite: true);
        }
        catch (Exception ex)
        {
            Log.Warn("LocalPredictionStore", $"Save failed: {ex.Message}");
        }
    }

    private static void TrimNextMap(Dictionary<string, int> nextMap)
    {
        if (nextMap.Count <= MaxNextPerToken)
        {
            return;
        }

        var list = new List<KeyValuePair<string, int>>(nextMap);
        list.Sort((a, b) =>
        {
            int cmp = b.Value.CompareTo(a.Value);
            return cmp != 0 ? cmp : string.CompareOrdinal(a.Key, b.Key);
        });

        nextMap.Clear();
        int limit = Math.Min(MaxNextPerToken, list.Count);
        for (int i = 0; i < limit; i++)
        {
            nextMap[list[i].Key] = list[i].Value;
        }
    }
}
