using Android.Content;
using Android.Database;
using Android.Database.Sqlite;
using IME.Shared.Utils.FileOperation;
using IME.Shared.Models;
using Newtonsoft.Json;
using System.Collections.Generic;
using Android.Util;

namespace IME.Shared.Data;

public class SwordDBHelper : SQLiteOpenHelper
{
    private static readonly object _lock = new object();
    //设置唯一的数据库名
    private static readonly string dbName = "SwordDB.db";
    private static readonly int dbVersion = 1;

    // 分类表
    // 快捷短语表名
    public static readonly string SwordTableName = "SwordTable";
    private static readonly string COL_CAT_ID = "_id";
    private static readonly string COL_CAT_NAME = "name";

    // 快捷短语的表
    private static readonly string TABLE_ITEMS = "items";
    private static readonly string COL_ITEM_ID = "_id";
    private static readonly string COL_ITEM_CAT_ID = "category_id";
    private static readonly string COL_ITEM_NAME = "item_name";

    public SwordDBHelper(Context context) : base(context, dbName, null, dbVersion)
    {
        // 不在构造函数中打开数据库，让 SQLiteOpenHelper 自动管理
    }

    // 创建数据库表
    public override void OnCreate(SQLiteDatabase db)
    {
        lock (_lock)
        {
            Log.Info("SwordDBHelper", "创建数据库");
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
                Log.Error("SwordDBHelper", $"打开数据库时检查失败: {ex.Message}");
            }
        }
    }

    public override void OnUpgrade(SQLiteDatabase db, int oldVersion, int newVersion)
    {
        //更新数据库
    }

    private void EnsureTables(SQLiteDatabase db)
    {
        if (TableExists(db, SwordTableName))
        {
            return;
        }

        //创建分类表
        string createCategoriesTable = "CREATE TABLE " + SwordTableName + "("
                                   + COL_CAT_ID + " INTEGER PRIMARY KEY AUTOINCREMENT, "
                                   + COL_CAT_NAME + " TEXT UNIQUE)";
        db.ExecSQL(createCategoriesTable);

        // 创建短语表（带外键约束）
        string createItemsTable = "CREATE TABLE " + TABLE_ITEMS + "("
                              + COL_ITEM_ID + " INTEGER PRIMARY KEY AUTOINCREMENT, "
                              + COL_ITEM_CAT_ID + " INTEGER, "
                              + COL_ITEM_NAME + " TEXT UNIQUE,"
                              + "FOREIGN KEY(" + COL_ITEM_CAT_ID + ") REFERENCES "
                              + SwordTableName + "(" + COL_CAT_ID + "))";
        db.ExecSQL(createItemsTable);
    }

    private bool TableExists(SQLiteDatabase db, string tableName)
    {
        using var cursor = db.RawQuery("SELECT name FROM sqlite_master WHERE type='table' AND name=?", new[] { tableName });
        return cursor.MoveToFirst();
    }

    private long GetCategoryCount(SQLiteDatabase db)
    {
        using var cursor = db.RawQuery($"SELECT COUNT(1) FROM {SwordTableName}", null);
        if (!cursor.MoveToFirst())
        {
            return 0;
        }

        return cursor.GetLong(0);
    }

    private void EnsureSeedData(SQLiteDatabase db)
    {
        if (GetCategoryCount(db) > 0)
        {
            return;
        }

        InsertInitData(db);
    }

    private void InsertInitData(SQLiteDatabase db)
    {
        Log.Info("SwordDBHelper", "插入初始化数据");
        try
        {
            var fileReadOperation = new FileReadOperation();
            var json = fileReadOperation.ReadJsonFile(Application.Context, Resource.Raw.sd);

            if (string.IsNullOrWhiteSpace(json))
            {
                Log.Warn("SwordDBHelper", "sd.json 读取为空，跳过初始化数据");
                return;
            }

            var weapons = JsonConvert.DeserializeObject<List<SdDataModel>>(json);
            if (weapons == null || weapons.Count == 0)
            {
                Log.Warn("SwordDBHelper", "sd.json 解析为空，跳过初始化数据");
                return;
            }

            // 使用事务提升插入性能
            db.BeginTransaction();

            try
            {
                foreach (var weapon in weapons)
                {
                    // 检查分类是否已经存在
                    using var cursor = db.Query(SwordTableName, new[] { COL_CAT_ID }, COL_CAT_NAME + " = ?", new[] { weapon.Name }, null, null, null);
                    long categoryId = -1;

                    if (cursor.MoveToFirst())
                    {
                        // 分类已存在，获取其ID
                        categoryId = cursor.GetLong(0);
                    }
                    else
                    {
                        // 插入分类表
                        var categoryValues = new ContentValues();
                        categoryValues.Put(COL_CAT_NAME, weapon.Name);
                        categoryId = db.InsertOrThrow(SwordTableName, null, categoryValues);
                    }

                    // 插入短语数据
                    foreach (var move in weapon.Data)
                    {
                        using var cursor1 = db.Query(TABLE_ITEMS, new[] { COL_ITEM_ID }, COL_ITEM_NAME + " = ?", new[] { move }, null, null, null);

                        if (!cursor1.MoveToFirst())
                        {
                            var itemValues = new ContentValues();
                            itemValues.Put(COL_ITEM_CAT_ID, categoryId);
                            itemValues.Put(COL_ITEM_NAME, move);
                            db.InsertOrThrow(TABLE_ITEMS, null, itemValues);
                        }
                    }
                }

                db.SetTransactionSuccessful();
                Log.Info("SwordDBHelper", "插入数据成功");
            }
            finally
            {
                db.EndTransaction();
            }
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error("DB_INSERT", $"初始化数据失败: {ex.Message}");
            throw new SQLiteException("数据库初始化失败");
        }
        // 不要在这里关闭数据库，让 SQLiteOpenHelper 管理数据库生命周期
    }

    //查询分类数据
    public bool AddCategory(string categoryName)
    {
        lock (_lock)
        {
            string normalized = NormalizeInput(categoryName);
            if (string.IsNullOrEmpty(normalized))
            {
                return false;
            }

            using var db = this.WritableDatabase;
            using var existing = db.Query(SwordTableName, new[] { COL_CAT_ID }, COL_CAT_NAME + " = ?", new[] { normalized }, null, null, null);
            if (existing.MoveToFirst())
            {
                return false;
            }

            var values = new ContentValues();
            values.Put(COL_CAT_NAME, normalized);
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

            using var db = this.WritableDatabase;
            long categoryId = EnsureCategoryId(db, normalizedCategory);
            if (categoryId <= 0)
            {
                return false;
            }

            using var existing = db.Query(TABLE_ITEMS, new[] { COL_ITEM_ID }, COL_ITEM_NAME + " = ?", new[] { normalizedItem }, null, null, null);
            if (existing.MoveToFirst())
            {
                return false;
            }

            var values = new ContentValues();
            values.Put(COL_ITEM_CAT_ID, categoryId);
            values.Put(COL_ITEM_NAME, normalizedItem);
            return db.Insert(TABLE_ITEMS, null, values) > 0;
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

            using var db = this.WritableDatabase;
            using var oldCursor = db.Query(SwordTableName, new[] { COL_CAT_ID }, COL_CAT_NAME + " = ?", new[] { oldName }, null, null, null);
            if (!oldCursor.MoveToFirst())
            {
                return false;
            }

            using var newCursor = db.Query(SwordTableName, new[] { COL_CAT_ID }, COL_CAT_NAME + " = ?", new[] { newName }, null, null, null);
            if (newCursor.MoveToFirst())
            {
                return false;
            }

            var values = new ContentValues();
            values.Put(COL_CAT_NAME, newName);
            return db.Update(SwordTableName, values, COL_CAT_NAME + " = ?", new[] { oldName }) > 0;
        }
    }

    public List<string> QueryCategories()
    {
        lock (_lock)
        {
            using var db = this.ReadableDatabase;
            var categories = new List<string>();
            using var cursor = db.Query(SwordTableName, new[] { COL_CAT_NAME }, null, null, null, null, null);

            if (cursor.MoveToFirst())
            {
                do
                {
                    categories.Add(cursor.GetString(0));
                } while (cursor.MoveToNext());
            }

            return categories;
        }
    }

    // 查询招式数据
    public List<string> QueryItemsByCategory(string categoryName)
    {
        lock (_lock)
        {
            using var db = this.ReadableDatabase;
            var items = new List<string>();
            var query = "SELECT " + COL_ITEM_NAME + " FROM " + TABLE_ITEMS + " WHERE " + COL_ITEM_CAT_ID + " IN (SELECT " + COL_CAT_ID + " FROM " + SwordTableName + " WHERE " + COL_CAT_NAME + " = ?)";
            using var cursor = db.RawQuery(query, new[] { categoryName });

            if (cursor.MoveToFirst())
            {
                do
                {
                    items.Add(cursor.GetString(0));
                } while (cursor.MoveToNext());
            }
            return items;
        }
    }
    
    // 随机返回招式数据，items 条数
    public List<string> QueryRandomItemsByCategory(string categoryName, int items)
    {
        lock (_lock)
        {
            using var db = this.ReadableDatabase;
            var itemsList = new List<string>();
            // 随机查询并返回指定的条数
            var query = "SELECT " + COL_ITEM_NAME + " FROM " + TABLE_ITEMS + " WHERE " + COL_ITEM_CAT_ID + " IN (SELECT " + COL_CAT_ID + " FROM " + SwordTableName + " WHERE " + COL_CAT_NAME + " = ?) ORDER BY RANDOM() LIMIT ?";
            
            using var cursor = db.RawQuery(query, new[] { categoryName, items.ToString()});

            if (cursor.MoveToFirst())
            {
                do
                {
                    itemsList.Add(cursor.GetString(0));
                } while (cursor.MoveToNext());
            }
            return itemsList;
        }
    }
    
    // ??????
    public int DeleteCategory(string categoryName)
    {
        lock (_lock)
        {
            string normalized = NormalizeInput(categoryName);
            if (string.IsNullOrEmpty(normalized))
            {
                return 0;
            }

            using var db = this.WritableDatabase;
            db.BeginTransaction();
            try
            {
                db.Delete(TABLE_ITEMS, COL_ITEM_CAT_ID + " IN (SELECT " + COL_CAT_ID + " FROM " + SwordTableName + " WHERE " + COL_CAT_NAME + " = ?)", new[] { normalized });
                var deletedRows = db.Delete(SwordTableName, COL_CAT_NAME + " = ?", new[] { normalized });
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
            using var db = this.WritableDatabase;
            var deletedRows = db.Delete(TABLE_ITEMS, COL_ITEM_NAME + " = ? AND " + COL_ITEM_CAT_ID + " IN (SELECT " + COL_CAT_ID + " FROM " + SwordTableName + " WHERE " + COL_CAT_NAME + " = ?)", new[] { itemName, categoryName });
            return deletedRows;
        }
    }

    private long EnsureCategoryId(SQLiteDatabase db, string categoryName)
    {
        using var cursor = db.Query(SwordTableName, new[] { COL_CAT_ID }, COL_CAT_NAME + " = ?", new[] { categoryName }, null, null, null);
        if (cursor.MoveToFirst())
        {
            return cursor.GetLong(0);
        }

        var values = new ContentValues();
        values.Put(COL_CAT_NAME, categoryName);
        return db.Insert(SwordTableName, null, values);
    }

    private static string NormalizeInput(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

}





