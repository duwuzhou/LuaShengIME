using System.Collections.Generic;
using System.IO;
using System.Text;
using Android.Content;

namespace IME.Features.Input.Simulation;

internal readonly record struct PinyinLookupToken(string Text, string? Pinyin);

internal sealed class PinyinReverseLookup
{
    private readonly Dictionary<string, string> _wordToPinyin;
    private readonly int _maxWordLength;

    private PinyinReverseLookup(Dictionary<string, string> wordToPinyin, int maxWordLength)
    {
        _wordToPinyin = wordToPinyin;
        _maxWordLength = maxWordLength <= 0 ? 1 : maxWordLength;
    }

    public static async Task<PinyinReverseLookup> LoadAsync(Context context)
    {
        var wordToPinyin = new Dictionary<string, string>(StringComparer.Ordinal);
        int maxWordLength = 1;

        using Stream stream = context.Assets.Open("rime/luna_pinyin.dict.yaml");
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        bool inEntries = false;
        while (true)
        {
            string? line = await reader.ReadLineAsync().ConfigureAwait(false);
            if (line == null)
            {
                break;
            }

            if (!inEntries)
            {
                if (string.Equals(line.Trim(), "...", StringComparison.Ordinal))
                {
                    inEntries = true;
                }

                continue;
            }

            if (!TryParseEntry(line, out string word, out string pinyin))
            {
                continue;
            }

            if (wordToPinyin.ContainsKey(word))
            {
                continue;
            }

            wordToPinyin[word] = pinyin;
            if (word.Length > maxWordLength)
            {
                maxWordLength = word.Length;
            }
        }

        return new PinyinReverseLookup(wordToPinyin, maxWordLength);
    }

    public IReadOnlyList<PinyinLookupToken> Tokenize(string text)
    {
        var tokens = new List<PinyinLookupToken>();
        if (string.IsNullOrEmpty(text))
        {
            return tokens;
        }

        int index = 0;
        while (index < text.Length)
        {
            if (!IsCjk(text[index]))
            {
                int start = index;
                index++;
                while (index < text.Length && !IsCjk(text[index]))
                {
                    index++;
                }

                tokens.Add(new PinyinLookupToken(text.Substring(start, index - start), null));
                continue;
            }

            if (TryMatchLongest(text, index, out string word, out string pinyin))
            {
                tokens.Add(new PinyinLookupToken(word, pinyin));
                index += word.Length;
                continue;
            }

            tokens.Add(new PinyinLookupToken(text[index].ToString(), null));
            index++;
        }

        return tokens;
    }

    private bool TryMatchLongest(string text, int index, out string word, out string pinyin)
    {
        int remaining = text.Length - index;
        int maxLength = Math.Min(_maxWordLength, remaining);

        for (int length = maxLength; length >= 1; length--)
        {
            string candidate = text.Substring(index, length);
            if (_wordToPinyin.TryGetValue(candidate, out string? resolvedPinyin))
            {
                word = candidate;
                pinyin = resolvedPinyin;
                return true;
            }
        }

        word = string.Empty;
        pinyin = string.Empty;
        return false;
    }

    private static bool TryParseEntry(string line, out string word, out string pinyin)
    {
        word = string.Empty;
        pinyin = string.Empty;

        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        string trimmed = line.Trim();
        if (trimmed.Length == 0 || trimmed[0] == '#')
        {
            return false;
        }

        int separatorIndex = FindSeparatorIndex(trimmed);
        if (separatorIndex <= 0 || separatorIndex >= trimmed.Length - 1)
        {
            return false;
        }

        string rawWord = trimmed.Substring(0, separatorIndex).Trim();
        string rawPinyin = trimmed.Substring(separatorIndex).Trim();
        string normalizedPinyin = NormalizePinyin(rawPinyin);

        if (rawWord.Length == 0 || normalizedPinyin.Length == 0 || !ContainsCjk(rawWord))
        {
            return false;
        }

        word = rawWord;
        pinyin = normalizedPinyin;
        return true;
    }

    private static int FindSeparatorIndex(string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                return i;
            }
        }

        return -1;
    }

    private static string NormalizePinyin(string pinyin)
    {
        var builder = new StringBuilder(pinyin.Length);
        bool lastWasSeparator = false;

        for (int i = 0; i < pinyin.Length; i++)
        {
            char ch = char.ToLowerInvariant(pinyin[i]);
            if (ch >= 'a' && ch <= 'z')
            {
                builder.Append(ch);
                lastWasSeparator = false;
                continue;
            }

            if ((char.IsWhiteSpace(ch) || ch == '\'' || ch == '-')
                && builder.Length > 0
                && !lastWasSeparator)
            {
                builder.Append('\'');
                lastWasSeparator = true;
            }
        }

        if (builder.Length > 0 && builder[builder.Length - 1] == '\'')
        {
            builder.Length--;
        }

        return builder.ToString();
    }

    private static bool ContainsCjk(string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            if (IsCjk(text[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCjk(char ch)
    {
        return (ch >= '\u3400' && ch <= '\u4DBF')
               || (ch >= '\u4E00' && ch <= '\u9FFF')
               || (ch >= '\uF900' && ch <= '\uFAFF');
    }
}
