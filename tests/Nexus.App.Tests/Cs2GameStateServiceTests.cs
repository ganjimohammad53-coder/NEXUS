using Nexus.App.Services;
using Xunit;

namespace Nexus.App.Tests;

public sealed class Cs2GameStateServiceTests
{
    [Fact]
    public void ParsesAvailableGsiFieldsAndLeavesMissingValuesNull()
    {
        const string payload = """
        {
          "provider": { "steamid": "76561198000000000" },
          "map": { "name": "de_mirage", "phase": "live", "round": 12, "team_ct": { "score": 7 }, "team_t": { "score": 5 } },
          "round": { "phase": "live" },
          "player": {
            "activity": "playing",
            "state": { "health": 83, "armor": 100, "money": 3200, "kills": 14, "deaths": 9, "assists": 3, "mvps": 2, "score": 19 },
            "weapons": { "weapon_ak47": { "name": "ak47", "state": "active" } }
          }
        }
        """;

        var snapshot = Cs2GameStateService.ParseSnapshot(payload);

        Assert.Equal("de_mirage", snapshot.MapName);
        Assert.Equal((int?)12, snapshot.MapRound);
        Assert.Equal((int?)7, snapshot.CtScore);
        Assert.Equal((int?)5, snapshot.TScore);
        Assert.Equal((int?)83, snapshot.Health);
        Assert.Equal((int?)14, snapshot.Kills);
        Assert.Equal("ak47", snapshot.ActiveWeapon);
        Assert.Equal((int?)100, snapshot.Armor); // Provided fields remain grounded in the payload.
    }

    [Fact]
    public void GsiConfigUsesLoopbackAndDoesNotRequestEnemyTelemetry()
    {
        var config = Cs2GameStateService.BuildConfig();
        Assert.Contains("http://127.0.0.1:3000/", config, StringComparison.Ordinal);
        Assert.Contains(Cs2GameStateService.AuthToken, config, StringComparison.Ordinal);
        Assert.Contains("\"allplayers_state\" \"0\"", config, StringComparison.Ordinal);
        Assert.Contains("\"allplayers_weapons\" \"0\"", config, StringComparison.Ordinal);
    }
}
