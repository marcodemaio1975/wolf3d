using System;

namespace RetroRaycaster.Core;

/// <summary>Immutable ARGB texels indexed by row and column, starting at the top left.</summary>
public sealed class WallTexture
{
    private readonly uint[,] pixels;
    public int Width => pixels.GetLength(1);
    public int Height => pixels.GetLength(0);
    public uint this[int y, int x] => pixels[y, x];

    public WallTexture(uint[,] pixels)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        if (pixels.Length == 0)
            throw new ArgumentException("The texture must contain at least one texel.", nameof(pixels));
        this.pixels = (uint[,])pixels.Clone();
    }
}
