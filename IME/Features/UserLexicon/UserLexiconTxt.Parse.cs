using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IME.Features.UserLexicon;

public static partial class UserLexiconTxt
{
    public static UserLexiconParseResult Parse(TextReader reader, UserLexiconParseOptions? options = null)
    {
        if (reader == null)
        {
            throw new ArgumentNullException(nameof(reader));
        }

        options ??= new UserLexiconParseOptions();
        var result = new UserLexiconParseResult();
        string? line;
        int lineNumber = 0;

        while ((line = reader.ReadLine()) != null)
        {
            lineNumber++;
            ParseLine(line, lineNumber, options, result);
        }

        return result;
    }

    private static void ParseLine(
        string rawLine,
        int lineNumber,
        UserLexiconParseOptions options,
        UserLexiconParseResult result)
    {
        string trimmed = rawLine.Trim();
        if (trimmed.Length == 0)
        {
            return;
        }

        if (!string.IsNullOrEmpty(options.CommentPrefix) &&
            trimmed.StartsWith(options.CommentPrefix, StringComparison.Ordinal))
        {
            return;
        }

        if (TryParseWordFirstLine(trimmed, lineNumber, result))
        {
            return;
        }

        if (!TrySplitPinyinAndWords(trimmed, out string pinyin, out string wordsPart))
        {
            result.Errors.Add(new UserLexiconParseError(lineNumber, "Missing separator between pinyin and words.", rawLine));
            return;
        }

        if (string.IsNullOrWhiteSpace(pinyin) || string.IsNullOrWhiteSpace(wordsPart))
        {
            result.Errors.Add(new UserLexiconParseError(lineNumber, "Missing pinyin or word section.", rawLine));
            return;
        }

        ParseWordsPart(pinyin, wordsPart, rawLine, lineNumber, options, result);
    }

    private static bool TrySplitPinyinAndWords(string line, out string pinyin, out string wordsPart)
    {
        pinyin = string.Empty;
        wordsPart = string.Empty;

        int tabIndex = line.IndexOf('\t');
        if (tabIndex > 0 && tabIndex < line.Length - 1)
        {
            pinyin = line.Substring(0, tabIndex).Trim();
            wordsPart = line.Substring(tabIndex + 1).Trim();
            return true;
        }

        if (TrySplitByMultiSpace(line, out string left, out string right))
        {
            pinyin = left.Trim();
            wordsPart = right.Trim();
            return true;
        }

        string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            pinyin = parts[0].Trim();
            wordsPart = string.Join(' ', parts.Skip(1)).Trim();
            return true;
        }

