using System;

namespace RetroRaycaster.Core;

public static class SoftwareRenderer
{
    public const uint SkyColor = 0xFF779BC0;
    public const uint FloorColor = 0xFF453F39;
    public const uint VerticalWallColor = 0xFFBC9270;
    public const uint HorizontalWallColor = 0xFF896A51;

    /// <summary>
    /// Renders unit-height walls with the camera half a grid unit above the floor.
    /// Camera coordinates are in grid units. Angles are in radians, with zero facing
    /// east and pi/2 facing south. The horizontal field of view must be between 0 and pi.
    /// </summary>
    /// <param name="pixels">Caller-owned buffer of at least width * height pixels.
    /// Each uint is packed as 0xAARRGGBB (BGRA bytes on little-endian machines).
    /// Rows run top to bottom, with pixel (x, y) at y * width + x.</param>
    public static void Render(GridMap map, double cameraX, double cameraY, double angle,
        double horizontalFov, int width, int height, Span<uint> pixels)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (!(horizontalFov > 0 && horizontalFov < Math.PI))
            throw new ArgumentOutOfRangeException(nameof(horizontalFov));
        if (!double.IsFinite(angle))
            throw new ArgumentOutOfRangeException(nameof(angle));
        if ((long)width * height > pixels.Length)
            throw new ArgumentException("The buffer must hold the entire frame.", nameof(pixels));

        int floorStart = (height / 2) * width;
        pixels[..floorStart].Fill(SkyColor);
        pixels[floorStart..(width * height)].Fill(FloorColor);

        var (forwardY, forwardX) = Math.SinCos(angle);
        double planeScale = Math.Tan(horizontalFov / 2);
        double planeX = -forwardY * planeScale;
        double planeY = forwardX * planeScale;
        double focalLength = (width * 0.5) / planeScale;

        for (int x = 0; x < width; x++)
        {
            double offset = 2 * (x + 0.5) / width - 1;
            // The forward component stays one, so T measures perpendicular depth.
            var hit = map.CastRay(cameraX, cameraY, forwardX + offset * planeX,
                forwardY + offset * planeY);
            if (hit is not RayHit wall)
                continue;

            double horizon = height * 0.5;
            double halfWall = wall.T == 0 ? horizon : Math.Min(horizon, (0.5 * focalLength) / wall.T);
            // Include row centers in the half-open interval [top, bottom).
            int top = (int)Math.Ceiling(horizon - halfWall - 0.5);
            int bottom = (int)Math.Ceiling(horizon + halfWall - 0.5);
            uint color = wall.Side == WallSide.Vertical ? VerticalWallColor : HorizontalWallColor;
            for (int y = top; y < bottom; y++)
                pixels[y * width + x] = color;
        }
    }
}
