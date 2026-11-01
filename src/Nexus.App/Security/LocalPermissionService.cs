namespace Nexus.App.Security;

/// <summary>
/// Alpha-only policy gate. It validates a one-time, short-lived approval bound to the exact action and target.
/// This is intentionally isolated behind an interface, but is NOT a separate process and is not a security boundary
/// against a compromised application process. Sensitive capabilities remain limited in Alpha.
/// </summary>
public sealed class LocalPermissionService : IPermissionService
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid, DateTime> _consumed = [];

    private static readonly HashSet<string> DisabledActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "execute-plugin", "load-plugin", "system-elevate", "elevate", "execute-automation",
        "send-online", "transmit-data", "move-file"
    };

    public AuthorizationDecision Authorize(PermissionRequest request, ApprovalGrant? grant)
    {
        try
        {
            if (request is null || string.IsNullOrWhiteSpace(request.Module) || string.IsNullOrWhiteSpace(request.Action) || string.IsNullOrWhiteSpace(request.Target))
                return AuthorizationDecision.Denied("Malformed permission request.");
            if (string.Equals(request.Module, "AI Center", StringComparison.OrdinalIgnoreCase))
                return AiExecutionPolicy.Evaluate(request.Action);
            if (AiExecutionPolicy.IsAiAction(request.Action) || DisabledActions.Contains(request.Action))
                return AuthorizationDecision.Denied("This capability is disabled in NEXUS 1.0 and cannot be enabled by an approval token.");
            // A grant created inside the UI process is not an independent authorization boundary.
            // Until a separately installed, OS-isolated broker is available, fail closed for every
            // irreversible operation rather than treating a same-process token as sufficient.
            if (request.IsIrreversible)
                return AuthorizationDecision.Denied("Irreversible operations are temporarily disabled until the isolated security broker is installed.");
            if (grant is null) return AuthorizationDecision.Denied("Explicit user approval is required.");
            if (!string.Equals(request.Module, grant.Module, StringComparison.Ordinal) ||
                !string.Equals(request.Action, grant.Action, StringComparison.Ordinal) ||
                !string.Equals(request.Target, grant.Target, StringComparison.Ordinal))
                return AuthorizationDecision.Denied("Approval does not match the exact requested operation and target.");
            var now = DateTime.UtcNow;
            if (grant.ExpiresUtc <= now || grant.ExpiresUtc > now.AddMinutes(2))
                return AuthorizationDecision.Denied("Approval is expired or outside the permitted lifetime.");
            if (grant.Nonce == Guid.Empty) return AuthorizationDecision.Denied("Invalid approval token.");
            lock (_sync)
            {
                // Bound memory growth in a long-lived desktop process. Expired nonces can no longer
                // authorize anything, so they do not need to remain in the replay cache.
                foreach (var expired in _consumed.Where(pair => pair.Value <= now).Select(pair => pair.Key).ToArray())
                    _consumed.Remove(expired);
                if (_consumed.ContainsKey(grant.Nonce))
                    return AuthorizationDecision.Denied("Approval has already been used.");
                _consumed.Add(grant.Nonce, grant.ExpiresUtc);
            }
            return AuthorizationDecision.Granted();
        }
        catch
        {
            return AuthorizationDecision.Denied("Permission service failed closed.");
        }
    }
}
