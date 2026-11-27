using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace RetroRaycaster.Core.Tests;

public class WorldSpriteTests
{
    private const int Width = 128, Height = 96;
    private const uint Brick = 0xFF993311;
    private static readonly WallTexture Wall = new(new uint[,] { { Brick } });
    private static readonly string[] Room = ["....B", "....B", "....B", "....B"];

    private static GridMap Load(string[] rows, params (double X, double Y)[] lamps)
    {
        var objects = lamps.Select(p => new { type = "signalLamp", x = p.X, y = p.Y });
        var json = JsonSerializer.SerializeToUtf8Bytes(new { rows, objects, spawn = new { x = 0.5, y = 1.5, angleDegrees = 0 } });
        using var stream = new MemoryStream(json);
        return LevelLoader.Load(stream, Wall, Wall).Map;
    }

    private static uint[] Frame(GridMap map, double cameraX = 0.5)
    {
        var pixels = new uint[Width * Height];
        SoftwareRenderer.Render(map, cameraX, 1.5, 0, Math.PI / 2, Width, Height, pixels);
        return pixels;
    }

    [Fact]
    public void LegacyLevelHasNoObjects()
    {
        using var stream = new MemoryStream("""{"rows":["."],"spawn":{"x":0.5,"y":0.5,"angleDegrees":0}}"""u8.ToArray());
        Assert.True(LevelLoader.Load(stream, Wall, Wall).Map.Objects.IsEmpty);
    }

    [Theory]
    [InlineData("other", "1", "1")]
    [InlineData("signalLamp", "1e999", "1")]
    [InlineData("signalLamp", "1", "-0.1")]
    [InlineData("signalLamp", "2", "0")]
    [InlineData("signalLamp", "1", "0")]
    [InlineData("signalLamp", "1", "1")]
    public void RejectsUnknownTypeOrInvalidFloorPosition(string type, string x, string y)
    {
        var json = $$"""
            {"rows":[".B",".D"],"spawn":{"x":0.5,"y":0.5,"angleDegrees":0},
             "objects":[{"type":"{{type}}","x":{{x}},"y":{{y}}}]}
            """;
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        Assert.Throws<JsonException>(() => LevelLoader.Load(stream, Wall, Wall));
    }

    [Fact]
    public void WallEdgeHidesOnlyPartOfLamp()
    {
        string[] edge = ["....B", "....B", "..B.B", "....B"];
        var lamp = (3.5, 2.1);
        var clear = Frame(Load(Room));
        var visible = Frame(Load(Room, lamp));
        var wall = Frame(Load(edge));
        var clipped = Frame(Load(edge, lamp));
        int hidden = 0, shown = 0;
        for (int i = 0; i < clipped.Length; i++)
        {
            if (visible[i] == clear[i]) continue;
            // The near wall's corner is (3, 2), at slope 0.2 from the camera.
            if ((i % Width + 0.5 - Width / 2) / (Width / 2) > 0.2)
            {
                Assert.Equal(wall[i], clipped[i]);
                hidden++;
            }
            else
            {
                Assert.Equal(visible[i], clipped[i]);
                shown++;
            }
        }
        Assert.True(hidden > 0 && shown > 0);
    }

    [Fact]
    public void NearLampOverlaysFarLampAndItsTransparentTexelsPreserveTheBackground()
    {
        var near = (2.5, 1.55);
        var far = (3.5, 1.5);
        var background = Frame(Load(Room));
        var front = Frame(Load(Room, near));
        var back = Frame(Load(Room, far));
        var both = Frame(Load(Room, near, far));
        bool overlap = false, throughTransparentPixel = false;
        for (int i = 0; i < both.Length; i++)
        {
            bool opaque = front[i] != background[i];
            Assert.Equal(opaque ? front[i] : back[i], both[i]);
            if (back[i] == background[i]) continue;
            overlap |= opaque && front[i] != back[i];
            // Inside the nearer billboard's bounds, but outside its painted silhouette.
            throughTransparentPixel |= !opaque && i % Width is >= 58 and < 74 && i / Width is >= 40 and < 64;
        }
        Assert.True(overlap && throughTransparentPixel);
    }

    [Theory]
    [InlineData(1.6)]
    [InlineData(1.5)]
    [InlineData(1.4999999999)]
    public void LampBehindOrAtCameraPlaneIsCulled(double cameraX)
    {
        Assert.Equal(Frame(Load(Room), cameraX), Frame(Load(Room, (1.5, 1.5)), cameraX));
    }

    [Theory]
    [InlineData("..D.B", 3.5)]
    [InlineData("..D..", double.PositiveInfinity)]
    public void OpeningDoorRefreshesSameDepthAndPixelBuffers(string row, double openDepth)
    {
        // Door is x=2, lamp x=3.5, and the last cell is either wall or a ray miss.
        var map = Load([row, row, row], (3.5, 1.5));
        var pixels = new uint[Width * Height];
        var depths = new double[Width];
        void Render() => SoftwareRenderer.Render(map, 0.5, 1.5, 0, Math.PI / 2, Width, Height, pixels, depths);
        Render();
        Assert.Equal(1.5, depths[Width / 2]);
        Assert.DoesNotContain(0xFF22DBB0u, pixels);
        Assert.True(map.TryToggleDoor(new PlayerState(0.5, 1.5, 0)));
        Render();
        Assert.Equal(openDepth, depths[Width / 2]);
        Assert.Contains(0xFF22DBB0u, pixels);
    }
}
