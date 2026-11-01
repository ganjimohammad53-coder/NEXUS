using Nexus.App.Services;
using Xunit;

namespace Nexus.App.Tests;

public sealed class Dota2GameStateServiceTests
{
    [Fact]
    public void ConfigUsesLoopbackAndRequestsDotaGameStateFields()
    {
        var config = Dota2GameStateService.BuildConfig();
        Assert.Contains("http://127.0.0.1:3001/", config, StringComparison.Ordinal);
        Assert.Contains(Dota2GameStateService.AuthToken, config, StringComparison.Ordinal);
        Assert.Contains("\"hero\" \"1\"", config, StringComparison.Ordinal);
        Assert.Contains("\"abilities\" \"1\"", config, StringComparison.Ordinal);
        Assert.Contains("\"items\" \"1\"", config, StringComparison.Ordinal);
    }
}
