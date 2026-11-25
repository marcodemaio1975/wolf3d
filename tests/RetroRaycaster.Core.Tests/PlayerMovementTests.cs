using System;
using Xunit;

namespace RetroRaycaster.Core.Tests;

public class PlayerMovementTests
{
    private static readonly GridMap OpenMap = new(new bool[8, 8]);

    [Theory]
    [InlineData(0, 1, 0, 3.5, 2)]
    [InlineData(Math.PI / 2, 1, 0, 2, 3.5)]
    [InlineData(Math.PI / 2, 0, 1, 0.5, 2)]
    [InlineData(0, -0.5, 0, 1.25, 2)]
    public void MovesRelativeToHeading(double angle, double forward, double strafe, double x, double y)
    {
        var result = PlayerMovement.Update(OpenMap, new(2, 2, angle), new(forward, strafe, 0), 0.5);
        Assert.Equal(x, result.X, 10);
        Assert.Equal(y, result.Y, 10);
        Assert.Equal(angle, result.Angle);
    }

    [Fact]
    public void PositiveTurnFacesSouthBeforeMoving()
    {
        var result = PlayerMovement.Update(OpenMap, new(2, 2, 0), new(1, 0, 1), 0.5);
        Assert.Equal(Math.PI / 2, result.Angle, 10);
        Assert.Equal(2, result.X, 10);
        Assert.Equal(3.5, result.Y, 10);
    }

    [Fact]
    public void DiagonalSpeedMatchesStraightLineSpeed()
    {
        var result = PlayerMovement.Update(OpenMap, new(2, 2, 0), new(1, 1, 0), 1);
        double expected = 2 + PlayerMovement.CellsPerSecond / Math.Sqrt(2);
        Assert.Equal(expected, result.X, 10);
        Assert.Equal(expected, result.Y, 10);
    }

    [Theory]
    [InlineData(2.75, 0.1)]
    [InlineData(0.5, 2)]
    public void StopsOneRadiusBeforeWallEvenWhenEndpointWouldBeBeyondIt(double x, double seconds)
    {
        var cells = new bool[5, 8];
        cells[2, 3] = true;
        var result = PlayerMovement.Update(new(cells), new(x, 2.5, 0), new(1, 0, 0), seconds);
        Assert.Equal(2.8, result.X, 10);
        Assert.Equal(2.5, result.Y);
    }

    [Fact]
    public void SlidesAlongWallAtTangentialSpeed()
    {
        var cells = new bool[8, 6];
        for (int y = 0; y < 8; y++) cells[y, 3] = true;
        var result = PlayerMovement.Update(new(cells), new(2.5, 2, 0), new(1, 1, 0), 1);
        Assert.Equal(2.8, result.X, 10);
        Assert.Equal(2 + PlayerMovement.CellsPerSecond / Math.Sqrt(2), result.Y, 10);
    }

    [Fact]
    public void CircleTouchesWallCorner()
    {
        var cells = new bool[5, 5];
        cells[2, 3] = true;
        var result = PlayerMovement.Update(new(cells), new(2.5, 1.88, 0), new(1, 0, 0), 1);
        // Offsets of 0.12 and 0.16 place the centre exactly 0.2 from the corner.
        Assert.Equal(2.84, result.X, 10);
    }

    [Theory]
    [InlineData(-1, 0, 0.2, 2)]
    [InlineData(1, 0, 7.8, 2)]
    [InlineData(0, -1, 2, 0.2)]
    [InlineData(0, 1, 2, 7.8)]
    public void MapEdgesContainEntireCircle(double forward, double strafe, double x, double y)
    {
        var result = PlayerMovement.Update(OpenMap, new(2, 2, 0), new(forward, strafe, 0), 10);
        Assert.Equal(x, result.X, 10);
        Assert.Equal(y, result.Y, 10);
    }
}
