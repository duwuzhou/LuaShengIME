using System;
using System.Collections.Generic;
using System.Linq;

namespace IME.Features.UserLexicon;

internal static class UserLexiconMerger
{
    internal static List<UserLexiconEntry> Merge(IEnumerable<UserLexiconEntry> entries, bool sort)
    {
        var map = new Dictionary<string, UserLexiconEntry>(StringComparer.Ordinal);
        if (entries == null)
        {
            return new List<UserLexiconEntry>();
        }

        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        foreach (var entry in entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Pinyin) || string.IsNullOrWhiteSpace(entry.Word))
            {
                continue;
            }

            string pinyin = entry.Pinyin.Trim();
            string word = entry.Word.Trim();
            int freq = Math.Max(1, entry.Frequency);
            long updatedAt = entry.UpdatedAtUtcMs > 0 ? entry.UpdatedAtUtcMs : now;

            string key = $"{pinyin}\t{word}";
            if (map.TryGetValue(key, out var existing))
            {
                if (freq > existing.Frequency)
                {
                    map[key] = new UserLexiconEntry(pinyin, word, freq, updatedAt);
                }
                continue;
            }

            map[key] = new UserLexiconEntry(pinyin, word, freq, updatedAt);
        }

        var merged = map.Values;
        if (!sort)
        {
            return merged.ToList();
        }

        return merged
            .OrderBy(entry => entry.Pinyin, StringComparer.Ordinal)
            .ThenByDescending(entry => entry.Frequency)
            .ThenBy(entry => entry.Word, StringComparer.Ordinal)
            .ToList();
    }
}
