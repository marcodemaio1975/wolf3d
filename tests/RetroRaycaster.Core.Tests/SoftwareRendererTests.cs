using System;
using Xunit;
using static RetroRaycaster.Core.SoftwareRenderer;

namespace RetroRaycaster.Core.Tests;

public class SoftwareRendererTests
{
    [Theory]
    [InlineData(3, 4, 16)]
    [InlineData(5, 7, 13)]
    public void FrontWallHasDepthScaledHeightAcrossEntireFrame(int wallX, int top, int bottom)
    {
        const int width = 48, height = 20;
        var cells = new bool[15, 6];
        for (int row = 0; row < cells.GetLength(0); row++)
            cells[row, wallX] = true;

        var pixels = new uint[width * height];
        Render(new GridMap(cells), 1, 7.5, 0, Math.PI / 2, width, height, pixels);

        for (int y = 0; y < height; y++)
        {
            uint expected = y < top ? SkyColor : y < bottom ? VerticalWallColor : FloorColor;
            for (int x = 0; x < width; x++)
                Assert.Equal(expected, pixels[y * width + x]);
        }
    }

    [Fact]
    public void SouthFacingCameraSamplesColumnCentersAndRepaintsMisses()
    {
        const int width = 4, height = 3;
        var cells = new bool[3, 5];
        cells[2, 4] = true;
        var pixels = new uint[width * height];
        Render(new GridMap(cells), 2.75, 0.25, Math.PI / 2, Math.PI / 2, width, height, pixels);

        // Facing south puts the wall to the left. Only the leftmost ray enters its north face.
        Assert.Equal(new uint[] {
            SkyColor, SkyColor, SkyColor, SkyColor,
            HorizontalWallColor, FloorColor, FloorColor, FloorColor,
            FloorColor, FloorColor, FloorColor, FloorColor
        }, pixels);

        Render(new GridMap(new bool[3, 5]), 2.75, 0.25, Math.PI / 2, Math.PI / 2, width, height, pixels);
        Assert.All(pixels[..width], color => Assert.Equal(SkyColor, color));
        Assert.All(pixels[width..], color => Assert.Equal(FloorColor, color));
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.0000000001)]
    public void WallAtCameraFillsOnlyTheFrame(double cameraX)
    {
        var pixels = new uint[17];
        var map = new GridMap(new bool[,] { { true, false } });
        Render(map, cameraX, 0.5, Math.PI, Math.PI / 2, 3, 5, pixels.AsSpan(1));

        Assert.Equal(0u, pixels[0]);
        Assert.All(pixels[1..16], color => Assert.Equal(VerticalWallColor, color));
        Assert.Equal(0u, pixels[16]);
    }
}
