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
                    var symbol => throw new JsonException($"Unknown symbol '{symbol}' at row {y + 1}, column {x + 1}.")
                };

        var map = new GridMap(cells, [brick, metal]);
        if (!PlayerMovement.CanStandAt(map, spawn.X, spawn.Y))
            throw new JsonException("The spawn circle must fit inside the map without overlapping a wall.");
        if (!double.IsFinite(spawn.AngleDegrees))
            throw new JsonException("spawn.angleDegrees must be finite.");
        return (map, new PlayerState(spawn.X, spawn.Y, spawn.AngleDegrees * (Math.PI / 180)));
    }
}
