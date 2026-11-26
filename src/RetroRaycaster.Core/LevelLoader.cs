using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RetroRaycaster.Core;

public static class LevelLoader
{
    private sealed class LevelData
    {
        [JsonPropertyName("rows")] public required string?[]? Rows { get; init; }
        [JsonPropertyName("spawn")] public required SpawnData? Spawn { get; init; }
    }

    private sealed class SpawnData
    {
        [JsonPropertyName("x")] public required double X { get; init; }
        [JsonPropertyName("y")] public required double Y { get; init; }
        [JsonPropertyName("angleDegrees")] public required double AngleDegrees { get; init; }
    }

    public static (GridMap Map, PlayerState Spawn) Load(Stream stream, WallTexture brick, WallTexture metal)
    {
        var level = JsonSerializer.Deserialize<LevelData>(stream);
        if (level?.Rows is not { Length: > 0 } rows)
            throw new JsonException("Expected a non-empty rows array.");
        var spawn = level.Spawn ?? throw new JsonException("Expected a spawn object.");
        int width = rows[0]?.Length ?? 0;
        for (int y = 0; y < rows.Length; y++)
        {
            if (string.IsNullOrEmpty(rows[y]))
                throw new JsonException($"Row {y + 1} must be a non-empty string.");
            if (rows[y]!.Length != width)
                throw new JsonException($"Row {y + 1} must contain {width} cells.");
        }

        var cells = new int[rows.Length, width];
        for (int y = 0; y < rows.Length; y++)
            for (int x = 0; x < width; x++)
                cells[y, x] = rows[y]![x] switch
                {
                    '.' => 0,
                    'B' => 1,
                    'M' => 2,
                    'D' => DoorMaterial,
                    var symbol => throw new JsonException($"Unknown symbol '{symbol}' at row {y + 1}, column {x + 1}.")
                };

        var map = new GridMap(cells, [brick, metal, DoorTexture], DoorMaterial);
        if (!PlayerMovement.CanStandAt(map, spawn.X, spawn.Y))
            throw new JsonException("The spawn circle must fit inside the map without overlapping a wall.");
        if (!double.IsFinite(spawn.AngleDegrees))
            throw new JsonException("spawn.angleDegrees must be finite.");
        return (map, new PlayerState(spawn.X, spawn.Y, spawn.AngleDegrees * (Math.PI / 180)));
    }

    private const int DoorMaterial = 3;
    private static readonly WallTexture DoorTexture = CreateDoorTexture();

    private static WallTexture CreateDoorTexture()
    {
        var pixels = new uint[32, 32];
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                // A timber door with a dark frame, cross braces and a brass latch.
                pixels[y, x] = (x, y) switch
                {
                    (< 2 or >= 30, _) or (_, < 2 or >= 30) => 0xFF41352D,
                    (>= 23 and <= 27, >= 15 and <= 17) => 0xFFFFCC52,
                    (_, 6 or 7 or 24 or 25) => 0xFF564337,
                    _ when x % 6 == 2 => 0xFF774827,
                    _ when (x / 6) % 2 == 0 => 0xFFC28345,
                    _ => 0xFFAD703D
                };
            }
        return new WallTexture(pixels);
    }
}
