using System;
using System.IO;
using System.Text.Json;
using Xunit;

namespace RetroRaycaster.Core.Tests;

public class DoorTests
{
    private static readonly WallTexture Wall = new(new uint[,] { { 0xFF808080 } });
    private static readonly MovementInput Forward = new(1, 0, 0);

    private static (GridMap Map, PlayerState Spawn) Load(params string[] rows)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(new { rows, spawn = new { x = 0.5, y = 0.5, angleDegrees = 0 } });
        using var stream = new MemoryStream(json);
        return LevelLoader.Load(stream, Wall, Wall);
    }

    [Fact]
    public void DoorBlocksUntilOpenedAndCanBeClosedFromOtherSide()
    {
        var (map, spawn) = Load("..D..B");
        Assert.Equal(2, map.CastRay(spawn.X, spawn.Y, 1, 0)?.CellX);
        var blocked = PlayerMovement.Update(map, spawn, Forward, 1);
        Assert.Equal(2 - PlayerMovement.Radius, blocked.X, 10);

        Assert.True(map.TryToggleDoor(spawn)); // Exactly 1.5 units to the near face.
        Assert.Equal(5, map.CastRay(spawn.X, spawn.Y, 1, 0)?.CellX);
        var across = PlayerMovement.Update(map, spawn, Forward, 1) with { Angle = Math.PI };
        Assert.Equal(3.5, across.X, 10);

        Assert.True(map.TryToggleDoor(across));
        Assert.Equal(2, map.CastRay(across.X, across.Y, -1, 0)?.CellX);
        Assert.Equal(3 + PlayerMovement.Radius, PlayerMovement.Update(map, across, Forward, 1).X, 10);
    }

    [Theory]
    [InlineData("...D.")] // Beyond reach.
    [InlineData(".BD.")] // A brick wall hides a door within reach.
    public void UnreachableDoorRemainsClosed(string row)
    {
        var (map, spawn) = Load(row);
        Assert.False(map.TryToggleDoor(spawn));
        Assert.Equal(row.IndexOf('D'), map.CastRay(row.Length - 0.5, 0.5, -1, 0)?.CellX);
    }

    [Fact]
    public void RangeIsMeasuredAlongTheViewRay()
    {
        var (map, spawn) = Load("....", "..D.");
        Assert.False(map.TryToggleDoor(spawn with { Angle = Math.PI / 6 }));
        Assert.Equal(2, map.CastRay(spawn.X, spawn.Y, Math.Cos(Math.PI / 6), 0.5)?.CellX);
    }

    [Theory]
    [InlineData(1.5, 1.5, 0)] // Center in the doorway.
    [InlineData(0.9, 1.5, 0)] // Circle straddles the near face.
    [InlineData(0.9, 0.9, 45)] // Circle overlaps only a corner.
    public void OccupiedDoorCannotClose(double x, double y, double degrees)
    {
        var (map, _) = Load("...", ".D.");
        Assert.True(map.TryToggleDoor(new PlayerState(0.5, 1.5, 0)));
        Assert.False(map.TryToggleDoor(new PlayerState(x, y, degrees * Math.PI / 180)));
        Assert.Null(map.CastRay(0.5, 1.5, 1, 0));
    }

    [Fact]
    public void OpenDoorTakesPrecedenceOverClosedDoorBehindIt()
    {
        var (map, spawn) = Load(".DD.");
        Assert.True(map.TryToggleDoor(spawn));
        Assert.Equal(2, map.CastRay(spawn.X, spawn.Y, 1, 0)?.CellX);
        Assert.True(map.TryToggleDoor(spawn));
        Assert.Equal(1, map.CastRay(spawn.X, spawn.Y, 1, 0)?.CellX);
    }
}
