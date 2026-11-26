using System;
using System.IO;
using System.Text;
using System.Text.Json;
using Xunit;

namespace RetroRaycaster.Core.Tests;

public class LevelLoaderTests
{
    private const uint Brick = 0xFFCC4422, Metal = 0xFF667788;
    private const string Spawn = """{"x":0.5,"y":0.5,"angleDegrees":0}""";

    private static (GridMap Map, PlayerState Spawn) Load(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return LevelLoader.Load(stream, new WallTexture(new uint[,] { { Brick } }),
            new WallTexture(new uint[,] { { Metal } }));
    }

    [Fact]
    public void LoadsRectangularMapWithBothTexturesAndConvertsHeading()
    {
        var (map, spawn) = Load("""{"rows":["..B","..M"],"spawn":{"x":1.5,"y":0.5,"angleDegrees":90}}""");
        Assert.Equal((3, 2), (map.Width, map.Height));
        Assert.Equal(new PlayerState(1.5, 0.5, Math.PI / 2), spawn);
        var pixels = new uint[1];
        for (int row = 0; row < 2; row++)
        {
            SoftwareRenderer.Render(map, spawn.X, row + 0.5, 0, Math.PI / 2, 1, 1, pixels);
            Assert.Equal(row == 0 ? Brick : Metal, pixels[0]);
        }
    }

    [Theory]
    [InlineData("[]", "rows")]
    [InlineData("""[""]""", "Row 1")]
    [InlineData("""[".",null]""", "Row 2")]
    [InlineData("""["..","."]""", "Row 2")]
    [InlineData("""["..",".?"]""", "'?' at row 2, column 2")]
    public void ReportsInvalidRows(string rows, string reason)
    {
        var error = Assert.Throws<JsonException>(() => Load($$"""{"rows":{{rows}},"spawn":{{Spawn}}}"""));
        Assert.Contains(reason, error.Message);
    }

    [Theory]
    [InlineData("""{"rows":["."]}""", "spawn")]
    [InlineData("""{"spawn":{}}""", "x")]
    [InlineData("""{"spawn":{"x":0,"y":0,"angleDegrees":0}}""", "rows")]
    [InlineData("""{"rows":["."],"spawn":{"x":0.5,"angleDegrees":0}}""", "y")]
    [InlineData("""{"rows":["."],"spawn":{"x":0.5,"y":0.5}}""", "angleDegrees")]
    [InlineData("""{"rows":["."],"spawn":{"x":0.5,"y":0.5,"angleDegrees":1e999}}""", "angleDegrees")]
    public void ReportsMissingOrInvalidField(string json, string field)
    {
        Assert.Contains(field, Assert.Throws<JsonException>(() => Load(json)).Message);
    }

    [Theory]
    [InlineData(0.1, 0.5)] // Circle crosses the outer boundary.
    [InlineData(1.5, 1.5)] // Center in the wall.
    [InlineData(0.9, 1.5)] // Center in free space, overlap at a face.
    [InlineData(0.9, 0.9)] // Only the corner intersects the circle.
    public void RejectsSpawnOverlaps(double x, double y)
    {
        Assert.Throws<JsonException>(() => Load(CornerLevel(x, y)));
    }

    [Theory]
    [InlineData(0.85, 0.85)] // Bounding box overlaps the wall, but the circle clears it.
    [InlineData(0.8, 1.5)] // Touching the wall is valid.
    public void AcceptsSpawnClearOfWall(double x, double y)
    {
        Assert.Equal(new PlayerState(x, y, 0), Load(CornerLevel(x, y)).Spawn);
    }

    private static string CornerLevel(double x, double y) =>
        JsonSerializer.Serialize(new { rows = new[] { "..", ".M" }, spawn = new { x, y, angleDegrees = 0 } });
}
