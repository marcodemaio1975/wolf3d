using System.IO;
using System.Text.Json;
using Xunit;

namespace RetroRaycaster.Core.Tests;

public class LevelExitTests
{
    private static readonly PlayerState ExitTile = new(2.1, 0.9, 0);
    private static readonly PlayerState LastCell = new(3.5, 0.5, 0);
    private static readonly WallTexture Wall = new(new uint[,] { { 0xFF888888 } });

    private static (GridMap Map, PlayerState Spawn) Load(params (string Type, double X)[] entries)
    {
        var objects = System.Array.ConvertAll(entries, entry => new { type = entry.Type, x = entry.X, y = 0.5 });
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            rows = new[] { ".....", ".D..." }, objects, spawn = new { x = 0.5, y = 1.5, angleDegrees = 0 }
        });
        using var stream = new MemoryStream(bytes);
        return LevelLoader.Load(stream, Wall, Wall);
    }

    [Fact]
    public void PortalLightsAfterLastPickupAndCompletesOnceOnEntry()
    {
        var (map, spawn) = Load(("energyCell", 0.5), ("levelExit", 2.5), ("energyCell", 3.5));
        var dormant = map.Objects[1].Texture;
        Assert.False(map.TryCompleteLevel(ExitTile));
        Assert.True(map.CollectEnergyCells(new PlayerState(0.5, 0.5, 0)));
        Assert.False(map.IsExitActive);
        Assert.False(map.TryCompleteLevel(ExitTile));
        Assert.True(map.CollectEnergyCells(LastCell));
        Assert.True(map.IsExitActive);
        Assert.NotSame(dormant, map.Objects[0].Texture);
        Assert.False(map.TryCompleteLevel(LastCell));
        Assert.False(map.TryCompleteLevel(spawn with { X = ExitTile.X }));
        Assert.False(map.IsCompleted);
        Assert.True(map.TryCompleteLevel(ExitTile));
        Assert.True(map.IsCompleted);
        Assert.False(map.TryCompleteLevel(ExitTile));
    }

    [Fact]
    public void ReloadRestoresSpawnDoorEnergyAndCompletion()
    {
        var entries = new[] { ("energyCell", 2.1), ("levelExit", 2.8) };
        var (map, spawn) = Load(entries);
        Assert.True(map.TryToggleDoor(spawn));
        Assert.Null(map.CastRay(spawn.X, spawn.Y, 1, 0));
        Assert.True(map.CollectEnergyCells(ExitTile));
        Assert.True(map.TryCompleteLevel(ExitTile)); // Pickup and exit share a tile.

        var fresh = Load(entries);
        Assert.Equal(new PlayerState(0.5, 1.5, 0), fresh.Spawn);
        Assert.False(fresh.Map.IsCompleted);
        Assert.False(fresh.Map.IsExitActive);
        Assert.Equal(0, fresh.Map.CollectedEnergyCells);
        Assert.Equal(1, fresh.Map.TotalEnergyCells);
        Assert.Equal(1, fresh.Map.CastRay(spawn.X, spawn.Y, 1, 0)?.CellX);
    }

    [Fact]
    public void ExitWithoutCellsStartsActive()
    {
        var (map, spawn) = Load(("levelExit", 2.5));
        Assert.True(map.IsExitActive);
        Assert.False(map.TryCompleteLevel(spawn));
        Assert.True(map.TryCompleteLevel(ExitTile));
    }

    [Fact]
    public void LevelWithoutExitContinuesAfterLastPickup()
    {
        var (map, _) = Load(("energyCell", 3.5));
        Assert.True(map.CollectEnergyCells(LastCell));
        Assert.False(map.IsExitActive);
        Assert.False(map.TryCompleteLevel(LastCell));
        Assert.False(map.IsCompleted);
    }

    [Fact]
    public void SecondExitReportsOffendingEntry()
    {
        var error = Assert.Throws<JsonException>(() => Load(("levelExit", 1.5), ("levelExit", 2.5)));
        Assert.Contains("Second levelExit at objects[1]", error.Message);
    }
}
