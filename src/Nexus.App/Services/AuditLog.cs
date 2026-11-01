using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Nexus.App.Services;

public sealed record AuditHealthResult(bool IsHealthy, int EventCount, string Status);

public sealed class AuditLog(string path)
{
    private readonly object _sync = new();
    private readonly string _path = Path.GetFullPath(path);

    public bool Record(string category, string result, string? target = null)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var targetHash = string.IsNullOrEmpty(target) ? "none" : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(target)))[..12];
            var line = $"{DateTime.UtcNow:O}\t{Clean(category)}\t{Clean(result)}\ttarget-sha256:{targetHash}{Environment.NewLine}";
            lock (_sync)
            {
                using var stream = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.Read);
                using var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true);
                writer.Write(line);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }
            return true;
        }
        catch
        {
            // Do not leak paths, exception text, or sensitive values. Callers must fail closed for sensitive actions.
            return false;
        }
    }

    /// <summary>
    /// Checks that the local audit file can be read and that every record matches the expected
    /// four-column format. This is a structural health check, not cryptographic tamper detection.
    /// </summary>
    public AuditHealthResult CheckHealth()
    {
        try
        {
            lock (_sync)
            {
                if (!File.Exists(_path))
                    return new AuditHealthResult(false, 0, "Audit log does not exist yet.");

                var count = 0;
                foreach (var line in File.ReadLines(_path, Encoding.UTF8))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var columns = line.Split('\t');
                    if (columns.Length != 4 ||
                        !DateTimeOffset.TryParse(columns[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _) ||
                        string.IsNullOrWhiteSpace(columns[1]) ||
                        string.IsNullOrWhiteSpace(columns[2]) ||
                        !IsValidTargetHash(columns[3]))
                    {
                        return new AuditHealthResult(false, count, "Audit log contains a malformed record.");
                    }
                    count++;
                }

                // Open without truncation to confirm the existing file is writable.
                using var stream = new FileStream(_path, FileMode.Open, FileAccess.Write, FileShare.Read);
                stream.Flush(flushToDisk: true);
                return new AuditHealthResult(true, count, "Audit log is readable, writable, and structurally valid.");
            }
        }
        catch
        {
            return new AuditHealthResult(false, 0, "Audit log could not be read or written.");
        }
    }

    private static bool IsValidTargetHash(string column)
    {
        const string prefix = "target-sha256:";
        if (!column.StartsWith(prefix, StringComparison.Ordinal)) return false;
        var hash = column[prefix.Length..];
        return hash == "none" || (hash.Length == 12 && hash.All(Uri.IsHexDigit));
    }

    private static string Clean(string value) => new string(value.Where(c => !char.IsControl(c)).Take(80).ToArray());
}
