using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Nexus.App.Services;

/// <summary>Loopback-only receiver for Dota 2 Game State Integration. Stores only the latest bounded JSON snapshot.</summary>
public sealed class Dota2GameStateService : IAsyncDisposable
{
    public const int DefaultPort = 3001;
    public const string AuthToken = "nexus-dota2-local-token";
    private readonly object _sync = new();
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private string? _latestJson;
    private DateTimeOffset? _receivedAtUtc;
    private string? _lastError;

    public string? LatestJson { get { lock (_sync) return _latestJson; } }
    public DateTimeOffset? ReceivedAtUtc { get { lock (_sync) return _receivedAtUtc; } }
    public string? LastError { get { lock (_sync) return _lastError; } }
    public bool IsRunning { get { lock (_sync) return _listener != null; } }

    public void Start(int port = DefaultPort)
    {
        lock (_sync)
        {
            if (_listener != null) return;
            if (port is < 1024 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            _listener = listener;
            _cts = new CancellationTokenSource();
            _lastError = null;
            _loopTask = AcceptLoopAsync(listener, _cts.Token);
        }
    }

    public async Task StopAsync()
    {
        Task? loop;
        lock (_sync)
        {
            if (_listener == null) return;
            _cts?.Cancel(); _listener.Stop(); _listener = null;
            loop = _loopTask; _loopTask = null; _cts?.Dispose(); _cts = null;
        }
        if (loop != null) { try { await loop.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false); } catch { } }
    }

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await listener.AcceptTcpClientAsync(ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (SocketException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { lock (_sync) _lastError = "Dota 2 listener error: " + ex.GetType().Name; break; }
            _ = HandleClientAsync(client, ct);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        using (var linked = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            linked.CancelAfter(TimeSpan.FromSeconds(5));
            try
            {
                using var stream = client.GetStream();
                var header = new List<byte>(1024); var one = new byte[1];
                while (header.Count < 16 * 1024)
                {
                    var n = await stream.ReadAsync(one.AsMemory(0, 1), linked.Token).ConfigureAwait(false);
                    if (n == 0) return; header.Add(one[0]); var c = header.Count;
                    if (c >= 4 && header[c-4] == 13 && header[c-3] == 10 && header[c-2] == 13 && header[c-1] == 10) break;
                }
                var headerText = Encoding.ASCII.GetString(header.ToArray());
                var firstLine = headerText.Split("\r\n", StringSplitOptions.None).FirstOrDefault() ?? "";
                var length = 0;
                foreach (var line in headerText.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Skip(1))
                {
                    var sep = line.IndexOf(':');
                    if (sep > 0 && line[..sep].Trim().Equals("Content-Length", StringComparison.OrdinalIgnoreCase)) int.TryParse(line[(sep+1)..].Trim(), out length);
                }
                if (length is < 0 or > 256 * 1024) { await Reply(stream, 413, "Payload too large", linked.Token); return; }
                if (!firstLine.StartsWith("POST ", StringComparison.OrdinalIgnoreCase)) { await Reply(stream, 405, "POST only", linked.Token); return; }
                var body = new byte[length]; var offset = 0;
                while (offset < length) { var n = await stream.ReadAsync(body.AsMemory(offset), linked.Token).ConfigureAwait(false); if (n == 0) return; offset += n; }
                using var doc = JsonDocument.Parse(body);
                if (!HasValidToken(doc.RootElement)) { await Reply(stream, 403, "Invalid local token", linked.Token); return; }
                var json = JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
                lock (_sync) { _latestJson = json; _receivedAtUtc = DateTimeOffset.UtcNow; _lastError = null; }
                await Reply(stream, 200, "OK", linked.Token);
            }
            catch (OperationCanceledException) { }
            catch (JsonException) { lock (_sync) _lastError = "Dota 2 sent invalid JSON."; }
            catch (Exception ex) { lock (_sync) _lastError = "Dota 2 GSI request failed: " + ex.GetType().Name; }
        }
    }

    private static bool HasValidToken(JsonElement root) => root.ValueKind == JsonValueKind.Object &&
        root.TryGetProperty("auth", out var auth) && auth.ValueKind == JsonValueKind.Object &&
        auth.TryGetProperty("token", out var token) && token.ValueKind == JsonValueKind.String &&
        string.Equals(token.GetString(), AuthToken, StringComparison.Ordinal);

    private static async Task Reply(NetworkStream stream, int status, string message, CancellationToken ct)
    {
        var body = Encoding.UTF8.GetBytes(message);
        var head = Encoding.ASCII.GetBytes($"HTTP/1.1 {status} {status switch { 200 => "OK", 403 => "Forbidden", 405 => "Method Not Allowed", 413 => "Payload Too Large", _ => "Bad Request" }}\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Length: {body.Length}\r\nConnection: close\r\nCache-Control: no-store\r\n\r\n");
        await stream.WriteAsync(head, ct).ConfigureAwait(false); await stream.WriteAsync(body, ct).ConfigureAwait(false); await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    public static string BuildConfig() => "\"NEXUS Dota 2\"\n{\n  \"uri\" \"http://127.0.0.1:3001/\"\n  \"timeout\" \"5.0\"\n  \"buffer\" \"0.1\"\n  \"throttle\" \"0.1\"\n  \"heartbeat\" \"30.0\"\n  \"auth\"\n  {\n    \"token\" \"" + AuthToken + "\"\n  }\n  \"data\"\n  {\n    \"provider\" \"1\"\n    \"map\" \"1\"\n    \"player\" \"1\"\n    \"hero\" \"1\"\n    \"abilities\" \"1\"\n    \"items\" \"1\"\n    \"events\" \"1\"\n    \"buildings\" \"1\"\n  }\n}\n";

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
}
