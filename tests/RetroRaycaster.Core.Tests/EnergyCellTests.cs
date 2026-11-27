using System;
using System.IO;
using System.Text.Json;
using Xunit;

namespace RetroRaycaster.Core.Tests;

public class EnergyCellTests
{
    private static readonly WallTexture Wall = new(new uint[,] { { 0xFF555555 } });

    private static GridMap Load(params (string Type, double X, double Y)[] entries)
    {
        var objects = Array.ConvertAll(entries, e => new { type = e.Type, x = e.X, y = e.Y });
        var json = JsonSerializer.SerializeToUtf8Bytes(new
        {
            rows = new[] { "....", "....", "...." },
            spawn = new { x = 0.5, y = 1.5, angleDegrees = 0 },
            objects
        });
        using var stream = new MemoryStream(json);
        return LevelLoader.Load(stream, Wall, Wall).Map;
    }

    [Theory]
    [InlineData(2.1, 1.1, -1, 0)] // Walk backward into the cell.
    [InlineData(1.1, 0.9, 0, 1)]  // Strafe into the cell.
    public void EnteringTileCollectsOnce(double x, double y, double forward, double strafe)
    {
        var map = Load(("energyCell", 1.99, 1.99));
        var player = new PlayerState(x, y, 0);
        Assert.False(map.CollectEnergyCells(player));
        Assert.Equal(0, map.CollectedEnergyCells);
        player = PlayerMovement.Update(map, player, new MovementInput(forward, strafe, 0), 0.1);
        Assert.True(map.CollectEnergyCells(player));
        Assert.False(map.CollectEnergyCells(player));
        Assert.Equal(1, map.CollectedEnergyCells);
        Assert.True(map.Objects.IsEmpty);
    }

    [Fact]
    public void CollectingBothCellsPreservesLampAndReloadRestoresPickups()
    {
        var entries = new[] { ("energyCell", 1.1, 1.1), ("signalLamp", 1.5, 1.5), ("energyCell", 2.5, 1.5) };
        var map = Load(entries);
        Assert.Equal(2, map.TotalEnergyCells);
        Assert.True(map.CollectEnergyCells(new PlayerState(1.5, 1.5, 0)));
        Assert.Equal(1, map.CollectedEnergyCells);
        Assert.True(map.CollectEnergyCells(new PlayerState(2.5, 1.5, 0)));
        Assert.Equal(2, map.CollectedEnergyCells);
        Assert.Equal(1.5, Assert.Single(map.Objects.ToArray()).X);

        var fresh = Load(entries);
        Assert.Equal(0, fresh.CollectedEnergyCells);
        Assert.Equal(2, fresh.TotalEnergyCells);
        Assert.Equal(3, fresh.Objects.Length);
        Assert.True(fresh.CollectEnergyCells(new PlayerState(1.5, 1.5, 0)));
    }

    [Fact]
    public void NextFrameShowsOnlyRemainingSprites()
    {
        var lamp = ("signalLamp", 3.1, 1.5);
        var map = Load(("energyCell", 1.9, 1.5), lamp);
        var expected = new uint[128 * 96];
        var pixels = new uint[expected.Length];
        var depths = new double[128];
        void Draw(GridMap scene, uint[] buffer) =>
            SoftwareRenderer.Render(scene, 0.5, 1.5, 0, Math.PI / 2, 128, 96, buffer, depths);

        var lampOnly = Load(lamp);
        Assert.Equal(0, lampOnly.TotalEnergyCells);
        Draw(lampOnly, expected);
        Draw(map, pixels);
        Assert.False(expected.AsSpan().SequenceEqual(pixels));
        map.CollectEnergyCells(new PlayerState(1.1, 1.1, 0));
        Draw(map, pixels);
        Assert.Equal(expected, pixels);
    }
}
