using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Android.Content;
using IME.Shared.InputEngine;

namespace IME.Features.UserLexicon;

public sealed class RimeUserLexiconStore : IRimeLexiconStore
{
    private const string DictionaryName = "custom_phrase";
    private const string DictionaryFileName = "custom_phrase.dict.yaml";
    private const string SchemaPatchFileName = "luna_pinyin.custom.yaml";

    private readonly Context _context;

    public RimeUserLexiconStore(Context context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public UserLexiconUpsertResult Upsert(IEnumerable<UserLexiconEntry> entries)
    {
        var result = new UserLexiconUpsertResult();
        if (entries == null)
        {
            return result;
        }

        var existing = GetAll().ToList();
        var merged = UserLexiconMerger.Merge(existing.Concat(entries), sort: false);
        WriteDictionary(merged);
        EnsureSchemaPatch(_context);

        result.Added = merged.Count;
        return result;
    }

    public IReadOnlyList<UserLexiconEntry> GetAll()
    {
        string dictPath = GetDictionaryPath();
        if (!File.Exists(dictPath))
        {
            return Array.Empty<UserLexiconEntry>();
        }

        var entries = new List<UserLexiconEntry>();
        bool inBody = false;

        foreach (string rawLine in File.ReadLines(dictPath, Encoding.UTF8))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            if (!inBody)
            {
                if (line == "...")
                {
                    inBody = true;
                }
                continue;
            }

            string[] parts = line.Split('\t');
            if (parts.Length < 2)
            {
                parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            }

            if (parts.Length < 2)
            {
                continue;
            }

            string word = parts[0].Trim();
            string pinyin = parts[1].Trim();
            int freq = 1;
            if (parts.Length >= 3)
            {
                int.TryParse(parts[2], out freq);
            }

            if (!string.IsNullOrEmpty(word) && !string.IsNullOrEmpty(pinyin))
            {
                entries.Add(new UserLexiconEntry(pinyin, word, Math.Max(1, freq)));
            }
        }

        return entries;
    }

    public int Clear()
    {
        int removed = 0;
        string dictPath = GetDictionaryPath();
        if (File.Exists(dictPath))
        {
            File.Delete(dictPath);
            removed++;
        }
        return removed;
    }

    public RimeApplyResult ApplyChanges()
    {
        return RimeMaintenanceRunner.DeployAndSync(_context);
    }

    private string GetDictionaryPath()
    {
        return Path.Combine(RimeUserDataPaths.EnsureUserDir(_context), DictionaryFileName);
    }

    private void WriteDictionary(List<UserLexiconEntry> entries)
    {
        string dictPath = GetDictionaryPath();
        var builder = new StringBuilder();
        builder.AppendLine("# Rime dictionary");
        builder.AppendLine("# encoding: utf-8");
        builder.AppendLine("---");
        builder.AppendLine($"name: {DictionaryName}");
        builder.AppendLine("version: \"1.0\"");
        builder.AppendLine("sort: by_weight");
        builder.AppendLine("use_preset_vocabulary: false");
        builder.AppendLine("...");

        foreach (var entry in entries.OrderBy(e => e.Pinyin, StringComparer.Ordinal)
                                     .ThenByDescending(e => e.Frequency)
                                     .ThenBy(e => e.Word, StringComparer.Ordinal))
        {
            builder.Append(entry.Word)
                   .Append('\t')
                   .Append(entry.Pinyin)
                   .Append('\t')
                   .Append(Math.Max(1, entry.Frequency))
                   .Append('\n');
        }

        File.WriteAllText(dictPath, builder.ToString(), new UTF8Encoding(false));
    }

    internal static void EnsureSchemaPatch(Context context)
    {
        string userDir = RimeUserDataPaths.GetDefaultUserDir(context);
        string patchPath = Path.Combine(userDir, SchemaPatchFileName);
        string content = File.Exists(patchPath)
            ? File.ReadAllText(patchPath, Encoding.UTF8)
            : string.Empty;

        string updated = UpsertPatch(content);
        if (!string.Equals(content, updated, StringComparison.Ordinal))
        {
            File.WriteAllText(patchPath, updated, new UTF8Encoding(false));
        }
    }

    private static string UpsertPatch(string existing)
    {
        if (string.IsNullOrEmpty(existing))
        {
            return "patch:\n  pinyin/enable_user_dict: true\n  pinyin/extra_tables:\n    - custom_phrase\n";
        }

        string normalized = existing.Replace("\r\n", "\n");
        var lines = new List<string>(normalized.Split('\n'));
        int patchIndex = -1;
        int enableIndex = -1;
        int extraIndex = -1;
        int customIndex = -1;

        for (int i = 0; i < lines.Count; i++)
        {
            string trimmed = lines[i].TrimStart();
            if (trimmed.StartsWith("patch:", StringComparison.Ordinal))
            {
                patchIndex = i;
            }
            if (trimmed.StartsWith("pinyin/enable_user_dict:", StringComparison.Ordinal))
            {
                enableIndex = i;
            }
            if (trimmed.StartsWith("pinyin/extra_tables:", StringComparison.Ordinal))
            {
                extraIndex = i;
            }
            if (trimmed.StartsWith("- custom_phrase", StringComparison.Ordinal))
            {
                customIndex = i;
            }
        }

        if (patchIndex < 0)
        {
            if (lines.Count > 0 && lines[^1].Length != 0)
                lines.Add(string.Empty);
            lines.Add("patch:");
            patchIndex = lines.Count - 1;
        }

        if (enableIndex < 0)
        {
            lines.Insert(patchIndex + 1, "  pinyin/enable_user_dict: true");
            if (extraIndex >= patchIndex + 1)
                extraIndex++;
            if (customIndex >= patchIndex + 1)
                customIndex++;
        }

        if (extraIndex < 0)
        {
            int insertAt = patchIndex + 2;
            lines.Insert(insertAt, "  pinyin/extra_tables:");
            lines.Insert(insertAt + 1, "    - custom_phrase");
        }
        else if (customIndex < 0)
        {
            lines.Insert(extraIndex + 1, "    - custom_phrase");
        }

        return string.Join("\n", lines).TrimEnd() + "\n";
    }
}

