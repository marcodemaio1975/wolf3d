using System;

namespace RetroRaycaster.Core;

public enum WallSide { Vertical, Horizontal }

/// <summary>The entry point into the wall is origin + direction * T.</summary>
public readonly record struct RayHit(int CellX, int CellY, WallSide Side, double T);

/// <summary>A grid of unit cells. X increases rightward and Y downward.</summary>
public sealed class GridMap
{
    private readonly int[,] materials;
    private readonly WallTexture[] textures = [];
    // Door cells retain their material ID. A negative ID denotes an open door.
    private readonly int doorMaterial;
    private readonly WorldSprite[] objects = [];
    public ReadOnlySpan<WorldSprite> Objects => objects;
    public int Width => materials.GetLength(1);
    public int Height => materials.GetLength(0);

    internal bool IsSolid(int x, int y) => materials[y, x] > 0;
    internal WallTexture? TextureAtWall(int x, int y) =>
        textures.Length == 0 ? null : textures[materials[y, x] - 1];

    /// <param name="solidCells">Rows indexed by [y, x]. True denotes a wall.</param>
    public GridMap(bool[,] solidCells)
    {
        ArgumentNullException.ThrowIfNull(solidCells);
        if (solidCells.GetLength(0) == 0 || solidCells.GetLength(1) == 0)
            throw new ArgumentException("The grid must contain at least one row and column.", nameof(solidCells));

        materials = new int[solidCells.GetLength(0), solidCells.GetLength(1)];
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                materials[y, x] = solidCells[y, x] ? 1 : 0;
    }

    /// <summary>Zero denotes empty space. Positive IDs select textures starting at one.</summary>
    public GridMap(int[,] materialIds, WallTexture[] textures) : this(materialIds, textures, 0) { }

    internal GridMap(int[,] materialIds, WallTexture[] textures, int doorMaterial, WorldSprite[]? objects = null)
    {
        ArgumentNullException.ThrowIfNull(materialIds);
        ArgumentNullException.ThrowIfNull(textures);
        if (materialIds.Length == 0)
            throw new ArgumentException("The grid must contain at least one cell.", nameof(materialIds));
        materials = (int[,])materialIds.Clone();
        this.doorMaterial = doorMaterial;
        this.objects = objects ?? [];
        this.textures = (WallTexture[])textures.Clone();
        foreach (var texture in this.textures)
            ArgumentNullException.ThrowIfNull(texture);
        foreach (int id in materials)
            if (id < 0 || id > textures.Length)
                throw new ArgumentOutOfRangeException(nameof(materialIds), "Unknown material ID.");
    }

    /// <summary>
    /// Finds the first wall entered, or returns null for an outside origin, a zero
    /// direction, or a ray that leaves the map. Directions need not have unit length.
    /// The origin belongs to cell (floor(x), floor(y)) and must be empty if inside.
    /// At a corner the ray enters only the diagonal cell and reports its vertical side.
    /// </summary>
    public RayHit? CastRay(double originX, double originY, double directionX, double directionY) =>
        Trace(originX, originY, directionX, directionY, double.PositiveInfinity, includeOpenDoors: false);

    /// <summary>Toggles the first visible door within 1.5 grid units of the player.</summary>
    public bool TryToggleDoor(PlayerState player)
    {
        if (doorMaterial == 0) return false;
        var (dy, dx) = Math.SinCos(player.Angle);
        var hit = Trace(player.X, player.Y, dx, dy, 1.5, includeOpenDoors: true);
        if (hit is not RayHit cell) return false;
        int id = materials[cell.CellY, cell.CellX];
        if (Math.Abs(id) != doorMaterial) return false;
        if (id < 0 && PlayerMovement.OverlapsCell(player.X, player.Y, cell.CellX, cell.CellY))
            return false;
        materials[cell.CellY, cell.CellX] = -id;
        return true;
    }

    private RayHit? Trace(double originX, double originY, double directionX, double directionY,
        double maxDistance, bool includeOpenDoors)
    {
        if (!double.IsFinite(originX) || !double.IsFinite(originY) ||
            !double.IsFinite(directionX) || !double.IsFinite(directionY))
            throw new ArgumentException("Ray coordinates and direction must be finite.");

        if (originX < 0 || originX >= Width || originY < 0 || originY >= Height ||
            (directionX == 0 && directionY == 0))
            return null;

        int x = (int)originX;
        int y = (int)originY;
        if (IsSolid(x, y))
            throw new ArgumentException("A moving ray must start in an empty cell.");
        // The occupied doorway is the first door and cannot be closed.
        if (includeOpenDoors && materials[y, x] < 0) return null;

        int stepX = Math.Sign(directionX);
        int stepY = Math.Sign(directionY);
        double nextX = CrossingTime(x, originX, directionX);
        double nextY = CrossingTime(y, originY, directionY);

        while (true)
        {
            bool crossX = stepX != 0 && (stepY == 0 || nextX <= nextY);
            bool crossY = stepY != 0 && (stepX == 0 || nextY <= nextX);
            double t = crossX ? nextX : nextY;
            if (t > maxDistance) return null;

            if (crossX) x += stepX;
            if (crossY) y += stepY;
            if (x < 0 || x >= Width || y < 0 || y >= Height)
                return null;
            if (IsSolid(x, y) || (includeOpenDoors && materials[y, x] < 0))
                return new RayHit(x, y, crossX ? WallSide.Vertical : WallSide.Horizontal, t);

            // Compute from the original ray to avoid accumulating rounding at corners.
            if (crossX) nextX = CrossingTime(x, originX, directionX);
            if (crossY) nextY = CrossingTime(y, originY, directionY);
        }
    }

    private static double CrossingTime(int cell, double origin, double direction)
    {
        if (direction == 0) return double.PositiveInfinity;
        int boundary = direction > 0 ? cell + 1 : cell;
        return (boundary - origin) / direction;
    }
}
