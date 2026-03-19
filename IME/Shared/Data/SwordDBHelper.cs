using System;
using System.Collections.Generic;
using Android.Content;
using Android.Database;
using Android.Database.Sqlite;
using Android.Util;
using IME.Shared.Models;
using IME.Shared.Utils.FileOperation;
using Newtonsoft.Json;

namespace IME.Shared.Data;

public class SwordDBHelper : SQLiteOpenHelper
{
    private static readonly object _lock = new object();

    private const string DbName = "SwordDB.db";
    private const int DbVersion = 2;

    public const string SwordTableName = "SwordTable";
    private const string TableItems = "items";

    private const string ColCatId = "_id";
    private const string ColCatName = "name";
    private const string ColCatBuiltin = "is_builtin";

    private const string ColItemId = "_id";
    private const string ColItemCatId = "category_id";
    private const string ColItemName = "item_name";
    private const string ColItemBuiltin = "is_builtin";

    public SwordDBHelper(Context context)
        : base(context, DbName, null, DbVersion)
    {
    }

    public override void OnCreate(SQLiteDatabase db)
    {
        lock (_lock)
        {
            Log.Info("SwordDBHelper", "Create database tables.");
            EnsureTables(db);
            EnsureSeedData(db);
        }
    }

    public override void OnOpen(SQLiteDatabase db)
    {
        base.OnOpen(db);

        lock (_lock)
        {
            try
            {
                EnsureTables(db);
                EnsureSeedData(db);
            }
            catch (Exception ex)
            {
                Log.Error("SwordDBHelper", $"Open database failed: {ex.Message}");
            }
        }
    }

    public override void OnUpgrade(SQLiteDatabase db, int oldVersion, int newVersion)
    {
        lock (_lock)
        {
            EnsureTables(db);
            EnsureSeedData(db);
        }
    }

    private void EnsureTables(SQLiteDatabase db)
    {
        if (!TableExists(db, SwordTableName))
        {
            string createCategoriesTable =
                "CREATE TABLE " + SwordTableName + " (" +
                ColCatId + " INTEGER PRIMARY KEY AUTOINCREMENT, " +
                ColCatName + " TEXT UNIQUE, " +
                ColCatBuiltin + " INTEGER NOT NULL DEFAULT 0)";

            db.ExecSQL(createCategoriesTable);
        }
        else
        {
            EnsureColumnExists(db, SwordTableName, ColCatBuiltin, "INTEGER NOT NULL DEFAULT 0");
        }

        if (!TableExists(db, TableItems))
        {
            string createItemsTable =
                "CREATE TABLE " + TableItems + " (" +
                ColItemId + " INTEGER PRIMARY KEY AUTOINCREMENT, " +
                ColItemCatId + " INTEGER, " +
                ColItemName + " TEXT UNIQUE, " +
                ColItemBuiltin + " INTEGER NOT NULL DEFAULT 0, " +
                "FOREIGN KEY(" + ColItemCatId + ") REFERENCES " + SwordTableName + "(" + ColCatId + "))";

            db.ExecSQL(createItemsTable);
        }
        else
        {
            EnsureColumnExists(db, TableItems, ColItemBuiltin, "INTEGER NOT NULL DEFAULT 0");
        }
    }

    private static bool TableExists(SQLiteDatabase db, string tableName)
    {
        using ICursor cursor = db.RawQuery(
            "SELECT name FROM sqlite_master WHERE type='table' AND name=?",
            new[] { tableName });

        return cursor.MoveToFirst();
    }

    private static void EnsureColumnExists(SQLiteDatabase db, string tableName, string columnName, string columnDefinition)
    {
        if (ColumnExists(db, tableName, columnName))
        {
            return;
        }

        db.ExecSQL($"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition}");
    }

