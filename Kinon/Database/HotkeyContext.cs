using System.Diagnostics;
using Kinon.Models;
using Microsoft.Data.Sqlite;

namespace Kinon.Database;

/// <summary>
/// SQLite 数据访问 — 创建表、CRUD、批量写入、搜索。
/// </summary>
public static class HotkeyContext
{
    private static string DbPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Kinon",
        "hotkeys.db"
    );

    private static string ConnectionString => $"Data Source={DbPath};Cache=Shared";

    /// <summary>
    /// 初始化数据库：创建目录、建表、建索引。
    /// </summary>
    public static void Initialize()
    {
        var dir = Path.GetDirectoryName(DbPath);
        if (dir != null) Directory.CreateDirectory(dir);

        using var conn = CreateConnection();
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS Hotkeys (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                HotkeyString TEXT NOT NULL,
                Description TEXT,
                ApplicationName TEXT NOT NULL DEFAULT '',
                ClickCount INTEGER NOT NULL DEFAULT 0,
                IsLearned INTEGER NOT NULL DEFAULT 0,
                ActionType TEXT NOT NULL DEFAULT '',
                ActionParam TEXT NOT NULL DEFAULT '',
                LastUsed TEXT NOT NULL DEFAULT ''
            );
            CREATE INDEX IF NOT EXISTS IX_Hotkeys_HotkeyString ON Hotkeys(HotkeyString);
            CREATE INDEX IF NOT EXISTS IX_Hotkeys_ApplicationName ON Hotkeys(ApplicationName);
            """;
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// 获取所有快捷键记录。
    /// </summary>
    public static List<HotkeyEntry> GetAll()
    {
        var list = new List<HotkeyEntry>();
        try
        {
            using var conn = CreateConnection();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, HotkeyString, Description, ApplicationName, ClickCount, IsLearned, ActionType, ActionParam, LastUsed FROM Hotkeys";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(ReadEntry(reader));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"HotkeyContext.GetAll failed: {ex.Message}");
        }
        return list;
    }

    /// <summary>
    /// 按程序名获取快捷键记录。
    /// </summary>
    public static List<HotkeyEntry> GetByApplication(string appName)
    {
        var list = new List<HotkeyEntry>();
        try
        {
            using var conn = CreateConnection();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, HotkeyString, Description, ApplicationName, ClickCount, IsLearned, ActionType, ActionParam, LastUsed FROM Hotkeys WHERE ApplicationName = @appName";
            cmd.Parameters.AddWithValue("@appName", appName);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(ReadEntry(reader));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"HotkeyContext.GetByApplication failed: {ex.Message}");
        }
        return list;
    }

    /// <summary>
    /// 批量写入/更新（INSERT OR REPLACE）。
    /// </summary>
    public static void BatchUpsert(List<HotkeyEntry> entries)
    {
        if (entries == null || entries.Count == 0)
            return;

        try
        {
            using var conn = CreateConnection();
            conn.Open();
            using var tx = conn.BeginTransaction();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT OR REPLACE INTO Hotkeys
                    (Id, HotkeyString, Description, ApplicationName, ClickCount, IsLearned, ActionType, ActionParam, LastUsed)
                VALUES
                    (@id, @hotkey, @desc, @app, @clicks, @learned, @actionType, @actionParam, @lastUsed)
                """;

            var idParam = cmd.CreateParameter(); idParam.ParameterName = "@id";
            var hotkeyParam = cmd.CreateParameter(); hotkeyParam.ParameterName = "@hotkey";
            var descParam = cmd.CreateParameter(); descParam.ParameterName = "@desc";
            var appParam = cmd.CreateParameter(); appParam.ParameterName = "@app";
            var clicksParam = cmd.CreateParameter(); clicksParam.ParameterName = "@clicks";
            var learnedParam = cmd.CreateParameter(); learnedParam.ParameterName = "@learned";
            var actionTypeParam = cmd.CreateParameter(); actionTypeParam.ParameterName = "@actionType";
            var actionParamParam = cmd.CreateParameter(); actionParamParam.ParameterName = "@actionParam";
            var lastUsedParam = cmd.CreateParameter(); lastUsedParam.ParameterName = "@lastUsed";

            cmd.Parameters.Add(idParam);
            cmd.Parameters.Add(hotkeyParam);
            cmd.Parameters.Add(descParam);
            cmd.Parameters.Add(appParam);
            cmd.Parameters.Add(clicksParam);
            cmd.Parameters.Add(learnedParam);
            cmd.Parameters.Add(actionTypeParam);
            cmd.Parameters.Add(actionParamParam);
            cmd.Parameters.Add(lastUsedParam);

            foreach (var entry in entries)
            {
                idParam.Value = entry.Id;
                hotkeyParam.Value = entry.HotkeyString;
                descParam.Value = (object?)entry.Description ?? DBNull.Value;
                appParam.Value = entry.ApplicationName;
                clicksParam.Value = entry.ClickCount;
                learnedParam.Value = entry.IsLearned ? 1 : 0;
                actionTypeParam.Value = entry.ActionType;
                actionParamParam.Value = entry.ActionParam;
                lastUsedParam.Value = entry.LastUsed?.ToString("o") ?? "";
                cmd.ExecuteNonQuery();
            }

            tx.Commit();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"HotkeyContext.BatchUpsert failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 插入新记录并返回自增 ID。
    /// </summary>
    public static int Insert(HotkeyEntry entry)
    {
        try
        {
            using var conn = CreateConnection();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO Hotkeys (HotkeyString, Description, ApplicationName, ClickCount, IsLearned, ActionType, ActionParam, LastUsed)
                VALUES (@hotkey, @desc, @app, @clicks, @learned, @actionType, @actionParam, @lastUsed);
                SELECT last_insert_rowid();
                """;
            cmd.Parameters.AddWithValue("@hotkey", entry.HotkeyString);
            cmd.Parameters.AddWithValue("@desc", (object?)entry.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@app", entry.ApplicationName);
            cmd.Parameters.AddWithValue("@clicks", entry.ClickCount);
            cmd.Parameters.AddWithValue("@learned", entry.IsLearned ? 1 : 0);
            cmd.Parameters.AddWithValue("@actionType", entry.ActionType);
            cmd.Parameters.AddWithValue("@actionParam", entry.ActionParam);
            cmd.Parameters.AddWithValue("@lastUsed", entry.LastUsed?.ToString("o") ?? "");
            var result = cmd.ExecuteScalar();
            if (result != null)
                entry.Id = Convert.ToInt32(result);
            return entry.Id;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"HotkeyContext.Insert failed: {ex.Message}");
            return 0;
        }
    }

    /// <summary>
    /// 更新已有记录。
    /// </summary>
    public static void Update(HotkeyEntry entry)
    {
        try
        {
            using var conn = CreateConnection();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                UPDATE Hotkeys SET
                    HotkeyString = @hotkey,
                    Description = @desc,
                    ApplicationName = @app,
                    ClickCount = @clicks,
                    IsLearned = @learned,
                    ActionType = @actionType,
                    ActionParam = @actionParam,
                    LastUsed = @lastUsed
                WHERE Id = @id
                """;
            cmd.Parameters.AddWithValue("@id", entry.Id);
            cmd.Parameters.AddWithValue("@hotkey", entry.HotkeyString);
            cmd.Parameters.AddWithValue("@desc", (object?)entry.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@app", entry.ApplicationName);
            cmd.Parameters.AddWithValue("@clicks", entry.ClickCount);
            cmd.Parameters.AddWithValue("@learned", entry.IsLearned ? 1 : 0);
            cmd.Parameters.AddWithValue("@actionType", entry.ActionType);
            cmd.Parameters.AddWithValue("@actionParam", entry.ActionParam);
            cmd.Parameters.AddWithValue("@lastUsed", entry.LastUsed?.ToString("o") ?? "");
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"HotkeyContext.Update failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 按 ID 删除记录。
    /// </summary>
    public static void Delete(int id)
    {
        try
        {
            using var conn = CreateConnection();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM Hotkeys WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"HotkeyContext.Delete failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 模糊搜索 HotkeyString 和 Description。
    /// </summary>
    public static List<HotkeyEntry> Search(string keyword)
    {
        var list = new List<HotkeyEntry>();
        if (string.IsNullOrWhiteSpace(keyword))
            return list;

        try
        {
            using var conn = CreateConnection();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT Id, HotkeyString, Description, ApplicationName, ClickCount, IsLearned, ActionType, ActionParam, LastUsed
                FROM Hotkeys
                WHERE HotkeyString LIKE @kw OR Description LIKE @kw
                ORDER BY ClickCount DESC
                """;
            cmd.Parameters.AddWithValue("@kw", $"%{keyword}%");
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(ReadEntry(reader));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"HotkeyContext.Search failed: {ex.Message}");
        }
        return list;
    }

    /// <summary>
    /// 获取最热门的 N 个快捷键。
    /// </summary>
    public static List<HotkeyEntry> GetTopByFrequency(int limit)
    {
        var list = new List<HotkeyEntry>();
        if (limit <= 0) limit = 10;

        try
        {
            using var conn = CreateConnection();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT Id, HotkeyString, Description, ApplicationName, ClickCount, IsLearned, ActionType, ActionParam, LastUsed
                FROM Hotkeys
                ORDER BY ClickCount DESC
                LIMIT @limit
                """;
            cmd.Parameters.AddWithValue("@limit", limit);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(ReadEntry(reader));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"HotkeyContext.GetTopByFrequency failed: {ex.Message}");
        }
        return list;
    }

    public static string? GetDbPathForDisplay() => DbPath;

    // ----- private helpers -----

    private static SqliteConnection CreateConnection()
    {
        return new SqliteConnection(ConnectionString);
    }

    private static HotkeyEntry ReadEntry(SqliteDataReader reader)
    {
        return new HotkeyEntry
        {
            Id = reader.GetInt32(0),
            HotkeyString = reader.GetString(1),
            Description = reader.IsDBNull(2) ? null : reader.GetString(2),
            ApplicationName = reader.GetString(3),
            ClickCount = reader.GetInt32(4),
            IsLearned = reader.GetInt32(5) != 0,
            ActionType = reader.GetString(6),
            ActionParam = reader.GetString(7),
            LastUsed = TryParseDateTime(reader, 8)
        };
    }

    private static DateTime? TryParseDateTime(SqliteDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
            return null;
        var raw = reader.GetString(ordinal);
        if (string.IsNullOrEmpty(raw))
            return null;
        if (DateTime.TryParse(raw, out var dt))
            return dt;
        return null;
    }
}
