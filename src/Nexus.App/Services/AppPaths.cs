namespace Nexus.App.Services;

public sealed record AppPaths(string RootPath, string DatabasePath, string AuditLogPath, string BackupsPath)
{
    public static AppPaths Create(string? rootOverride = null)
    {
        var root = rootOverride ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NEXUS");
        Directory.CreateDirectory(root);
        var backups = Path.Combine(root, "Backups");
        Directory.CreateDirectory(backups);
        return new AppPaths(root, Path.Combine(root, "nexus.db"), Path.Combine(root, "security-audit.log"), backups);
    }
}
