using System;

namespace RetroRaycaster.Core;

/// <summary>Grid coordinates and clockwise angle in radians. Zero faces east.</summary>
public readonly record struct PlayerState(double X, double Y, double Angle);

/// <summary>Signed controls in [-1, 1]. Positive values move forward, strafe right and turn clockwise.</summary>
public readonly record struct MovementInput(double Forward, double Strafe, double Turn);

public static class PlayerMovement
{
    public const double Radius = 0.2;
    public const double CellsPerSecond = 3;
    public const double RadiansPerSecond = Math.PI;

    /// <summary>Turns first, then moves using the new heading. The initial circle must fit in free space.</summary>
    public static PlayerState Update(GridMap map, PlayerState player, MovementInput input, double elapsedSeconds)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        if (!ValidAxis(input.Forward) || !ValidAxis(input.Strafe) || !ValidAxis(input.Turn))
            throw new ArgumentOutOfRangeException(nameof(input));

        double angle = player.Angle + input.Turn * elapsedSeconds * RadiansPerSecond;
        double length = Math.Sqrt(input.Forward * input.Forward + input.Strafe * input.Strafe);
        double distance = Math.Min(1, length) * elapsedSeconds * CellsPerSecond;
        if (!double.IsFinite(angle) || !double.IsFinite(distance))
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        if (distance == 0) return player with { Angle = angle };

        var (sin, cos) = Math.SinCos(angle);
        double dx = (input.Forward * cos - input.Strafe * sin) / length;
        double dy = (input.Forward * sin + input.Strafe * cos) / length;
        double x = player.X;
        double y = player.Y;
        while (distance > 0)
        {
            // Keep the axis-separated path close to the requested diagonal on long updates.
            double step = Math.Min(distance, Radius);
            double nextX = MoveAxis(map, x, y, dx * step, horizontal: true);
            double nextY = MoveAxis(map, y, nextX, dy * step, horizontal: false);
            if (x == nextX && y == nextY) break;
            x = nextX;
            y = nextY;
            distance -= step;
        }
        return new PlayerState(x, y, angle);
    }

    private static bool ValidAxis(double value) => value is >= -1 and <= 1;

    private static double MoveAxis(GridMap map, double position, double across, double delta, bool horizontal)
    {
        if (delta == 0) return position;
        int size = horizontal ? map.Width : map.Height;
        int breadth = horizontal ? map.Height : map.Width;
        double target = Math.Clamp(position + delta, Radius, size - Radius);
        int first = (int)Math.Max(0, Math.Min(position, target) - Radius);
        int last = (int)Math.Min(size - 1, Math.Max(position, target) + Radius);
        int firstRow = (int)Math.Max(0, across - Radius);
        int lastRow = (int)Math.Min(breadth - 1, across + Radius);
        for (int row = firstRow; row <= lastRow; row++)
        {
            if (across <= row - Radius || across >= row + 1 + Radius) continue;
            double gap = Math.Max(0, Math.Max(row - across, across - (row + 1)));
            // At a corner only this cross-section of the circle reaches the wall.
            double reach = Math.Sqrt(Math.Max(0, Radius * Radius - gap * gap));
            for (int cell = first; cell <= last; cell++)
            {
                if (!(horizontal ? map.IsSolid(cell, row) : map.IsSolid(row, cell))) continue;
                if (delta > 0 && position <= cell)
                    target = Math.Min(target, cell - reach);
                if (delta < 0 && position >= cell + 1)
                    target = Math.Max(target, cell + 1 + reach);
            }
        }
        return target;
    }
}
