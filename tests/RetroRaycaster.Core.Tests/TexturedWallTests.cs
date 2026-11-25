using System;
using Xunit;

namespace RetroRaycaster.Core.Tests;

public class TexturedWallTests
{
    private static readonly WallTexture Pattern = new(new uint[,]
    {
        { 0xFF100000, 0xFF200000, 0xFF300000, 0xFF400000 },
        { 0xFF001000, 0xFF002000, 0xFF003000, 0xFF004000 },
        { 0xFF122434, 0xFF244668, 0xFF36689A, 0xFF488ACE },
        { 0xFF000010, 0xFF000020, 0xFF000030, 0xFF000040 }
    });

    [Theory]
    [InlineData(0, 0xFF36689Au)] // East: impact Y = 1.61, column 2.
    [InlineData(2, 0xFF488ACEu)] // West: impact Y = 1.21, mirrored column 3.
    [InlineData(1, 0xFF244567u)] // South: impact X = 1.09, mirrored column 3, shaded.
    [InlineData(3, 0xFF122334u)] // North: impact X = 1.49, column 1, shaded.
    public void ObliqueRaySamplesOrientedFace(int quarterTurns, uint expected)
    {
        var map = new GridMap(new int[,] { { 1, 1, 1 }, { 1, 0, 1 }, { 1, 1, 1 } }, [Pattern]);
        var pixels = new uint[5 * 3];
        SoftwareRenderer.Render(map, 1.35, 1.35, quarterTurns * Math.PI / 2, Math.PI / 2, 5, 3, pixels);
        Assert.Equal(expected, pixels[1 * 5 + 3]);
    }

    [Fact]
    public void AdjacentCellsSelectDifferentMaterialsInOneFrame()
    {
        var second = new WallTexture(new uint[,] { { 0xFFABCDEF } });
        var map = new GridMap(new int[,] { { 0, 1 }, { 0, 2 } }, [Pattern, second]);
        var pixels = new uint[3];
        SoftwareRenderer.Render(map, 0.5, 1, 0, Math.PI / 2, 3, 1, pixels);
        Assert.Equal(new uint[] { 0xFF36689A, 0xFFABCDEF, 0xFFABCDEF }, pixels);
    }

    [Fact]
    public void CloseWallShowsOnlyMiddleTexels()
    {
        var map = new GridMap(new int[,] { { 0, 1 } }, [Pattern]);
        var pixels = new uint[5 * 4];
        // The wall projects to ten rows. Only the four central rows fit on screen.
        SoftwareRenderer.Render(map, 0.75, 0.4, 0, Math.PI / 2, 5, 4, pixels);
        Assert.Equal(new uint[] { 0xFF002000, 0xFF002000, 0xFF244668, 0xFF244668 },
            new[] { pixels[2], pixels[7], pixels[12], pixels[17] });
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void EitherMaterialStopsMovement(int material)
    {
        var map = new GridMap(new int[,] { { 0, material, 0 } }, [Pattern, Pattern]);
        var player = PlayerMovement.Update(map, new PlayerState(0.5, 0.5, 0), new MovementInput(1, 0, 0), 1);
        Assert.Equal(1 - PlayerMovement.Radius, player.X, 10);
    }
}
