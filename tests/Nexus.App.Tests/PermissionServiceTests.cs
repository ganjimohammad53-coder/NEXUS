using Nexus.App.Security;
using Xunit;

namespace Nexus.App.Tests;

public sealed class PermissionServiceTests
{
    [Theory]
    [InlineData("File Manager", "delete-file", "C:\\temp\\important.txt")]
    [InlineData("File Manager", "rename-file", "C:\\temp\\a.txt -> C:\\temp\\b.txt")]
    [InlineData("Security", "restore-database", "backup:verified.db")]
    [InlineData("Productivity", "delete-task", "task:42")]
    public void IrreversibleOperationsFailClosedUntilIsolatedBrokerExists(string module, string action, string target)
    {
        var service = new LocalPermissionService();
        var request = new PermissionRequest(module, action, target, true);
        var grant = new ApprovalGrant(module, action, target, DateTime.UtcNow.AddMinutes(1), Guid.NewGuid());

        var result = service.Authorize(request, grant);

        Assert.False(result.Allowed);
        Assert.Contains("isolated security broker", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DeniesWhenApprovalMissing()
    {
        var service = new LocalPermissionService();
        var result = service.Authorize(new PermissionRequest("Files", "delete", "file:abc", true), null);
        Assert.False(result.Allowed);
    }

    [Fact]
    public void DeniesApprovalForDifferentTarget()
    {
        var service = new LocalPermissionService();
        var request = new PermissionRequest("Files", "delete", "file:target-a", true);
        var grant = new ApprovalGrant("Files", "delete", "file:target-b", DateTime.UtcNow.AddMinutes(1), Guid.NewGuid());
        Assert.False(service.Authorize(request, grant).Allowed);
    }

    [Fact]
    public void DeniesExpiredApproval()
    {
        var service = new LocalPermissionService();
        var request = new PermissionRequest("Files", "delete", "file:target", true);
        var grant = new ApprovalGrant("Files", "delete", "file:target", DateTime.UtcNow.AddSeconds(-1), Guid.NewGuid());
        Assert.False(service.Authorize(request, grant).Allowed);
    }

    [Fact]
    public void ApprovalCanOnlyBeUsedOnce()
    {
        var service = new LocalPermissionService();
        var request = new PermissionRequest("Security", "create-backup", "folder:backup");
        var grant = new ApprovalGrant(request.Module, request.Action, request.Target, DateTime.UtcNow.AddMinutes(1), Guid.NewGuid());
        Assert.True(service.Authorize(request, grant).Allowed);
        Assert.False(service.Authorize(request, grant).Allowed);
    }

    [Theory]
    [InlineData("AI Center", "send-online", "request:ai-payload")]
    [InlineData("Plugins", "execute-plugin", "plugin:unreviewed")]
    [InlineData("System", "system-elevate", "process:current")]
    [InlineData("Automation", "execute-automation", "recipe:one")]
    public void DisabledCapabilitiesRemainDeniedEvenWithApproval(string module, string action, string target)
    {
        var service = new LocalPermissionService();
        var request = new PermissionRequest(module, action, target, true);
        var grant = new ApprovalGrant(module, action, target, DateTime.UtcNow.AddMinutes(1), Guid.NewGuid());
        Assert.False(service.Authorize(request, grant).Allowed);
    }

    [Fact]
    public void RestoreRemainsBlockedUntilIsolatedBrokerExists()
    {
        var service = new LocalPermissionService();
        var request = new PermissionRequest("Security", "restore-database", "backup:verified.db", true);
        var grant = new ApprovalGrant(request.Module, request.Action, request.Target, DateTime.UtcNow.AddMinutes(1), Guid.NewGuid());

        var result = service.Authorize(request, grant);

        Assert.False(result.Allowed);
        Assert.Contains("isolated security broker", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DeniesApprovalWithEmptyNonce()
    {
        var service = new LocalPermissionService();
        var request = new PermissionRequest("Security", "restore-database", "backup:verified.db", true);
        var grant = new ApprovalGrant(request.Module, request.Action, request.Target, DateTime.UtcNow.AddMinutes(1), Guid.Empty);
        Assert.False(service.Authorize(request, grant).Allowed);
    }

    [Fact]
    public void DeniesApprovalWithExcessiveLifetime()
    {
        var service = new LocalPermissionService();
        var request = new PermissionRequest("Security", "create-backup", "folder:backup");
        var grant = new ApprovalGrant(request.Module, request.Action, request.Target, DateTime.UtcNow.AddMinutes(3), Guid.NewGuid());
        Assert.False(service.Authorize(request, grant).Allowed);
    }

    [Fact]
    public void DeniesApprovalWithChangedAction()
    {
        var service = new LocalPermissionService();
        var request = new PermissionRequest("Security", "restore", "database:live", true);
        var grant = new ApprovalGrant("Security", "backup", "database:live", DateTime.UtcNow.AddMinutes(1), Guid.NewGuid());
        Assert.False(service.Authorize(request, grant).Allowed);
    }
}
