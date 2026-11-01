namespace Nexus.App.Security;

/// <summary>
/// Explicit policy checks for NEXUS AI requests. Online calls require a per-message user confirmation,
/// an API key supplied for the current session, and the fixed HTTPS OpenAI endpoint. This is an
/// application-level guard, not an OS-enforced security boundary against a compromised process.
/// </summary>
public static class AiExecutionPolicy
{
    private static readonly HashSet<string> BlockedActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "infer-local", "run-local-model", "load-model", "download-model", "install-model",
        "send-online", "transmit-data", "call-provider", "submit-prompt", "share-context",
        "ai-memory-read", "ai-memory-write", "execute-ai-tool", "ai-file-access"
    };

    private static readonly HashSet<string> AllowedLocalModels = new(StringComparer.OrdinalIgnoreCase)
    {
        "qwen2.5:14b", "qwen3.5:4b"
    };

    private static readonly HashSet<string> AllowedOnlineModels = new(StringComparer.OrdinalIgnoreCase)
    {
        "gpt-4.1-mini", "gpt-4.1", "gpt-4o-mini"
    };

    public static bool IsAiAction(string? action) =>
        !string.IsNullOrWhiteSpace(action) && BlockedActions.Contains(action.Trim());

    /// <summary>Legacy permission service remains fail-closed for AI actions; provider requests use explicit methods below.</summary>
    public static AuthorizationDecision Evaluate(string? capability) =>
        AuthorizationDecision.Denied("AI actions must pass the provider-specific policy and explicit UI consent flow.");

    public static AuthorizationDecision EvaluateLocalInference(Uri? endpoint, string? model)
    {
        if (endpoint is null || !endpoint.IsAbsoluteUri || endpoint.Scheme != Uri.UriSchemeHttp ||
            !string.Equals(endpoint.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
            endpoint.Port != 11434 || endpoint.AbsolutePath != "/api/chat" ||
            !string.IsNullOrEmpty(endpoint.UserInfo) || !string.IsNullOrEmpty(endpoint.Query) || !string.IsNullOrEmpty(endpoint.Fragment))
            return AuthorizationDecision.Denied("Local AI is restricted to http://127.0.0.1:11434/api/chat.");
        if (string.IsNullOrWhiteSpace(model) || !AllowedLocalModels.Contains(model.Trim()))
            return AuthorizationDecision.Denied("The selected local model is not in the NEXUS allowlist.");
        return AuthorizationDecision.Granted();
    }


    /// <summary>Allows user-approved search only through the fixed DuckDuckGo HTML endpoint.</summary>
    public static AuthorizationDecision EvaluateWebSearch(bool explicitConsent, string? query, Uri? endpoint)
    {
        if (!explicitConsent)
            return AuthorizationDecision.Denied("Web search requires explicit confirmation for each message.");
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2 || query.Trim().Length > 500)
            return AuthorizationDecision.Denied("Web search query must contain between 2 and 500 characters.");
        if (endpoint is null || !endpoint.IsAbsoluteUri || endpoint.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(endpoint.Host, "html.duckduckgo.com", StringComparison.OrdinalIgnoreCase) ||
            endpoint.Port != 443 || endpoint.AbsolutePath != "/html/" ||
            !string.IsNullOrEmpty(endpoint.UserInfo) || !string.IsNullOrEmpty(endpoint.Fragment))
            return AuthorizationDecision.Denied("Web search is restricted to the fixed HTTPS DuckDuckGo HTML endpoint.");
        return AuthorizationDecision.Granted();
    }

    public static AuthorizationDecision EvaluateOnlineRequest(bool explicitPerMessageConsent, bool hasApiKey, Uri? endpoint, string? model = null)
    {
        if (!explicitPerMessageConsent)
            return AuthorizationDecision.Denied("Online AI requires explicit confirmation for every message.");
        if (!hasApiKey)
            return AuthorizationDecision.Denied("An OpenAI API key is required for online AI.");
        if (endpoint is null || !endpoint.IsAbsoluteUri || endpoint.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(endpoint.Host, "api.openai.com", StringComparison.OrdinalIgnoreCase) ||
            endpoint.Port != 443 || endpoint.AbsolutePath != "/v1/chat/completions" ||
            !string.IsNullOrEmpty(endpoint.UserInfo) || !string.IsNullOrEmpty(endpoint.Query) || !string.IsNullOrEmpty(endpoint.Fragment))
            return AuthorizationDecision.Denied("Online AI is restricted to the fixed HTTPS OpenAI API endpoint.");
        if (model is not null && !AllowedOnlineModels.Contains(model.Trim()))
            return AuthorizationDecision.Denied("The selected online model is not in the NEXUS allowlist.");
        return AuthorizationDecision.Granted();
    }
}
