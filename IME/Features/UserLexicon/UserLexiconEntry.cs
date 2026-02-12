namespace IME.Features.UserLexicon;

public sealed record UserLexiconEntry(
    string Pinyin,
    string Word,
    int Frequency,
    long UpdatedAtUtcMs = 0);
