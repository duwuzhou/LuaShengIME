using System;
using System.Collections.Generic;
using Android.Content;
using Android.Database;
using Android.Database.Sqlite;

namespace IME.Features.UserLexicon;

public sealed class LocalUserLexiconStore : IUserLexiconStore, IDisposable
{
    private readonly UserLexiconDbHelper _dbHelper;

    public LocalUserLexiconStore(Context context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        _dbHelper = new UserLexiconDbHelper(context);
    }

    public UserLexiconUpsertResult Upsert(IEnumerable<UserLexiconEntry> entries)
    {
        var result = new UserLexiconUpsertResult();
        if (entries == null)
        {
            return result;
        }

        using SQLiteDatabase db = _dbHelper.WritableDatabase;
        db.BeginTransaction();

        try
        {
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Pinyin) || string.IsNullOrWhiteSpace(entry.Word))
                {
                    result.Skipped++;
                    continue;
                }

                int freq = Math.Max(1, entry.Frequency);
                long updatedAt = entry.UpdatedAtUtcMs > 0
                    ? entry.UpdatedAtUtcMs
                    : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                int? existingFrequency = TryGetFrequency(db, entry.Pinyin, entry.Word);
                if (existingFrequency.HasValue)
                {
                    if (existingFrequency.Value >= freq)
                    {
                        result.Skipped++;
                        continue;
                    }

                    var values = new ContentValues();
                    values.Put(UserLexiconDbHelper.ColFrequency, freq);
                    values.Put(UserLexiconDbHelper.ColUpdatedAt, updatedAt);

                    db.Update(
                        UserLexiconDbHelper.TableName,
                        values,
                        $"{UserLexiconDbHelper.ColPinyin}=? AND {UserLexiconDbHelper.ColWord}=?",
                        new[] { entry.Pinyin, entry.Word });

                    result.Updated++;
                }
                else
                {
                    var values = new ContentValues();
                    values.Put(UserLexiconDbHelper.ColPinyin, entry.Pinyin);
                    values.Put(UserLexiconDbHelper.ColWord, entry.Word);
                    values.Put(UserLexiconDbHelper.ColFrequency, freq);
                    values.Put(UserLexiconDbHelper.ColUpdatedAt, updatedAt);
                    db.Insert(UserLexiconDbHelper.TableName, null, values);

                    result.Added++;
                }
            }

            db.SetTransactionSuccessful();
        }
        finally
        {
            db.EndTransaction();
        }

        return result;
    }

    public IReadOnlyList<UserLexiconEntry> GetAll()
    {
        var entries = new List<UserLexiconEntry>();

        using SQLiteDatabase db = _dbHelper.ReadableDatabase;
        using ICursor cursor = db.Query(
            UserLexiconDbHelper.TableName,
            new[]
            {
                UserLexiconDbHelper.ColPinyin,
                UserLexiconDbHelper.ColWord,
                UserLexiconDbHelper.ColFrequency,
                UserLexiconDbHelper.ColUpdatedAt
            },
            null,
            null,
            null,
            null,
            $"{UserLexiconDbHelper.ColPinyin} ASC, {UserLexiconDbHelper.ColFrequency} DESC");

        while (cursor.MoveToNext())
        {
            string pinyin = cursor.GetString(0);
            string word = cursor.GetString(1);
            int frequency = cursor.GetInt(2);
            long updatedAt = cursor.GetLong(3);
            entries.Add(new UserLexiconEntry(pinyin, word, frequency, updatedAt));
        }

        return entries;
    }

    public IReadOnlyList<UserLexiconEntry> QueryByPinyinPrefix(string prefix, int limit)
    {
        var entries = new List<UserLexiconEntry>();
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return entries;
        }

        string normalized = NormalizePinyin(prefix);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return entries;
        }
        string like = normalized + "%";
        string limitClause = Math.Max(1, limit).ToString();

        using SQLiteDatabase db = _dbHelper.ReadableDatabase;
        string sql =
            "SELECT " +
            $"{UserLexiconDbHelper.ColPinyin}, {UserLexiconDbHelper.ColWord}, {UserLexiconDbHelper.ColFrequency}, {UserLexiconDbHelper.ColUpdatedAt} " +
            $"FROM {UserLexiconDbHelper.TableName} " +
            "WHERE REPLACE(REPLACE(LOWER(" + UserLexiconDbHelper.ColPinyin + "), ' ', ''), \"'\", '') LIKE ? " +
            $"ORDER BY {UserLexiconDbHelper.ColFrequency} DESC " +
            "LIMIT ?";

        using ICursor cursor = db.RawQuery(sql, new[] { like ?? string.Empty, limitClause ?? "10" });

        while (cursor.MoveToNext())
        {
            string pinyin = cursor.GetString(0);
            string word = cursor.GetString(1);
            int frequency = cursor.GetInt(2);
            long updatedAt = cursor.GetLong(3);
            entries.Add(new UserLexiconEntry(pinyin, word, frequency, updatedAt));
        }

        return entries;
    }

    private static string NormalizePinyin(string pinyin)
    {
        if (string.IsNullOrWhiteSpace(pinyin))
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder();
        foreach (char c in pinyin)
        {
            if (char.IsWhiteSpace(c) || c == '\'')
            {
                continue;
            }

            builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }

    public int Clear()
    {
        using SQLiteDatabase db = _dbHelper.WritableDatabase;
        return db.Delete(UserLexiconDbHelper.TableName, null, null);
    }

    public bool IncrementFrequency(string pinyin, string word, int delta = 1)
    {
        if (string.IsNullOrWhiteSpace(pinyin) || string.IsNullOrWhiteSpace(word))
        {
            return false;
        }

        int increment = Math.Max(1, delta);
        long updatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        using SQLiteDatabase db = _dbHelper.WritableDatabase;
        db.BeginTransaction();

        try
        {
            int? existingFrequency = TryGetFrequency(db, pinyin, word);
            int newFrequency = (existingFrequency ?? 0) + increment;

            var values = new ContentValues();
            values.Put(UserLexiconDbHelper.ColPinyin, pinyin);
            values.Put(UserLexiconDbHelper.ColWord, word);
            values.Put(UserLexiconDbHelper.ColFrequency, newFrequency);
            values.Put(UserLexiconDbHelper.ColUpdatedAt, updatedAt);

            if (existingFrequency.HasValue)
            {
                db.Update(
                    UserLexiconDbHelper.TableName,
                    values,
                    $"{UserLexiconDbHelper.ColPinyin}=? AND {UserLexiconDbHelper.ColWord}=?",
                    new[] { pinyin, word });
            }
            else
            {
                db.Insert(UserLexiconDbHelper.TableName, null, values);
            }

            db.SetTransactionSuccessful();
            return true;
        }
        finally
        {
            db.EndTransaction();
        }
    }

    public void Dispose()
    {
        _dbHelper.Close();
    }

    private static int? TryGetFrequency(SQLiteDatabase db, string pinyin, string word)
    {
        if (string.IsNullOrWhiteSpace(pinyin) || string.IsNullOrWhiteSpace(word))
        {
            return null;
        }

        using ICursor cursor = db.Query(
            UserLexiconDbHelper.TableName,
            new[] { UserLexiconDbHelper.ColFrequency },
            $"{UserLexiconDbHelper.ColPinyin}=? AND {UserLexiconDbHelper.ColWord}=?",
            new[] { pinyin, word },
            null,
            null,
            null,
            "1");

        if (cursor.MoveToFirst())
        {
            return cursor.GetInt(0);
        }

        return null;
    }

    private sealed class UserLexiconDbHelper : SQLiteOpenHelper
    {
        public const string TableName = "user_lexicon";
        public const string ColId = "_id";
        public const string ColPinyin = "pinyin";
        public const string ColWord = "word";
        public const string ColFrequency = "freq";
        public const string ColUpdatedAt = "updated_at";

        private const string DbName = "UserLexicon.db";
        private const int DbVersion = 1;

        public UserLexiconDbHelper(Context context)
            : base(context, DbName, null, DbVersion)
        {
        }

        public override void OnCreate(SQLiteDatabase db)
        {
            string createSql = "CREATE TABLE IF NOT EXISTS " + TableName + " (" +
                               ColId + " INTEGER PRIMARY KEY AUTOINCREMENT, " +
                               ColPinyin + " TEXT NOT NULL, " +
                               ColWord + " TEXT NOT NULL, " +
                               ColFrequency + " INTEGER NOT NULL, " +
                               ColUpdatedAt + " INTEGER NOT NULL, " +
                               "UNIQUE(" + ColPinyin + ", " + ColWord + "))";

            db.ExecSQL(createSql);
        }

        public override void OnUpgrade(SQLiteDatabase db, int oldVersion, int newVersion)
        {
        }
    }
}
