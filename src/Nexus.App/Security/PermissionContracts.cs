namespace Nexus.App.Security;

public sealed record PermissionRequest(string Module, string Action, string Target, bool IsIrreversible = false);
public sealed record ApprovalGrant(string Module, string Action, string Target, DateTime ExpiresUtc, Guid Nonce);
public sealed record AuthorizationDecision(bool Allowed, string Reason)
{
    public static AuthorizationDecision Denied(string reason) => new(false, reason);
    public static AuthorizationDecision Granted() => new(true, "Explicit approval validated.");
}

public interface IPermissionService
{
    AuthorizationDecision Authorize(PermissionRequest request, ApprovalGrant? grant);
}
