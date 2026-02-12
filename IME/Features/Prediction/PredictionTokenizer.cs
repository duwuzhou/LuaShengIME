using System;
using System.Collections.Generic;

namespace IME.Features.Prediction;

internal static class PredictionTokenizer
{
    public static List<string> ExtractTokens(string text)
    {
        var tokens = new List<string>();
        if (string.IsNullOrEmpty(text))
        {
            return tokens;
        }

        int i = 0;
        while (i < text.Length)
        {
            if (!IsTokenChar(text[i]))
            {
                i++;
                continue;
            }

            int start = i;
            while (i < text.Length && IsTokenChar(text[i]))
            {
                i++;
            }

            int length = i - start;
            if (length <= 0)
            {
                continue;
            }

            string token = NormalizeToken(text.Substring(start, length));
            if (!string.IsNullOrWhiteSpace(token))
            {
                tokens.Add(token);
            }
        }

        return tokens;
    }

    public static string NormalizeKey(string token)
    {
        return NormalizeToken(token);
    }

    private static string NormalizeToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return string.Empty;
        }

        if (token.Length > 32)
        {
            token = token.Substring(0, 32);
        }

        return token.ToLowerInvariant();
    }

    private static bool IsTokenChar(char c)
    {
        if (char.IsLetterOrDigit(c))
        {
            return true;
        }

        return IsCjk(c);
    }

    private static bool IsCjk(char c)
    {
        return (c >= '\u4E00' && c <= '\u9FFF')
            || (c >= '\u3400' && c <= '\u4DBF')
            || (c >= '\uF900' && c <= '\uFAFF');
    }
}
