using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IME.Features.UserLexicon;

public static partial class UserLexiconTxt
{
    public static string Serialize(IEnumerable<UserLexiconEntry> entries, UserLexiconSerializeOptions? options = null)
    {
        options ??= new UserLexiconSerializeOptions();
        if (entries == null)
        {
            return string.Empty;
        }

        var grouped = entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Pinyin) && !string.IsNullOrWhiteSpace(entry.Word))
            .GroupBy(entry => entry.Pinyin.Trim(), StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal);

        var builder = new StringBuilder();

        foreach (var group in grouped)
        {
            var tokens = group
                .OrderByDescending(entry => entry.Frequency)
                .ThenBy(entry => entry.Word, StringComparer.Ordinal)
                .Select(entry => $"{entry.Word}:{Math.Max(1, entry.Frequency)}");

            builder
                .Append(group.Key)
                .Append('\t')
                .Append(string.Join(options.WordDelimiter.ToString(), tokens))
                .Append('\n');
        }

        return builder.ToString();
    }
}
