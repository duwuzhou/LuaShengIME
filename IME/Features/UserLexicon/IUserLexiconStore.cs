using System.Collections.Generic;

namespace IME.Features.UserLexicon;

public sealed class UserLexiconUpsertResult
{
    public int Added { get; internal set; }
    public int Updated { get; internal set; }
    public int Skipped { get; internal set; }
}

public interface IUserLexiconStore
{
    UserLexiconUpsertResult Upsert(IEnumerable<UserLexiconEntry> entries);
    IReadOnlyList<UserLexiconEntry> GetAll();
    int Clear();
}
