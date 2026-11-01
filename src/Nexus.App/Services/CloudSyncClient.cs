using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Nexus.App.Services;

/// <summary>
/// Optional cloud API client. Existing local-first features remain usable when this client is not configured.
/// Access tokens are held in memory only; callers should not persist them to the local database or logs.
/// </summary>
public sealed class CloudSyncClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsClient;
    private string? _accessToken;

    public CloudSyncClient(Uri baseAddress, HttpClient? httpClient = null)
    {
        if (baseAddress is null || !baseAddress.IsAbsoluteUri || baseAddress.Scheme != Uri.UriSchemeHttps && !IsLoopback(baseAddress))
            throw new ArgumentException("Use HTTPS for remote servers. HTTP is allowed only for loopback development.", nameof(baseAddress));

        _ownsClient = httpClient is null;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        _http.BaseAddress = baseAddress;
    }

    public bool IsSignedIn => !string.IsNullOrWhiteSpace(_accessToken);

    public async Task<CloudUser> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync("api/auth/login", new { email, password }, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new InvalidOperationException("Email or password is incorrect.");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("The server returned an empty sign-in response.");
        _accessToken = result.AccessToken;
        return result.User;
    }

    public async Task<CloudUser> RegisterAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync("api/auth/register", new { email, password }, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("The server returned an empty registration response.");
        _accessToken = result.AccessToken;
        return result.User;
    }

    public async Task<IReadOnlyList<CloudSyncDocument>> GetDocumentsAsync(CancellationToken cancellationToken = default)
    {
        using var request = CreateAuthorizedRequest(HttpMethod.Get, "api/sync");
        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<CloudSyncDocument>>(cancellationToken: cancellationToken) ?? [];
    }

    public async Task<CloudSyncDocumentResult> PutDocumentAsync(string key, JsonElement payload, long expectedRevision, CancellationToken cancellationToken = default)
    {
        using var request = CreateAuthorizedRequest(HttpMethod.Put, $"api/sync/{Uri.EscapeDataString(key)}");
        request.Content = JsonContent.Create(new { payload = payload.GetRawText(), expectedRevision });
        using var response = await _http.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new CloudSyncConflictException("The cloud copy changed since it was read. Fetch the latest revision before retrying.");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CloudSyncDocumentResult>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("The server returned an empty sync response.");
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (IsSignedIn)
            {
                using var request = CreateAuthorizedRequest(HttpMethod.Post, "api/auth/logout");
                using var response = await _http.SendAsync(request, cancellationToken);
            }
        }
        finally
        {
            _accessToken = null;
        }
    }

    private HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string path)
    {
        if (string.IsNullOrWhiteSpace(_accessToken)) throw new InvalidOperationException("Sign in before using cloud sync.");
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        return request;
    }

    private static bool IsLoopback(Uri uri) => uri.IsLoopback;

    public void Dispose()
    {
        if (_ownsClient) _http.Dispose();
        _accessToken = null;
    }

    private sealed record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt, CloudUser User);
}

public sealed record CloudUser(Guid Id, string Email, string Role);
public sealed record CloudSyncDocument(string Key, string Payload, long Revision, DateTimeOffset UpdatedAt);
public sealed record CloudSyncDocumentResult(string Key, long Revision, DateTimeOffset UpdatedAt);

public sealed class CloudSyncConflictException(string message) : Exception(message);
