using Nexus.App.Services;
using Xunit;

namespace Nexus.App.Tests;

public sealed class DatabaseTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "nexus-tests-" + Guid.NewGuid().ToString("N"));
    private AppDatabase CreateDatabase()
    {
        Directory.CreateDirectory(_directory);
        var database = new AppDatabase(Path.Combine(_directory, "test.db"));
        database.Initialize();
        return database;
    }

    [Fact]
    public void SettingsPersistAndCanBeUpdatedWithoutAffectingOtherKeys()
    {
        var db = CreateDatabase();
        db.SetSetting("accent", "ocean");
        db.SetSetting("density", "compact");
        db.SetSetting("accent", "violet");

        Assert.Equal("violet", db.GetSetting("accent", "fallback"));
        Assert.Equal("compact", db.GetSetting("density", "fallback"));
        Assert.Equal("fallback", db.GetSetting("missing", "fallback"));
    }

    [Fact]
    public void TaskPersistsAndCanBeCompleted()
    {
        var db = CreateDatabase();
        var id = db.AddTask("Test persistence");
        Assert.Contains(db.GetTasks(), t => t.Id == id && t.Title == "Test persistence" && !t.IsCompleted);
        Assert.True(db.SetTaskCompleted(id, true));
        Assert.Contains(db.GetTasks(), t => t.Id == id && t.IsCompleted);
    }

    [Fact]
    public void RejectsEmptyAndOversizedTaskTitles()
    {
        var db = CreateDatabase();
        Assert.Throws<ArgumentException>(() => db.AddTask("   "));
        Assert.Throws<ArgumentException>(() => db.AddTask(new string('x', 241)));
    }

    [Fact]
    public void SnapshotIsSeparateAndContainsDatabase()
    {
        var db = CreateDatabase();
        db.AddTask("Backup test");
        var snapshot = Path.Combine(_directory, "backup.db");
        db.CreateConsistentSnapshot(snapshot);
        Assert.True(File.Exists(snapshot));
        Assert.True(new FileInfo(snapshot).Length > 0);
        Assert.NotEqual(Path.GetFullPath(snapshot), db.DatabasePath);
        Assert.True(db.VerifySnapshot(snapshot));
    }

    [Fact]
    public void RejectsSnapshotThatIsAFileSymbolicLinkWhenSupported()
    {
        var db = CreateDatabase();
        db.AddTask("Original");
        var target = Path.Combine(_directory, "real-backup.db");
        db.CreateConsistentSnapshot(target);
        var link = Path.Combine(_directory, "linked-backup.db");
        try { File.CreateSymbolicLink(link, target); }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or NotSupportedException or ArgumentException)
        {
            return; // Symbolic-link creation may be unavailable on the host running tests.
        }
        Assert.False(db.VerifySnapshot(link));
    }

    [Fact]
    public void BackupManifestContainsMatchingSha256()
    {
        var db = CreateDatabase();
        db.AddTask("Manifest check");
        var audit = new AuditLog(Path.Combine(_directory, "audit.log"));
        var service = new BackupService(db, audit);
        var result = service.CreateVerifiedBackup(Path.Combine(_directory, "backups"));
        using var stream = File.OpenRead(result.DatabasePath);
        var actual = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));
        Assert.Equal(actual, result.Sha256);
        using var manifest = System.Text.Json.JsonDocument.Parse(File.ReadAllText(result.ManifestPath));
        Assert.Equal(actual, manifest.RootElement.GetProperty("sha256").GetString());
    }

    [Fact]
    public void BackupCanBeVerifiedAndRestoredWithRollbackProtection()
    {
        var db = CreateDatabase();
        db.AddTask("Before restore");
        var audit = new AuditLog(Path.Combine(_directory, "audit.log"));
        var service = new BackupService(db, audit);
        var backup = service.CreateVerifiedBackup(Path.Combine(_directory, "backups"));
        db.AddTask("After backup");

        var verified = service.VerifyBackup(backup.ManifestPath);
        Assert.NotNull(verified);
        service.RestoreVerifiedBackup(verified!, Path.Combine(_directory, "backups"));

        var tasks = db.GetTasks();
        Assert.Contains(tasks, t => t.Title == "Before restore");
        Assert.DoesNotContain(tasks, t => t.Title == "After backup");
        Assert.True(db.VerifySnapshot(db.DatabasePath) == false); // The live database is never accepted as a backup source.
    }

    [Fact]
    public void TamperedBackupIsRejected()
    {
        var db = CreateDatabase();
        db.AddTask("Integrity test");
        var service = new BackupService(db, new AuditLog(Path.Combine(_directory, "audit.log")));
        var result = service.CreateVerifiedBackup(Path.Combine(_directory, "backups"));
        File.AppendAllText(result.DatabasePath, "tamper");
        Assert.Null(service.VerifyBackup(result.ManifestPath));
        Assert.Empty(service.ListVerifiedBackups(Path.Combine(_directory, "backups")));
    }


    [Fact]
    public void RestoreCreatesMissingRollbackDirectoryAndRechecksBackup()
    {
        var db = CreateDatabase();
        db.AddTask("Before backup");
        var sourceBackups = Path.Combine(_directory, "source-backups");
        var rollbackDirectory = Path.Combine(_directory, "rollback-directory-not-created-yet");
        var service = new BackupService(db, new AuditLog(Path.Combine(_directory, "audit.log")));
        var backup = service.CreateVerifiedBackup(sourceBackups);
        db.AddTask("After backup");

        // The rollback destination is intentionally absent; restore should create it safely.
        service.RestoreVerifiedBackup(service.VerifyBackup(backup.ManifestPath)!, rollbackDirectory);

        Assert.True(Directory.Exists(rollbackDirectory));
        Assert.Contains(db.GetTasks(), task => task.Title == "Before backup");
        Assert.DoesNotContain(db.GetTasks(), task => task.Title == "After backup");
    }

    [Fact]
    public void RejectsMalformedManifestWithoutThrowing()
    {
        var db = CreateDatabase();
        var service = new BackupService(db, new AuditLog(Path.Combine(_directory, "audit.log")));
        var backups = Path.Combine(_directory, "backups");
        Directory.CreateDirectory(backups);
        var manifest = Path.Combine(backups, "bad.manifest.json");
        File.WriteAllText(manifest, "{ not valid json }");

        Assert.Null(service.VerifyBackup(manifest));
        Assert.Empty(service.ListVerifiedBackups(backups));
    }

    [Fact]
    public void RejectsManifestWithNonHexadecimalHash()
    {
        var db = CreateDatabase();
        var service = new BackupService(db, new AuditLog(Path.Combine(_directory, "audit.log")));
        var result = service.CreateVerifiedBackup(Path.Combine(_directory, "backups"));
        var json = File.ReadAllText(result.ManifestPath);
        var hash = System.Text.Json.JsonDocument.Parse(json).RootElement.GetProperty("sha256").GetString()!;
        File.WriteAllText(result.ManifestPath, json.Replace(hash, new string('Z', 64), StringComparison.Ordinal));

        Assert.Null(service.VerifyBackup(result.ManifestPath));
    }


    [Fact]
    public void BackupCreationFailsClosedWhenAuditLogCannotBeWritten()
    {
        var db = CreateDatabase();
        var blocker = Path.Combine(_directory, "audit-blocker");
        File.WriteAllText(blocker, "not a directory");
        var service = new BackupService(db, new AuditLog(Path.Combine(blocker, "audit.log")));
        var backupDirectory = Path.Combine(_directory, "blocked-backups");

        Assert.Throws<IOException>(() => service.CreateVerifiedBackup(backupDirectory));
        Assert.True(Directory.Exists(backupDirectory));
        Assert.Empty(Directory.EnumerateFiles(backupDirectory));
    }

    [Fact]
    public void RestoreRejectsStaleBackupMetadataBeforeChangingDatabase()
    {
        var db = CreateDatabase();
        db.AddTask("Keep this task");
        var service = new BackupService(db, new AuditLog(Path.Combine(_directory, "audit.log")));
        var backup = service.CreateVerifiedBackup(Path.Combine(_directory, "backups"));
        db.AddTask("Must remain after rejected restore");
        var verified = service.VerifyBackup(backup.ManifestPath)!;
        var stale = verified with { Sha256 = new string('0', 64) };

        Assert.Throws<InvalidDataException>(() => service.RestoreVerifiedBackup(stale, Path.Combine(_directory, "rollback")));
        Assert.Contains(db.GetTasks(), task => task.Title == "Keep this task");
        Assert.Contains(db.GetTasks(), task => task.Title == "Must remain after rejected restore");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true); } catch { }
    }
}
