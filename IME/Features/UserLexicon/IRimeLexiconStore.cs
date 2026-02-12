namespace IME.Features.UserLexicon;

public sealed class RimeApplyResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
}

public interface IRimeLexiconStore : IUserLexiconStore
{
    RimeApplyResult ApplyChanges();
}