        return false;
    }

    private static bool TrySplitByMultiSpace(string line, out string left, out string right)
    {
        left = string.Empty;
        right = string.Empty;
        int count = 0;

        for (int i = 0; i < line.Length; i++)
        {
            if (char.IsWhiteSpace(line[i]))
            {
                count++;
                continue;
            }

            if (count >= 2)
            {
                int splitIndex = i - count;
                left = line.Substring(0, splitIndex);
                right = line.Substring(i);
                return true;
            }

            count = 0;
        }

        return false;
    }

    private static void ParseWordsPart(
        string pinyin,
        string wordsPart,
        string rawLine,
        int lineNumber,
        UserLexiconParseOptions options,
        UserLexiconParseResult result)
    {
        var tokens = wordsPart.Split(options.WordDelimiter, StringSplitOptions.RemoveEmptyEntries);
        var items = new List<(string Word, int? Frequency)>();

        if (tokens.Length == 1)
        {
            string single = tokens[0].Trim();
            if (!TryParseToken(single, out string word, out int frequency, out bool hasFrequency))
            {
                result.Errors.Add(new UserLexiconParseError(lineNumber, "Invalid word token.", rawLine));
                return;
            }

            if (!hasFrequency)
            {
                var parts = single.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && !HasExplicitFrequency(single))
                {
                    items.AddRange(ParseWordFrequencyPairs(parts));
                }
                else
                {
                    items.Add((word, hasFrequency ? frequency : null));
                }
            }
            else
            {
                items.Add((word, frequency));
            }
        }
        else
        {
            foreach (string token in tokens)
            {
                string tokenTrimmed = token.Trim();
                if (tokenTrimmed.Length == 0)
                {
                    result.Errors.Add(new UserLexiconParseError(lineNumber, "Empty word token.", rawLine));
                    continue;
                }

                if (!TryParseToken(tokenTrimmed, out string word, out int frequency, out bool hasFrequency))
                {
                    result.Errors.Add(new UserLexiconParseError(lineNumber, "Invalid word token.", rawLine));
                    continue;
                }

                items.Add((word, hasFrequency ? frequency : null));
            }
        }

        ApplyFrequenciesAndAdd(pinyin, items, lineNumber, rawLine, options, result);
    }

    private static List<(string Word, int? Frequency)> ParseWordFrequencyPairs(string[] parts)
    {
        var items = new List<(string Word, int? Frequency)>();
        int index = 0;

        while (index < parts.Length)
        {
            string word = parts[index].Trim();
            if (word.Length == 0)
            {
                index++;
                continue;
            }

            int? frequency = null;
            if (index + 1 < parts.Length && int.TryParse(parts[index + 1], out int parsed) && parsed > 0)
            {
                frequency = parsed;
                index += 2;
            }
            else
            {
                index += 1;
            }

            items.Add((word, frequency));
        }

        return items;
    }

    private static void ApplyFrequenciesAndAdd(
        string pinyin,
        List<(string Word, int? Frequency)> items,
        int lineNumber,
        string rawLine,
        UserLexiconParseOptions options,
        UserLexiconParseResult result)
    {
        if (items.Count == 0)
        {
            result.Errors.Add(new UserLexiconParseError(lineNumber, "No word tokens found.", rawLine));
            return;
        }

        bool singleNoFrequency = items.Count == 1 && !items[0].Frequency.HasValue;
        int missingIndex = 0;

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Word))
            {
                result.Errors.Add(new UserLexiconParseError(lineNumber, "Empty word token.", rawLine));
                continue;
            }

            int frequency = item.Frequency ?? (singleNoFrequency
                ? options.DefaultSingleFrequency
                : Math.Max(1, options.DefaultFrequencyStart - (missingIndex * options.DefaultFrequencyStep)));

            if (!item.Frequency.HasValue)
            {
                missingIndex++;
            }

            result.Entries.Add(new UserLexiconEntry(pinyin, item.Word.Trim(), Math.Max(1, frequency)));
        }
    }

    private static bool TryParseWordFirstLine(
        string line,
        int lineNumber,
        UserLexiconParseResult result)
    {
        string[] parts = line.Split('\t');
        if (parts.Length < 2)
        {
            return false;
        }

        string left = parts[0].Trim();
        string right = parts[1].Trim();

        if (!IsLikelyWord(left) || !IsLikelyPinyin(right))
        {
            return false;
        }

        int freq = 1;
        if (parts.Length >= 3)
        {
            int.TryParse(parts[2].Trim(), out freq);
        }

        result.Entries.Add(new UserLexiconEntry(right, left, Math.Max(1, freq)));
        return true;
    }

    private static bool IsLikelyWord(string text)
    {
        foreach (char c in text)
        {
            if (c >= 0x4E00 && c <= 0x9FFF)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsLikelyPinyin(string text)
    {
        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                continue;
            }

            if ((c >= 'a' && c <= 'z') ||
                (c >= 'A' && c <= 'Z') ||
                (c >= '0' && c <= '9') ||
                c == '\'' || c == '-')
            {
                continue;
            }

            return false;
        }

        return text.Length > 0;
    }

    private static bool HasExplicitFrequency(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        int colonIndex = token.LastIndexOf(':');
        if (colonIndex <= 0 || colonIndex >= token.Length - 1)
        {
            return false;
        }

        string freqText = token[(colonIndex + 1)..].Trim();
        return int.TryParse(freqText, out int freq) && freq > 0;
    }

    private static bool TryParseToken(string token, out string word, out int frequency, out bool hasFrequency)
    {
        word = string.Empty;
        frequency = 0;
        hasFrequency = false;

        int colonIndex = token.LastIndexOf(':');
        if (colonIndex > 0 && colonIndex < token.Length - 1)
        {
            string wordPart = token[..colonIndex].Trim();
            string freqText = token[(colonIndex + 1)..].Trim();

            if (!string.IsNullOrEmpty(wordPart) &&
                int.TryParse(freqText, out int parsed) &&
                parsed > 0)
            {
                word = wordPart;
                frequency = parsed;
                hasFrequency = true;
                return true;
            }
        }

        word = token.Trim();
        return !string.IsNullOrEmpty(word);
    }
}
