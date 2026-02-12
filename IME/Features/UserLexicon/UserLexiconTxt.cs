using System;
using System.Collections.Generic;

namespace IME.Features.UserLexicon;

public sealed class UserLexiconParseError
{
    public UserLexiconParseError(int lineNumber, string message, string rawLine)
    {
        LineNumber = lineNumber;
        Message = message;
        RawLine = rawLine;
    }

    public int LineNumber { get; }
    public string Message { get; }
    public string RawLine { get; }
}

public sealed class UserLexiconParseResult
{
    public List<UserLexiconEntry> Entries { get; } = new();
    public List<UserLexiconParseError> Errors { get; } = new();
}

public sealed class UserLexiconParseOptions
{
    public char WordDelimiter { get; init; } = '|';
    public string CommentPrefix { get; init; } = "#";
    public int DefaultSingleFrequency { get; init; } = 1;
    public int DefaultFrequencyStart { get; init; } = 100;
    public int DefaultFrequencyStep { get; init; } = 10;
}

public sealed class UserLexiconSerializeOptions
{
    public char WordDelimiter { get; init; } = '|';
}

public static partial class UserLexiconTxt
{
}
