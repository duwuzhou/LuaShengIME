using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace IME.Shared.InputEngine
{
    internal sealed class OpenccSimplifier
    {
        private sealed class Node
        {
            public readonly Dictionary<char, Node> Next = new Dictionary<char, Node>();
            public string? Value;
        }

        private readonly Node _root = new Node();
        private readonly Dictionary<char, string> _charMap = new Dictionary<char, string>();

        public static OpenccSimplifier? TryLoad(string openccDir, Action<string>? logWarn = null)
        {
            if (string.IsNullOrEmpty(openccDir))
            {
                logWarn?.Invoke("OpenCC dir is empty");
                return null;
            }

            string phrasesPath = Path.Combine(openccDir, "TSPhrases.txt");
            string charsPath = Path.Combine(openccDir, "TSCharacters.txt");

            if (!File.Exists(phrasesPath) || !File.Exists(charsPath))
            {
                logWarn?.Invoke($"OpenCC files missing: phrases='{phrasesPath}', chars='{charsPath}'");
                return null;
            }

            try
            {
                var simplifier = new OpenccSimplifier();
                simplifier.LoadPhrases(phrasesPath);
                simplifier.LoadChars(charsPath);
                return simplifier;
            }
            catch (Exception ex)
            {
                logWarn?.Invoke($"OpenCC load failed: {ex.Message}");
                return null;
            }
        }

        public string Convert(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            var sb = new StringBuilder(input.Length);
            int i = 0;

            while (i < input.Length)
            {
                string? bestValue = null;
                int bestLen = 0;
                var node = _root;

                for (int j = i; j < input.Length; j++)
                {
                    if (!node.Next.TryGetValue(input[j], out node))
                        break;

                    if (node.Value != null)
                    {
                        bestValue = node.Value;
                        bestLen = j - i + 1;
                    }
                }

                if (bestLen > 0 && bestValue != null)
                {
                    sb.Append(bestValue);
                    i += bestLen;
                    continue;
                }

                char c = input[i];
                if (_charMap.TryGetValue(c, out var mapped))
                {
                    sb.Append(mapped);
                }
                else
                {
                    sb.Append(c);
                }

                i++;
            }

            return sb.ToString();
        }

        private void LoadPhrases(string phrasesPath)
        {
            using var stream = File.OpenRead(phrasesPath);
            using var reader = new StreamReader(stream, Encoding.UTF8, true);
            string? line;

            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line) || line[0] == '#')
                    continue;

                if (!TryParseLine(line, out var key, out var value))
                    continue;

                if (key.Length <= 1)
                    continue;

                AddPhrase(key, value);
            }
        }

        private void LoadChars(string charsPath)
        {
            using var stream = File.OpenRead(charsPath);
            using var reader = new StreamReader(stream, Encoding.UTF8, true);
            string? line;

            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line) || line[0] == '#')
                    continue;

                if (!TryParseLine(line, out var key, out var value))
                    continue;

                if (key.Length != 1)
                    continue;

                _charMap[key[0]] = value;
            }
        }

        private void AddPhrase(string key, string value)
        {
            var node = _root;
            foreach (char c in key)
            {
                if (!node.Next.TryGetValue(c, out var next))
                {
                    next = new Node();
                    node.Next[c] = next;
                }
                node = next;
            }
            node.Value = value;
        }

        private static bool TryParseLine(string line, out string key, out string value)
        {
            key = string.Empty;
            value = string.Empty;

            int tab = line.IndexOf('\t');
            if (tab <= 0 || tab >= line.Length - 1)
                return false;

            key = line.Substring(0, tab);
            string valuePart = line.Substring(tab + 1).Trim();
            if (valuePart.Length == 0)
                return false;

            int space = valuePart.IndexOf(' ');
            value = space > 0 ? valuePart.Substring(0, space) : valuePart;
            return value.Length > 0;
        }
    }
}