    private static bool ColumnExists(SQLiteDatabase db, string tableName, string columnName)
    {
        using ICursor cursor = db.RawQuery($"PRAGMA table_info({tableName})", null);
        while (cursor.MoveToNext())
        {
            string currentName = cursor.GetString(1);
            if (string.Equals(currentName, columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void EnsureSeedData(SQLiteDatabase db)
    {
        List<SdDataModel> seedData = LoadSeedData();
        if (seedData.Count == 0)
        {
            return;
        }

        db.BeginTransaction();
        try
        {
            db.ExecSQL($"UPDATE {SwordTableName} SET {ColCatBuiltin}=0");
            db.ExecSQL($"UPDATE {TableItems} SET {ColItemBuiltin}=0");

            foreach (var category in seedData)
            {
                string categoryName = NormalizeInput(category.Name);
                if (string.IsNullOrEmpty(categoryName))
                {
                    continue;
                }

                long categoryId = EnsureCategoryId(db, categoryName, isBuiltin: true);
                foreach (string item in category.Data ?? new List<string>())
                {
                    EnsureItem(db, categoryId, item, isBuiltin: true);
                }
            }

            db.SetTransactionSuccessful();
        }
        catch (Exception ex)
        {
            Log.Error("SwordDBHelper", $"Ensure seed data failed: {ex.Message}");
            throw;
        }
        finally
        {
            db.EndTransaction();
        }
    }

    private static List<SdDataModel> LoadSeedData()
    {
        try
        {
            var fileReadOperation = new FileReadOperation();
            string json = fileReadOperation.ReadJsonFile(Application.Context, Resource.Raw.sd);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<SdDataModel>();
            }

            return JsonConvert.DeserializeObject<List<SdDataModel>>(json) ?? new List<SdDataModel>();
        }
        catch (Exception ex)
        {
            Log.Error("SwordDBHelper", $"Load seed data failed: {ex.Message}");
            return new List<SdDataModel>();
        }
    }

    public bool AddCategory(string categoryName)
    {
        lock (_lock)
        {
            string normalized = NormalizeInput(categoryName);
            if (string.IsNullOrEmpty(normalized))
            {
                return false;
            }

            using SQLiteDatabase db = WritableDatabase;
            using ICursor existing = db.Query(SwordTableName, new[] { ColCatId }, ColCatName + " = ?", new[] { normalized }, null, null, null);
            if (existing.MoveToFirst())
            {
                return false;
            }

            var values = new ContentValues();
            values.Put(ColCatName, normalized);
            values.Put(ColCatBuiltin, 0);
            return db.Insert(SwordTableName, null, values) > 0;
        }
    }

    public bool AddItem(string categoryName, string itemName)
    {
        lock (_lock)
        {
            string normalizedCategory = NormalizeInput(categoryName);
            string normalizedItem = NormalizeInput(itemName);
            if (string.IsNullOrEmpty(normalizedCategory) || string.IsNullOrEmpty(normalizedItem))
            {
                return false;
            }

            using SQLiteDatabase db = WritableDatabase;
            long categoryId = EnsureCategoryId(db, normalizedCategory, isBuiltin: false);
            if (categoryId <= 0)
            {
                return false;
            }

            using ICursor existing = db.Query(TableItems, new[] { ColItemId }, ColItemName + " = ?", new[] { normalizedItem }, null, null, null);
            if (existing.MoveToFirst())
            {
                return false;
            }

            var values = new ContentValues();
            values.Put(ColItemCatId, categoryId);
            values.Put(ColItemName, normalizedItem);
            values.Put(ColItemBuiltin, 0);
            return db.Insert(TableItems, null, values) > 0;
        }
    }

    public bool RenameCategory(string oldCategoryName, string newCategoryName)
    {
        lock (_lock)
        {
            string oldName = NormalizeInput(oldCategoryName);
            string newName = NormalizeInput(newCategoryName);
            if (string.IsNullOrEmpty(oldName) || string.IsNullOrEmpty(newName))
            {
                return false;
            }

            if (string.Equals(oldName, newName, StringComparison.Ordinal))
            {
                return true;
            }

            using SQLiteDatabase db = WritableDatabase;
            using ICursor oldCursor = db.Query(SwordTableName, new[] { ColCatId }, ColCatName + " = ?", new[] { oldName }, null, null, null);
            if (!oldCursor.MoveToFirst())
            {
                return false;
            }

            using ICursor newCursor = db.Query(SwordTableName, new[] { ColCatId }, ColCatName + " = ?", new[] { newName }, null, null, null);
            if (newCursor.MoveToFirst())
            {
                return false;
            }

            var values = new ContentValues();
            values.Put(ColCatName, newName);
            values.Put(ColCatBuiltin, 0);
            return db.Update(SwordTableName, values, ColCatName + " = ?", new[] { oldName }) > 0;
        }
    }

    public List<string> QueryCategories(bool includeCustom = true)
    {
        lock (_lock)
        {
            using SQLiteDatabase db = ReadableDatabase;
            using ICursor cursor = includeCustom
                ? db.Query(SwordTableName, new[] { ColCatName }, null, null, null, null, $"{ColCatName} ASC")
                : db.RawQuery(
                    "SELECT DISTINCT c." + ColCatName +
                    " FROM " + SwordTableName + " c INNER JOIN " + TableItems + " i ON c." + ColCatId + " = i." + ColItemCatId +
                    " WHERE c." + ColCatBuiltin + " = 1 AND i." + ColItemBuiltin + " = 1 ORDER BY c." + ColCatName + " ASC",
                    null);

            var categories = new List<string>();
            while (cursor.MoveToNext())
            {
                categories.Add(cursor.GetString(0));
            }

            return categories;
        }
    }

    public List<string> QueryItemsByCategory(string categoryName, bool includeCustom = true)
    {
        lock (_lock)
        {
            using SQLiteDatabase db = ReadableDatabase;
            string sql =
                "SELECT " + ColItemName +
                " FROM " + TableItems +
                " WHERE " + ColItemCatId + " IN (SELECT " + ColCatId + " FROM " + SwordTableName + " WHERE " + ColCatName + " = ?)" +
                (includeCustom ? string.Empty : " AND " + ColItemBuiltin + " = 1") +
                " ORDER BY " + ColItemName + " ASC";

            using ICursor cursor = db.RawQuery(sql, new[] { categoryName });
            var items = new List<string>();

            while (cursor.MoveToNext())
            {
                items.Add(cursor.GetString(0));
            }

            return items;
        }
    }

    public List<string> QueryRandomItemsByCategory(string categoryName, int items, bool includeCustom = true)
    {
        lock (_lock)
        {
            using SQLiteDatabase db = ReadableDatabase;
            string sql =
                "SELECT " + ColItemName +
                " FROM " + TableItems +
                " WHERE " + ColItemCatId + " IN (SELECT " + ColCatId + " FROM " + SwordTableName + " WHERE " + ColCatName + " = ?)" +
                (includeCustom ? string.Empty : " AND " + ColItemBuiltin + " = 1") +
                " ORDER BY RANDOM() LIMIT ?";

            using ICursor cursor = db.RawQuery(sql, new[] { categoryName, Math.Max(1, items).ToString() });
            var itemsList = new List<string>();

            while (cursor.MoveToNext())
            {
                itemsList.Add(cursor.GetString(0));
            }

            return itemsList;
        }
    }

    public int DeleteCategory(string categoryName)
    {
        lock (_lock)
        {
            string normalized = NormalizeInput(categoryName);
            if (string.IsNullOrEmpty(normalized))
            {
                return 0;
            }

            using SQLiteDatabase db = WritableDatabase;
            db.BeginTransaction();
            try
            {
                db.Delete(
                    TableItems,
                    ColItemCatId + " IN (SELECT " + ColCatId + " FROM " + SwordTableName + " WHERE " + ColCatName + " = ?)",
                    new[] { normalized });

                int deletedRows = db.Delete(SwordTableName, ColCatName + " = ?", new[] { normalized });
                db.SetTransactionSuccessful();
                return deletedRows;
            }
            finally
            {
                db.EndTransaction();
            }
        }
    }

    public int DeleteItem(string categoryName, string itemName)
    {
        lock (_lock)
        {
            using SQLiteDatabase db = WritableDatabase;
            return db.Delete(
                TableItems,
                ColItemName + " = ? AND " + ColItemCatId + " IN (SELECT " + ColCatId + " FROM " + SwordTableName + " WHERE " + ColCatName + " = ?)",
                new[] { itemName, categoryName });
        }
    }

    private static long EnsureCategoryId(SQLiteDatabase db, string categoryName, bool isBuiltin)
    {
        using ICursor cursor = db.Query(SwordTableName, new[] { ColCatId }, ColCatName + " = ?", new[] { categoryName }, null, null, null);
        if (cursor.MoveToFirst())
        {
            long categoryId = cursor.GetLong(0);
            if (isBuiltin)
            {
                var update = new ContentValues();
                update.Put(ColCatBuiltin, 1);
                db.Update(SwordTableName, update, ColCatId + " = ?", new[] { categoryId.ToString() });
            }

            return categoryId;
        }

        var values = new ContentValues();
        values.Put(ColCatName, categoryName);
        values.Put(ColCatBuiltin, isBuiltin ? 1 : 0);
        return db.Insert(SwordTableName, null, values);
    }

    private static void EnsureItem(SQLiteDatabase db, long categoryId, string itemName, bool isBuiltin)
    {
        string normalizedItem = NormalizeInput(itemName);
        if (categoryId <= 0 || string.IsNullOrEmpty(normalizedItem))
        {
            return;
        }

        using ICursor cursor = db.Query(
            TableItems,
            new[] { ColItemId },
            ColItemName + " = ? AND " + ColItemCatId + " = ?",
            new[] { normalizedItem, categoryId.ToString() },
            null,
            null,
            null,
            "1");

        if (cursor.MoveToFirst())
        {
            long itemId = cursor.GetLong(0);
            var update = new ContentValues();
            update.Put(ColItemBuiltin, isBuiltin ? 1 : 0);
            db.Update(TableItems, update, ColItemId + " = ?", new[] { itemId.ToString() });
            return;
        }

        using ICursor sameTextCursor = db.Query(
            TableItems,
            new[] { ColItemId },
            ColItemName + " = ?",
            new[] { normalizedItem },
            null,
            null,
            null,
            "1");

        if (sameTextCursor.MoveToFirst())
        {
            if (isBuiltin)
            {
                long itemId = sameTextCursor.GetLong(0);
                var update = new ContentValues();
                update.Put(ColItemBuiltin, 1);
                db.Update(TableItems, update, ColItemId + " = ?", new[] { itemId.ToString() });
            }

            return;
        }

        var values = new ContentValues();
        values.Put(ColItemCatId, categoryId);
        values.Put(ColItemName, normalizedItem);
        values.Put(ColItemBuiltin, isBuiltin ? 1 : 0);
        db.Insert(TableItems, null, values);
    }

    private static string NormalizeInput(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
