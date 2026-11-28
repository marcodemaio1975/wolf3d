using System.IO;
using System.Text;
using System.Text.Json;
using Xunit;

namespace RetroRaycaster.Core.Tests;

public class CampaignTests
{
    private static Campaign Read(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return Campaign.ReadManifest(stream);
    }

    private static (GridMap Map, PlayerState Spawn) Load(string name)
    {
        double x = name switch { "relay.json" => 1.5, "demo.json" => 0.5, _ => throw new FileNotFoundException(name) };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            rows = new[] { ".." }, spawn = new { x, y = 0.5, angleDegrees = 0 },
            objects = new[] { new { type = "levelExit", x, y = 0.5 } }
        });
        using var stream = new MemoryStream(bytes);
        var texture = new WallTexture(new uint[,] { { 0xFFFFFFFF } });
        return LevelLoader.Load(stream, texture, texture);
    }

    [Fact]
    public void ManifestOrderReplayAndWrapUseFreshLevels()
    {
        var campaign = Read("""{"levels":["relay.json","demo.json"]}""");
        var first = campaign.LoadLevel(false, Load);
        Assert.Equal(1.5, first.Spawn.X);
        Assert.Equal(1, campaign.LevelNumber);
        Assert.False(campaign.IsLastLevel);
        Assert.True(first.Map.TryCompleteLevel(first.Spawn));

        var second = campaign.LoadLevel(true, Load);
        Assert.Equal(0.5, second.Spawn.X);
        Assert.True(campaign.IsLastLevel);
        Assert.True(second.Map.TryCompleteLevel(second.Spawn));
        var replay = campaign.LoadLevel(false, Load);
        Assert.Equal(2, campaign.LevelNumber);
        Assert.Equal(second.Spawn, replay.Spawn);
        Assert.False(replay.Map.IsCompleted);
        Assert.True(replay.Map.TryCompleteLevel(replay.Spawn));

        var restart = campaign.LoadLevel(true, Load);
        Assert.Equal(1, campaign.LevelNumber);
        Assert.Equal(first.Spawn, restart.Spawn);
        Assert.False(restart.Map.IsCompleted);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SingleLevelCampaignRestartsItsOnlyLevel(bool advance)
    {
        var campaign = Read("""{"levels":["demo.json"]}""");
        var first = campaign.LoadLevel(false, Load);
        Assert.True(first.Map.TryCompleteLevel(first.Spawn));
        var next = campaign.LoadLevel(advance, Load);
        Assert.True(campaign.IsLastLevel);
        Assert.Equal(1, campaign.LevelNumber);
        Assert.False(next.Map.IsCompleted);
    }

    [Fact]
    public void FailedAdvanceKeepsCurrentLevelForReplay()
    {
        var campaign = Read("""{"levels":["relay.json","missing.json"]}""");
        var first = campaign.LoadLevel(false, Load);
        Assert.Throws<FileNotFoundException>(() => campaign.LoadLevel(true, Load));
        Assert.Equal(1, campaign.LevelNumber);
        Assert.Equal(first.Spawn, campaign.LoadLevel(false, Load).Spawn);
    }

    [Theory]
    [InlineData("""{"levels":[]}""")]
    [InlineData("""{"levels":[" "]}""")]
    [InlineData("{")]
    public void InvalidManifestReportsJsonError(string json) =>
        Assert.Throws<JsonException>(() => Read(json));
}
