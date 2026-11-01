using Nexus.App.Security;
using Xunit;

namespace Nexus.App.Tests;

public sealed class AiExecutionPolicyTests
{
    [Theory]
    [InlineData("infer-local")]
    [InlineData("run-local-model")]
    [InlineData("load-model")]
    [InlineData("download-model")]
    [InlineData("call-provider")]
    [InlineData("submit-prompt")]
    [InlineData("share-context")]
    [InlineData("ai-memory-write")]
    [InlineData("execute-ai-tool")]
    [InlineData("ai-file-access")]
    public void AiCapabilitiesAreRecognizedAsBlockedRegardlessOfModule(string action)
    {
        Assert.True(AiExecutionPolicy.IsAiAction(action));
        var request = new PermissionRequest("Productivity", action, "target:sample");
        var grant = new ApprovalGrant(request.Module, request.Action, request.Target, DateTime.UtcNow.AddMinutes(1), Guid.NewGuid());

        Assert.False(new LocalPermissionService().Authorize(request, grant).Allowed);
    }

    [Fact]
    public void AiCenterAlwaysDeniesEvenWithApproval()
    {
        var request = new PermissionRequest("AI Center", "anything", "request:test");
        var grant = new ApprovalGrant(request.Module, request.Action, request.Target, DateTime.UtcNow.AddMinutes(1), Guid.NewGuid());

        var decision = new LocalPermissionService().Authorize(request, grant);

        Assert.False(decision.Allowed);
        Assert.Contains("AI", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnknownNonAiActionIsNotMisclassifiedAsAi()
    {
        Assert.False(AiExecutionPolicy.IsAiAction("create-backup"));
        Assert.False(AiExecutionPolicy.IsAiAction(null));
        Assert.False(AiExecutionPolicy.IsAiAction(" "));
    }

    [Fact]
    public void LocalInferenceAllowsOnlyLoopbackEndpointAndKnownModel()
    {
        Assert.True(AiExecutionPolicy.EvaluateLocalInference(
            new Uri("http://127.0.0.1:11434/api/chat"), "qwen2.5:14b").Allowed);
        Assert.False(AiExecutionPolicy.EvaluateLocalInference(
            new Uri("http://192.168.1.10:11434/api/chat"), "qwen2.5:14b").Allowed);
        Assert.False(AiExecutionPolicy.EvaluateLocalInference(
            new Uri("http://127.0.0.1:11434/api/chat"), "untrusted-model").Allowed);
    }

    [Fact]
    public void WebSearchRequiresConsentAndFixedHttpsEndpoint()
    {
        var endpoint = new Uri("https://html.duckduckgo.com/html/?q=test");
        Assert.True(AiExecutionPolicy.EvaluateWebSearch(true, "test query", endpoint).Allowed);
        Assert.False(AiExecutionPolicy.EvaluateWebSearch(false, "test query", endpoint).Allowed);
        Assert.False(AiExecutionPolicy.EvaluateWebSearch(true, "", endpoint).Allowed);
        Assert.False(AiExecutionPolicy.EvaluateWebSearch(true, "test query", new Uri("http://html.duckduckgo.com/html/?q=test")).Allowed);
        Assert.False(AiExecutionPolicy.EvaluateWebSearch(true, "test query", new Uri("https://evil.example/html/?q=test")).Allowed);
    }

    [Fact]
    public void OnlineInferenceRequiresConsentKeyFixedHttpsEndpointAndAllowedModel()
    {
        var endpoint = new Uri("https://api.openai.com/v1/chat/completions");
        Assert.True(AiExecutionPolicy.EvaluateOnlineRequest(true, true, endpoint, "gpt-4.1-mini").Allowed);
        Assert.False(AiExecutionPolicy.EvaluateOnlineRequest(false, true, endpoint, "gpt-4.1-mini").Allowed);
        Assert.False(AiExecutionPolicy.EvaluateOnlineRequest(true, false, endpoint, "gpt-4.1-mini").Allowed);
        Assert.False(AiExecutionPolicy.EvaluateOnlineRequest(true, true, new Uri("http://api.openai.com/v1/chat/completions"), "gpt-4.1-mini").Allowed);
        Assert.False(AiExecutionPolicy.EvaluateOnlineRequest(true, true, new Uri("https://evil.example/v1/chat/completions"), "gpt-4.1-mini").Allowed);
        Assert.False(AiExecutionPolicy.EvaluateOnlineRequest(true, true, endpoint, "unknown-model").Allowed);
    }
}
