using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Nexus.App.Services;

/// <summary>
/// Receives Counter-Strike 2 Game State Integration (GSI) updates on loopback only.
/// It never reads game memory, injects code, or communicates with game servers.
/// </summary>
public sealed class Cs2GameStateService : IAsyncDisposable
{
    public const int DefaultPort = 3000;
    public const string AuthToken = "nexus-cs2-local-token";
    private readonly object _sync = new();
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private Cs2Snapshot? _latest;
    private string? _lastError;

    public Cs2Snapshot? Latest { get { lock (_sync) return _latest; } }
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
            _cts?.Cancel();
            _listener.Stop();
            _listener = null;
            loop = _loopTask;
            _loopTask = null;
            _cts?.Dispose();
            _cts = null;
        }
        if (loop != null)
        {
            try { await loop.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false); }
            catch (Exception) { /* stop is best-effort and remains safe */ }
        }
    }

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (SocketException) when (cancellationToken.IsCancellationRequested) { break; }
            catch (Exception ex) { lock (_sync) _lastError = "Listener error: " + ex.GetType().Name; break; }
            _ = HandleClientAsync(client, cancellationToken);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        using (var requestCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            requestCts.CancelAfter(TimeSpan.FromSeconds(5));
            var requestToken = requestCts.Token;
            try
            {
                client.ReceiveTimeout = 3000;
                client.SendTimeout = 3000;
                using var stream = client.GetStream();
                var headerBytes = new List<byte>(1024);
                var one = new byte[1];
                while (headerBytes.Count < 16 * 1024)
                {
                    var read = await stream.ReadAsync(one.AsMemory(0, 1), requestToken).ConfigureAwait(false);
                    if (read == 0) return;
                    headerBytes.Add(one[0]);
                    var n = headerBytes.Count;
                    if (n >= 4 && headerBytes[n - 4] == 13 && headerBytes[n - 3] == 10 && headerBytes[n - 2] == 13 && headerBytes[n - 1] == 10) break;
                }
                var headerText = Encoding.ASCII.GetString(headerBytes.ToArray());
                var firstLine = headerText.Split("\r\n", StringSplitOptions.None).FirstOrDefault() ?? "";
                var contentLength = 0;
                foreach (var line in headerText.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Skip(1))
                {
                    var separator = line.IndexOf(':');
                    if (separator > 0 && line[..separator].Trim().Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                        int.TryParse(line[(separator + 1)..].Trim(), out contentLength);
                }
                if (contentLength is < 0 or > 256 * 1024)
                {
                    await WriteResponseAsync(stream, 413, "Payload too large", requestToken).ConfigureAwait(false);
                    return;
                }
                if (!firstLine.StartsWith("POST ", StringComparison.OrdinalIgnoreCase))
                {
                    await WriteResponseAsync(stream, 405, "POST only", requestToken).ConfigureAwait(false);
                    return;
                }
                var body = new byte[contentLength];
                var offset = 0;
                while (offset < body.Length)
                {
                    var read = await stream.ReadAsync(body.AsMemory(offset), requestToken).ConfigureAwait(false);
                    if (read == 0) return;
                    offset += read;
                }
                using var document = JsonDocument.Parse(body);
                if (!HasValidToken(document.RootElement))
                {
                    await WriteResponseAsync(stream, 403, "Invalid local token", requestToken).ConfigureAwait(false);
                    return;
                }
                var snapshot = ParseSnapshot(document.RootElement);
                lock (_sync) { _latest = snapshot; _lastError = null; }
                await WriteResponseAsync(stream, 200, "OK", requestToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (JsonException) { lock (_sync) _lastError = "CS2 sent an invalid JSON payload."; }
            catch (Exception ex) { lock (_sync) _lastError = "GSI request failed: " + ex.GetType().Name; }
        }
    }

    private static bool HasValidToken(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object &&
        root.TryGetProperty("auth", out var auth) && auth.ValueKind == JsonValueKind.Object &&
        auth.TryGetProperty("token", out var token) && token.ValueKind == JsonValueKind.String &&
        string.Equals(token.GetString(), AuthToken, StringComparison.Ordinal);

    private static async Task WriteResponseAsync(NetworkStream stream, int status, string text, CancellationToken ct)
    {
        var body = Encoding.UTF8.GetBytes(text);
        var header = Encoding.ASCII.GetBytes($"HTTP/1.1 {status} {Reason(status)}\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Length: {body.Length}\r\nConnection: close\r\nCache-Control: no-store\r\n\r\n");
        await stream.WriteAsync(header, ct).ConfigureAwait(false);
        await stream.WriteAsync(body, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    private static string Reason(int status) => status switch { 200 => "OK", 403 => "Forbidden", 405 => "Method Not Allowed", 413 => "Payload Too Large", _ => "Bad Request" };

    public static Cs2Snapshot ParseSnapshot(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return ParseSnapshot(doc.RootElement);
    }

    private static Cs2Snapshot ParseSnapshot(JsonElement root)
    {
        var provider = Child(root, "provider");
        var map = Child(root, "map");
        var round = Child(root, "round");
        var player = Child(root, "player");
        var state = Child(player, "state");
        var teamCt = Child(map, "team_ct");
        var teamT = Child(map, "team_t");
        var weapons = Child(player, "weapons");
        string? activeWeapon = null;
        if (weapons.ValueKind == JsonValueKind.Object)
        {
            foreach (var weapon in weapons.EnumerateObject())
            {
                if (Child(weapon.Value, "state").ValueKind == JsonValueKind.String &&
                    string.Equals(Child(weapon.Value, "state").GetString(), "active", StringComparison.OrdinalIgnoreCase))
                { activeWeapon = StringValue(Child(weapon.Value, "name")) ?? weapon.Name; break; }
            }
        }
        return new Cs2Snapshot(
            DateTimeOffset.UtcNow,
            StringValue(Child(provider, "steamid")),
            StringValue(Child(map, "name")),
            StringValue(Child(map, "phase")),
            IntValue(Child(map, "round")),
            StringValue(Child(round, "phase")),
            IntValue(Child(teamCt, "score")),
            IntValue(Child(teamT, "score")),
            StringValue(Child(player, "activity")),
            IntValue(Child(state, "health")),
            IntValue(Child(state, "armor")),
            IntValue(Child(state, "money")),
            IntValue(Child(state, "kills")),
            IntValue(Child(state, "deaths")),
            IntValue(Child(state, "assists")),
            IntValue(Child(state, "mvps")),
            IntValue(Child(state, "score")),
            activeWeapon,
            DateTimeOffset.UtcNow);
    }

    private static JsonElement Child(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var child) ? child : default;
    private static string? StringValue(JsonElement e) => e.ValueKind == JsonValueKind.String ? e.GetString() : null;
    private static int? IntValue(JsonElement e) => e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out var value) ? value : null;

    public static string BuildConfig() => "\"NEXUS\"\n{\n  \"uri\" \"http://127.0.0.1:3000/\"\n  \"timeout\" \"5.0\"\n  \"buffer\" \"0.1\"\n  \"throttle\" \"0.1\"\n  \"heartbeat\" \"30.0\"\n  \"auth\"\n  {\n    \"token\" \"" + AuthToken + "\"\n  }\n  \"data\"\n  {\n    \"provider\" \"1\"\n    \"map\" \"1\"\n    \"round\" \"1\"\n    \"player_id\" \"1\"\n    \"player_state\" \"1\"\n    \"player_weapons\" \"1\"\n    \"allplayers_id\" \"0\"\n    \"allplayers_state\" \"0\"\n    \"allplayers_weapons\" \"0\"\n    \"allplayers_match_stats\" \"0\"\n  }\n}\n";

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
}

public sealed record Cs2Snapshot(
    DateTimeOffset ReceivedAtUtc,
    string? SteamId,
    string? MapName,
    string? MapPhase,
    int? MapRound,
    string? RoundPhase,
    int? CtScore,
    int? TScore,
    string? Activity,
    int? Health,
    int? Armor,
    int? Money,
    int? Kills,
    int? Deaths,
    int? Assists,
    int? Mvps,
    int? PlayerScore,
    string? ActiveWeapon,
    DateTimeOffset Timestamp);
