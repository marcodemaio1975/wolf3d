using System;
using System.Buffers;

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
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        var depths = ArrayPool<double>.Shared.Rent(width);
        try
        {
            Render(map, cameraX, cameraY, angle, horizontalFov, width, height, pixels, depths);
        }
        finally
        {
            ArrayPool<double>.Shared.Return(depths);
        }
    }

    public static void Render(GridMap map, double cameraX, double cameraY, double angle,
        double horizontalFov, int width, int height, Span<uint> pixels, Span<double> wallDepths)
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
        if (wallDepths.Length < width)
            throw new ArgumentException("The depth buffer must hold one value per column.", nameof(wallDepths));

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
            double rayX = forwardX + offset * planeX;
            double rayY = forwardY + offset * planeY;
            var hit = map.CastRay(cameraX, cameraY, rayX, rayY);
            wallDepths[x] = hit?.T ?? double.PositiveInfinity;
            if (hit is not RayHit wall)
                continue;

            double horizon = height * 0.5;
            double halfWall = wall.T == 0 ? horizon : Math.Min(horizon, (0.5 * focalLength) / wall.T);
            // Include row centers in the half-open interval [top, bottom).
            int top = (int)Math.Ceiling(horizon - halfWall - 0.5);
            int bottom = (int)Math.Ceiling(horizon + halfWall - 0.5);
            bool vertical = wall.Side == WallSide.Vertical;
            var texture = map.TextureAtWall(wall.CellX, wall.CellY);
            if (texture is null)
            {
                uint color = vertical ? VerticalWallColor : HorizontalWallColor;
                for (int y = top; y < bottom; y++)
                    pixels[y * width + x] = color;
                continue;
            }

            double along = vertical ? cameraY + rayY * wall.T : cameraX + rayX * wall.T;
            int u = Math.Min(texture.Width - 1, (int)((along - Math.Floor(along)) * texture.Width));
            // U increases to the viewer's right when facing each wall head-on.
            if (vertical ? rayX < 0 : rayY > 0)
                u = texture.Width - 1 - u;

            // T / focalLength is the inverse *unclipped* projected wall height.
            // At T = 0 the visible strip samples the middle of the texture.
            double inverseHeight = wall.T / focalLength;
            for (int y = top; y < bottom; y++)
            {
                double v = 0.5 + (y + 0.5 - horizon) * inverseHeight;
                int row = Math.Clamp((int)(v * texture.Height), 0, texture.Height - 1);
                uint color = texture[row, u];
                pixels[y * width + x] = vertical ? color : (color & 0xFF000000u) | ((color & 0x00FEFEFEu) >> 1);
            }
        }

        if (!map.Objects.IsEmpty)
            DrawSprites(map.Objects, cameraX, cameraY, forwardX, forwardY, focalLength,
                width, height, pixels, wallDepths);
    }

    private static void DrawSprites(ReadOnlySpan<WorldSprite> objects, double cameraX, double cameraY,
        double forwardX, double forwardY, double focalLength, int width, int height,
        Span<uint> pixels, ReadOnlySpan<double> wallDepths)
    {
        var pool = ArrayPool<(double Depth, int Index)>.Shared;
        var order = pool.Rent(objects.Length);
        try
        {
            int count = 0;
            for (int i = 0; i < objects.Length; i++)
            {
                double depth = (objects[i].X - cameraX) * forwardX + (objects[i].Y - cameraY) * forwardY;
                // Cull at the camera plane before dividing, including a coincident lamp.
                if (depth > 1e-8) order[count++] = (depth, i);
            }
            order.AsSpan(0, count).Sort();
            for (int i = count - 1; i >= 0; i--)
            {
                var (depth, index) = order[i];
                var sprite = objects[index];
                double right = (sprite.Y - cameraY) * forwardX - (sprite.X - cameraX) * forwardY;
                double scale = focalLength / depth;
                double spriteHeight = sprite.Height * scale;
                double spriteWidth = spriteHeight * sprite.Texture.Width / sprite.Texture.Height;
                double left = width * 0.5 + right * scale - spriteWidth * 0.5;
                double top = height * 0.5 + (0.5 - sprite.Height) * scale;
                int endX = ClipEdge(left + spriteWidth, width);
                int startY = ClipEdge(top, height);
                int endY = ClipEdge(top + spriteHeight, height);
                for (int x = ClipEdge(left, width); x < endX; x++)
                {
                    if (depth >= wallDepths[x]) continue;
                    int u = Math.Clamp((int)((x + 0.5 - left) / spriteWidth * sprite.Texture.Width), 0, sprite.Texture.Width - 1);
                    for (int y = startY; y < endY; y++)
                    {
                        int v = Math.Clamp((int)((y + 0.5 - top) / spriteHeight * sprite.Texture.Height), 0, sprite.Texture.Height - 1);
                        uint color = sprite.Texture[v, u];
                        if ((color >> 24) != 0) pixels[y * width + x] = color;
                    }
                }
            }
        }
        finally
        {
            pool.Return(order);
        }
    }

    private static int ClipEdge(double edge, int limit) =>
        (int)Math.Clamp(Math.Ceiling(edge - 0.5), 0, limit);
}
