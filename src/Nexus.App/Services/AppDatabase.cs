using Microsoft.Data.Sqlite;
using Nexus.App.Models;

namespace Nexus.App.Services;

public sealed class AppDatabase
{
    private readonly string _connectionString;
    private readonly string _databasePath;

    public AppDatabase(string databasePath)
    {
        _databasePath = Path.GetFullPath(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = _databasePath, Mode = SqliteOpenMode.ReadWriteCreate }.ToString();
    }

    public string DatabasePath => _databasePath;

    public void Initialize()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode = WAL;
            PRAGMA foreign_keys = ON;
            CREATE TABLE IF NOT EXISTS schema_info (version INTEGER NOT NULL);
            INSERT INTO schema_info(version) SELECT 1 WHERE NOT EXISTS (SELECT 1 FROM schema_info);
            CREATE TABLE IF NOT EXISTS tasks (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                title TEXT NOT NULL CHECK(length(title) BETWEEN 1 AND 240),
                is_completed INTEGER NOT NULL DEFAULT 0 CHECK(is_completed IN (0,1)),
                created_utc TEXT NOT NULL,
                completed_utc TEXT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_tasks_created_utc ON tasks(created_utc DESC);
            CREATE TABLE IF NOT EXISTS settings (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<TaskItem> GetTasks()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, title, is_completed, created_utc, completed_utc FROM tasks ORDER BY is_completed ASC, created_utc DESC;";
        using var reader = command.ExecuteReader();
        var items = new List<TaskItem>();
        while (reader.Read())
        {
            items.Add(new TaskItem(reader.GetInt64(0), reader.GetString(1), reader.GetInt64(2) == 1,
                DateTime.Parse(reader.GetString(3), null, System.Globalization.DateTimeStyles.RoundtripKind),
                reader.IsDBNull(4) ? null : DateTime.Parse(reader.GetString(4), null, System.Globalization.DateTimeStyles.RoundtripKind)));
        }
        return items;
    }

    public long AddTask(string title)
    {
        title = (title ?? string.Empty).Trim();
        if (title.Length is < 1 or > 240) throw new ArgumentException("Task title must contain 1–240 characters.", nameof(title));
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO tasks(title, is_completed, created_utc) VALUES ($title, 0, $created);";
        command.Parameters.AddWithValue("$title", title);
        command.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
        using var idCommand = connection.CreateCommand();
        idCommand.CommandText = "SELECT last_insert_rowid();";
        return (long)idCommand.ExecuteScalar()!;
    }

    public bool SetTaskCompleted(long id, bool completed)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE tasks SET is_completed=$done, completed_utc=$completed WHERE id=$id;";
        command.Parameters.AddWithValue("$done", completed ? 1 : 0);
        command.Parameters.AddWithValue("$completed", completed ? DateTime.UtcNow.ToString("O") : DBNull.Value);
        command.Parameters.AddWithValue("$id", id);
        return command.ExecuteNonQuery() == 1;
    }

    public bool DeleteTask(long id)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM tasks WHERE id=$id;";
        command.Parameters.AddWithValue("$id", id);
        return command.ExecuteNonQuery() == 1;
    }

    public string GetSetting(string key, string fallback)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM settings WHERE key=$key;";
        command.Parameters.AddWithValue("$key", key);
        return command.ExecuteScalar() as string ?? fallback;
    }

    public void SetSetting(string key, string value)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO settings(key,value) VALUES($key,$value) ON CONFLICT(key) DO UPDATE SET value=excluded.value;";
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.ExecuteNonQuery();
    }

    public void CreateConsistentSnapshot(string destinationPath)
    {
        var destination = Path.GetFullPath(destinationPath);
        if (string.Equals(destination, _databasePath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Backup destination must not be the live database.");
        if (File.Exists(destination))
            throw new IOException("Backup destination already exists; existing files are never overwritten.");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        // Atomically reserve the destination so a concurrent operation cannot make SQLite
        // open and mutate an unrelated file between the existence check and connection open.
        using (new FileStream(destination, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None)) { }
        try
        {
            using var source = Open();
            using var target = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = destination, Mode = SqliteOpenMode.ReadWrite, Pooling = false }.ToString());
            target.Open();
            source.BackupDatabase(target);
        }
        catch
        {
            // A failed snapshot must not leave an empty or partial DB that looks like a completed backup.
            SqliteConnection.ClearAllPools();
            try { if (File.Exists(destination)) File.Delete(destination); } catch { }
            try { if (File.Exists(destination + "-wal")) File.Delete(destination + "-wal"); } catch { }
            try { if (File.Exists(destination + "-shm")) File.Delete(destination + "-shm"); } catch { }
            throw;
        }
    }

    public void RestoreConsistentSnapshot(string snapshotPath)
    {
        var sourcePath = Path.GetFullPath(snapshotPath);
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("Verified restore source was not found.", sourcePath);
        if (string.Equals(sourcePath, _databasePath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Restore source cannot be the live database.");
        if (!VerifySnapshot(sourcePath)) throw new InvalidDataException("Restore source failed SQLite integrity verification.");

        // Close idle pooled handles before replacing the live database contents.
        SqliteConnection.ClearAllPools();
        using var source = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = sourcePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        source.Open();
        using var destination = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = _databasePath, Mode = SqliteOpenMode.ReadWrite, Pooling = false }.ToString());
        destination.Open();
        using (var pragma = destination.CreateCommand())
        { pragma.CommandText = "PRAGMA foreign_keys = ON;"; pragma.ExecuteNonQuery(); }
        source.BackupDatabase(destination);
        using var check = destination.CreateCommand();
        check.CommandText = "PRAGMA integrity_check;";
        if (!string.Equals(check.ExecuteScalar() as string, "ok", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Restored database failed SQLite integrity verification.");
        SqliteConnection.ClearAllPools();
    }

    public bool VerifySnapshot(string snapshotPath)
    {
        var path = Path.GetFullPath(snapshotPath);
        if (!File.Exists(path) || string.Equals(path, _databasePath, StringComparison.OrdinalIgnoreCase)) return false;
        // Do not trust a snapshot that is itself a symlink/junction/reparse-point file.
        try
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) return false;
        }
        catch { return false; }
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";
        return string.Equals(command.ExecuteScalar() as string, "ok", StringComparison.OrdinalIgnoreCase);
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON;";
        command.ExecuteNonQuery();
        return connection;
    }
}
