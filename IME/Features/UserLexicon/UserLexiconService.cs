using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IME.Features.UserLexicon;

public sealed class UserLexiconImportResult
{
    public int ParsedEntries { get; init; }
    public int MergedEntries { get; init; }
    public UserLexiconUpsertResult LocalStoreResult { get; init; } = new();
    public UserLexiconUpsertResult? RimeStoreResult { get; init; }
    public RimeApplyResult? RimeApplyResult { get; init; }
    public IReadOnlyList<UserLexiconParseError> Errors { get; init; } = Array.Empty<UserLexiconParseError>();
}

public sealed class UserLexiconExportResult
{
    public int TotalEntries { get; init; }
}

public sealed class UserLexiconService
{
    private readonly IUserLexiconStore _localStore;
    private readonly IRimeLexiconStore? _rimeStore;

    public UserLexiconService(IUserLexiconStore localStore, IRimeLexiconStore? rimeStore = null)
    {
        _localStore = localStore ?? throw new ArgumentNullException(nameof(localStore));
        _rimeStore = rimeStore;
    }

    public Task<UserLexiconImportResult> ImportTxtAsync(Stream input, bool applyRimeChanges = false, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => ImportTxt(input, applyRimeChanges, cancellationToken), cancellationToken);
    }

    public UserLexiconImportResult ImportTxt(Stream input, bool applyRimeChanges = false, CancellationToken cancellationToken = default)
    {
        if (input == null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        using var reader = new StreamReader(input, Encoding.UTF8, true, 1024, leaveOpen: true);
        var parseResult = UserLexiconTxt.Parse(reader);
        cancellationToken.ThrowIfCancellationRequested();

        var merged = UserLexiconMerger.Merge(parseResult.Entries, sort: true);
        var localResult = _localStore.Upsert(merged);
        UserLexiconUpsertResult? rimeResult = null;
        RimeApplyResult? rimeApply = null;

        if (_rimeStore != null)
        {
            rimeResult = _rimeStore.Upsert(merged);
            if (applyRimeChanges)
            {
                rimeApply = _rimeStore.ApplyChanges();
            }
        }

        return new UserLexiconImportResult
        {
            ParsedEntries = parseResult.Entries.Count,
            MergedEntries = merged.Count,
            LocalStoreResult = localResult,
            RimeStoreResult = rimeResult,
            RimeApplyResult = rimeApply,
            Errors = parseResult.Errors
        };
    }

    public Task<UserLexiconExportResult> ExportTxtAsync(Stream output, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => ExportTxt(output, cancellationToken), cancellationToken);
    }

    public UserLexiconExportResult ExportTxt(Stream output, CancellationToken cancellationToken = default)
    {
        if (output == null)
        {
            throw new ArgumentNullException(nameof(output));
        }

        IEnumerable<UserLexiconEntry> entries = _localStore.GetAll();
        if (_rimeStore != null)
        {
            entries = entries.Concat(_rimeStore.GetAll());
        }

        var merged = UserLexiconMerger.Merge(entries, sort: true);
        cancellationToken.ThrowIfCancellationRequested();

        string content = UserLexiconTxt.Serialize(merged);
        using var writer = new StreamWriter(output, new UTF8Encoding(false), 1024, leaveOpen: true);
        writer.Write(content);
        writer.Flush();

        return new UserLexiconExportResult
        {
            TotalEntries = merged.Count
        };
    }
}
