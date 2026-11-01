using Nexus.App.Services;
using Xunit;

namespace Nexus.App.Tests;

public sealed class AuditLogTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "nexus-audit-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void RecordWritesEventWithoutPlaintextTarget()
    {
        Directory.CreateDirectory(_directory);
        var logPath = Path.Combine(_directory, "audit.log");
        var audit = new AuditLog(logPath);
        var sensitiveTarget = Path.Combine(_directory, "private-document.txt");

        Assert.True(audit.Record("file.open", "success", sensitiveTarget));
        var contents = File.ReadAllText(logPath);
        Assert.Contains("file.open", contents);
        Assert.Contains("success", contents);
        Assert.Contains("target-sha256:", contents);
        Assert.DoesNotContain(sensitiveTarget, contents, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RecordStripsControlCharactersFromEventFields()
    {
        Directory.CreateDirectory(_directory);
        var logPath = Path.Combine(_directory, "audit.log");
        var audit = new AuditLog(logPath);

        Assert.True(audit.Record("security\r\nforged", "ok\tforged"));
        var contents = File.ReadAllText(logPath);
        Assert.DoesNotContain("forged\r", contents);
        Assert.DoesNotContain("forged\n", contents);
        Assert.Contains("securityforged", contents);
        Assert.Contains("okforged", contents);
    }

    [Fact]
    public void RecordFailsClosedWhenAuditPathCannotBeCreated()
    {
        Directory.CreateDirectory(_directory);
        var blocker = Path.Combine(_directory, "not-a-directory");
        File.WriteAllText(blocker, "blocker");
        var audit = new AuditLog(Path.Combine(blocker, "audit.log"));

        Assert.False(audit.Record("security.test", "success"));
    }

    [Fact]
    public void CheckHealthReportsValidRecordsAndCount()
    {
        Directory.CreateDirectory(_directory);
        var audit = new AuditLog(Path.Combine(_directory, "audit.log"));
        Assert.True(audit.Record("security.test", "success"));
        Assert.True(audit.Record("backup.create", "success", Path.Combine(_directory, "backup.db")));

        var health = audit.CheckHealth();

        Assert.True(health.IsHealthy);
        Assert.Equal(2, health.EventCount);
    }

    [Fact]
    public void CheckHealthRejectsMalformedRecords()
    {
        Directory.CreateDirectory(_directory);
        var logPath = Path.Combine(_directory, "audit.log");
        File.WriteAllText(logPath, "not-a-valid-audit-record\n");
        var health = new AuditLog(logPath).CheckHealth();

        Assert.False(health.IsHealthy);
        Assert.Contains("malformed", health.Status, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true); } catch { }
    }
}
