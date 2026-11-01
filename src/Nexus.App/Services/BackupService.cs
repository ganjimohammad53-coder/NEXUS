using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Nexus.App.Services;

public sealed record BackupResult(string DatabasePath, string ManifestPath, string Sha256, DateTime CreatedUtc);
public sealed record VerifiedBackup(string DatabasePath, string ManifestPath, string Sha256, DateTime CreatedUtc, long Bytes);

public sealed class BackupService(AppDatabase database, AuditLog audit)
{
    public BackupResult CreateVerifiedBackup(string backupDirectory)
    {
        var directory = Path.GetFullPath(backupDirectory);
        Directory.CreateDirectory(directory);
        var created = DateTime.UtcNow;
        var file = Path.Combine(directory, $"nexus-backup-{created:yyyyMMdd-HHmmss-fff}Z.db");
        var manifestPath = file + ".manifest.json";
        if (File.Exists(file) || File.Exists(manifestPath))
            throw new IOException("A backup with this timestamp already exists; existing files are never overwritten.");

        if (!audit.Record("backup.create", "started", file))
            throw new IOException("Security audit is unavailable. Backup creation was blocked.");
        try
        {
            database.CreateConsistentSnapshot(file);
            var info = new FileInfo(file);
            if (!info.Exists || info.Length == 0) throw new IOException("Backup snapshot is empty.");
            if (!database.VerifySnapshot(file)) throw new IOException("SQLite integrity verification failed for the backup snapshot.");
            string hash;
            using (var stream = File.OpenRead(file)) hash = Convert.ToHexString(SHA256.HashData(stream));
            var manifest = JsonSerializer.Serialize(new BackupManifest(1, created, Path.GetFileName(file), info.Length, hash), new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            using (var manifestStream = new FileStream(manifestPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(manifestStream, new UTF8Encoding(false))) { writer.Write(manifest); writer.Flush(); manifestStream.Flush(true); }
            if (VerifyBackup(manifestPath) is null) throw new IOException("Backup manifest verification failed.");
            if (!audit.Record("backup.create", "success", file)) throw new IOException("Security audit is unavailable; backup creation was not accepted as complete.");
            return new BackupResult(file, manifestPath, hash, created);
        }
        catch
        {
            // Do not leave partial database/manifest artifacts that could be mistaken for a usable backup.
            // Never delete an artifact unless it is one of the exact paths reserved by this operation.
            try { if (File.Exists(manifestPath)) File.Delete(manifestPath); } catch { }
            try { if (File.Exists(file)) File.Delete(file); } catch { }
            try { if (File.Exists(file + "-wal")) File.Delete(file + "-wal"); } catch { }
            try { if (File.Exists(file + "-shm")) File.Delete(file + "-shm"); } catch { }
            audit.Record("backup.create", "failed", file);
            throw;
        }
    }

    public VerifiedBackup? VerifyBackup(string manifestPath)
    {
        try
        {
            var manifestFull = Path.GetFullPath(manifestPath);
            if (!File.Exists(manifestFull) || IsReparsePoint(manifestFull) ||
                !manifestFull.EndsWith(".manifest.json", StringComparison.OrdinalIgnoreCase)) return null;
            using var document = JsonDocument.Parse(File.ReadAllText(manifestFull));
            var root = document.RootElement;
            if (root.GetProperty("formatVersion").GetInt32() != 1) return null;
            var fileName = root.GetProperty("databaseFile").GetString();
            if (string.IsNullOrWhiteSpace(fileName) || !string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal)) return null;
            var expectedBytes = root.GetProperty("bytes").GetInt64();
            var expectedHash = root.GetProperty("sha256").GetString();
            var created = root.GetProperty("createdUtc").GetDateTime();
            if (expectedBytes <= 0 || string.IsNullOrWhiteSpace(expectedHash) || expectedHash.Length != 64 ||
                expectedHash.Any(c => !Uri.IsHexDigit(c))) return null;
            var dbPath = Path.Combine(Path.GetDirectoryName(manifestFull)!, fileName);
            var info = new FileInfo(dbPath);
            if (!info.Exists || IsReparsePoint(dbPath) || info.Length != expectedBytes || !database.VerifySnapshot(dbPath)) return null;
            string actualHash;
            using (var stream = File.OpenRead(dbPath)) actualHash = Convert.ToHexString(SHA256.HashData(stream));
            if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(actualHash), Encoding.ASCII.GetBytes(expectedHash.ToUpperInvariant()))) return null;
            return new VerifiedBackup(dbPath, manifestFull, actualHash, created, info.Length);
        }
        catch { return null; }
    }

    public IReadOnlyList<VerifiedBackup> ListVerifiedBackups(string backupDirectory)
    {
        if (!Directory.Exists(backupDirectory)) return [];
        return Directory.EnumerateFiles(Path.GetFullPath(backupDirectory), "*.manifest.json", SearchOption.TopDirectoryOnly)
            .Select(VerifyBackup).Where(x => x is not null).Select(x => x!)
            .OrderByDescending(x => x.CreatedUtc).ToArray();
    }

    public void RestoreVerifiedBackup(VerifiedBackup backup, string backupDirectory)
    {
        ArgumentNullException.ThrowIfNull(backup);
        var verified = VerifyBackup(backup.ManifestPath);
        if (verified is null ||
            !PathEquals(verified.DatabasePath, backup.DatabasePath) ||
            !string.Equals(verified.Sha256, backup.Sha256, StringComparison.OrdinalIgnoreCase) ||
            verified.Bytes != backup.Bytes || verified.CreatedUtc != backup.CreatedUtc)
            throw new InvalidDataException("Backup verification changed. Select and verify the backup again.");

        var livePath = Path.GetFullPath(database.DatabasePath);
        if (PathEquals(verified.DatabasePath, livePath))
            throw new InvalidOperationException("Cannot restore the live database from itself.");

        var directory = Path.GetFullPath(backupDirectory);
        Directory.CreateDirectory(directory);
        // A random suffix avoids same-millisecond collisions and never reuses an existing recovery file.
        var rollbackPath = Path.Combine(directory, $"nexus-rollback-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}Z-{Guid.NewGuid():N}.db");
        if (!audit.Record("backup.restore", "started", verified.DatabasePath))
            throw new IOException("Security audit is unavailable. Restore was blocked.");

        var rollbackReady = false;
        var restoreAttempted = false;
        var preserveRollback = false;
        try
        {
            // The rollback must be both created and verified before touching the live database.
            database.CreateConsistentSnapshot(rollbackPath);
            if (!database.VerifySnapshot(rollbackPath))
                throw new IOException($"Pre-restore rollback snapshot failed integrity verification. Preserve this file for inspection: {rollbackPath}");
            rollbackReady = true;

            // Re-verify the selected backup immediately before the mutation to narrow the TOCTOU window.
            var lastCheck = VerifyBackup(backup.ManifestPath);
            if (lastCheck is null || !PathEquals(lastCheck.DatabasePath, verified.DatabasePath) ||
                !string.Equals(lastCheck.Sha256, verified.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Backup changed immediately before restore. No database changes were made.");

            restoreAttempted = true;
            database.RestoreConsistentSnapshot(verified.DatabasePath);
            if (!audit.Record("backup.restore", "success", verified.DatabasePath))
                throw new IOException("Audit logging failed after restore; the pre-restore database will be reinstated.");
        }
        catch (Exception restoreError)
        {
            audit.Record("backup.restore", "failed", verified.DatabasePath);
            if (restoreAttempted && rollbackReady)
            {
                try
                {
                    database.RestoreConsistentSnapshot(rollbackPath);
                    if (!audit.Record("backup.restore.rollback", "success", rollbackPath))
                    {
                        preserveRollback = true;
                        throw new IOException("Rollback ran but its audit event could not be recorded.");
                    }
                }
                catch (Exception rollbackError)
                {
                    preserveRollback = true;
                    audit.Record("backup.restore.rollback", "failed", rollbackPath);
                    throw new IOException(
                        $"Restore failed and automatic rollback could not be confirmed. Preserve this recovery file: {rollbackPath}",
                        new AggregateException(restoreError, rollbackError));
                }
            }
            else if (restoreAttempted)
            {
                preserveRollback = true;
                throw new IOException(
                    $"Restore failed, and a verified rollback snapshot was not available. Inspect the database and preserve any recovery files in: {directory}",
                    restoreError);
            }
            throw;
        }
        finally
        {
            if (rollbackReady && !preserveRollback)
            {
                try { if (File.Exists(rollbackPath)) File.Delete(rollbackPath); }
                catch { /* Keep the recovery artifact if cleanup is blocked. */ }
            }
        }
    }

    private static bool IsReparsePoint(string path)
    {
        try { return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0; }
        catch { return true; } // Unknown path state is not trusted for a restore source.
    }

    private static bool PathEquals(string left, string right) =>
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);

    private sealed record BackupManifest(int FormatVersion, DateTime CreatedUtc, string DatabaseFile, long Bytes, string Sha256);
}
