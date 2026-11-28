using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RetroRaycaster.Core;

public sealed class Campaign
{
    private sealed class Manifest
    {
        [JsonPropertyName("levels")] public required string[] Levels { get; init; }
    }

    private readonly string[] levels;
    private int index = -1;

    private Campaign(string[] levels) => this.levels = levels;

    public int LevelNumber => index + 1;
    public int LevelCount => levels.Length;
    public bool IsLastLevel => LevelNumber == LevelCount;

    public static Campaign ReadManifest(Stream stream)
    {
        var manifest = JsonSerializer.Deserialize<Manifest>(stream);
        if (manifest?.Levels is not { Length: > 0 } levels)
            throw new JsonException("The campaign must list at least one level in 'levels'.");
        for (int i = 0; i < levels.Length; i++)
            if (string.IsNullOrWhiteSpace(levels[i]))
                throw new JsonException($"Expected a level filename at levels[{i}].");
        return new Campaign(levels);
    }

    public (GridMap Map, PlayerState Spawn) LoadLevel(bool advance,
        Func<string, (GridMap Map, PlayerState Spawn)> load)
    {
        int target = index < 0 ? 0 : advance ? (index + 1) % levels.Length : index;
        var level = load(levels[target]);
        index = target;
        return level;
    }
}
