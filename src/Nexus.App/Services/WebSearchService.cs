using System.Net;
using System.Text.RegularExpressions;

namespace Nexus.App.Services;

/// <summary>
/// Lightweight HTML search adapter. Sends only the approved query to DuckDuckGo HTML search;
/// it does not execute scripts, follow result redirects, or fetch result pages automatically.
/// Search result content is untrusted input and must never be treated as instructions.
/// </summary>
public sealed class WebSearchService
{
    private static readonly Uri SearchEndpoint = new("https://html.duckduckgo.com/html/");
    private static readonly HttpClient Http = new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(25)
    };

    static WebSearchService()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("NEXUS-Desktop/1.0 (user-initiated web search)");
        Http.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml");
    }

    public sealed record SearchResult(string Title, string Url, string Snippet);

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        query = (query ?? string.Empty).Trim();
        if (query.Length is < 2 or > 500)
            throw new ArgumentException("Search query must contain between 2 and 500 characters.", nameof(query));

        var uri = new Uri(SearchEndpoint, "?q=" + Uri.EscapeDataString(query));
        using var response = await Http.GetAsync(uri, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Search provider returned HTTP {(int)response.StatusCode}.");

        var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var anchors = Regex.Matches(html,
            "<a\\b(?=[^>]*\\bclass=[\\\"'][^\\\"']*\\bresult__a\\b[^\\\"']*[\\\"'])[^>]*href=[\\\"'](?<url>[^\\\"']+)[\\\"'][^>]*>(?<title>.*?)</a>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var snippets = Regex.Matches(html,
            "<a\\b(?=[^>]*\\bclass=[\\\"'][^\\\"']*\\bresult__snippet\\b[^\\\"']*[\\\"'])[^>]*>(?<snippet>.*?)</a>|<div\\b(?=[^>]*\\bclass=[\\\"'][^\\\"']*\\bresult__snippet\\b[^\\\"']*[\\\"'])[^>]*>(?<snippet2>.*?)</div>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        var results = new List<SearchResult>();
        for (var i = 0; i < anchors.Count && results.Count < 5; i++)
        {
            var anchor = anchors[i];
            var title = CleanText(anchor.Groups["title"].Value);
            var url = ResolveResultUrl(WebUtility.HtmlDecode(anchor.Groups["url"].Value));
            var snippet = i < snippets.Count
                ? CleanText(snippets[i].Groups["snippet"].Success ? snippets[i].Groups["snippet"].Value : snippets[i].Groups["snippet2"].Value)
                : string.Empty;

            if (title.Length == 0 || !Uri.TryCreate(url, UriKind.Absolute, out var resultUri) ||
                (resultUri.Scheme != Uri.UriSchemeHttps && resultUri.Scheme != Uri.UriSchemeHttp) ||
                !string.IsNullOrEmpty(resultUri.UserInfo))
                continue;

            if (results.Any(r => string.Equals(r.Url, resultUri.AbsoluteUri, StringComparison.OrdinalIgnoreCase))) continue;
            results.Add(new SearchResult(title, resultUri.AbsoluteUri, snippet));
        }
        return results;
    }

    private static string ResolveResultUrl(string raw)
    {
        if (raw.StartsWith("//", StringComparison.Ordinal)) raw = "https:" + raw;
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri)) return string.Empty;
        if (uri.Host.EndsWith("duckduckgo.com", StringComparison.OrdinalIgnoreCase) && uri.AbsolutePath == "/l/")
        {
            var encoded = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(p => p.StartsWith("uddg=", StringComparison.OrdinalIgnoreCase));
            if (encoded is not null)
            {
                var value = encoded[(encoded.IndexOf('=') + 1)..];
                try { raw = Uri.UnescapeDataString(value.Replace("+", "%20", StringComparison.Ordinal)); }
                catch { return string.Empty; }
            }
        }
        return Uri.TryCreate(raw, UriKind.Absolute, out var resolved) &&
               (resolved.Scheme == Uri.UriSchemeHttp || resolved.Scheme == Uri.UriSchemeHttps) &&
               string.IsNullOrEmpty(resolved.UserInfo) ? resolved.AbsoluteUri : string.Empty;
    }

    private static string CleanText(string html)
    {
        var withoutTags = Regex.Replace(html, "<[^>]+>", " ");
        return Regex.Replace(WebUtility.HtmlDecode(withoutTags), "\\s+", " ").Trim();
    }
}
