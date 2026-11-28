namespace RetroRaycaster.Core;

public readonly record struct WorldSprite(double X, double Y, double Height, WallTexture Texture)
{
    private static readonly WallTexture LampTexture = CreateLampTexture();
    private static readonly WallTexture CellTexture = CreateCellTexture();
    private static readonly WallTexture DormantExitTexture = CreateExitTexture(0xFF805642);
    internal static readonly WallTexture ActiveExitTexture = CreateExitTexture(0xFF80FF75);
    internal bool IsEnergyCell { get; private init; }

    internal static WorldSprite SignalLamp(double x, double y) => new(x, y, 0.75, LampTexture);
    internal static WorldSprite LevelExit(double x, double y) => new(x, y, 0.95, DormantExitTexture);
    internal static WorldSprite EnergyCell(double x, double y) =>
        new(x, y, 0.42, CellTexture) { IsEnergyCell = true };

    private static WallTexture CreateExitTexture(uint light)
    {
        var pixels = new uint[28, 24];
        for (int y = 0; y < 28; y++)
            for (int x = 0; x < 24; x++)
                pixels[y, x] = (x, y) switch
                {
                    (>= 7 and <= 16, 2 or 3) or (3 or 20, >= 7 and <= 22) => light,
                    (>= 2 and <= 21, >= 1 and <= 5) or (>= 2 and <= 5 or >= 18 and <= 21, >= 6) => 0xFF555B76,
                    (>= 1 and <= 22, >= 26) => 0xFF8D92AC,
                    _ => 0
                };
        return new WallTexture(pixels);
    }

    private static WallTexture CreateCellTexture()
    {
        var pixels = new uint[16, 20];
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 20; x++)
                pixels[y, x] = (x, y) switch
                {
                    (>= 4 and <= 7 or >= 12 and <= 15, 1 or 2) => 0xFFE8B964,
                    (>= 2 and <= 17, 3 or 14) or (1 or 18, >= 4 and <= 13) => 0xFF432443,
                    (>= 3 and <= 16, 4) => 0xFFFF99D0,
                    (>= 10 and <= 12, 5 or 6) or (>= 8 and <= 11, 7 or 8) or
                        (>= 7 and <= 12, 9) or (>= 9 and <= 10, 10 or 11) or (8, 12) => 0xFFFFEF83,
                    (>= 2 and <= 17, >= 4 and <= 13) => 0xFFB02C77,
                    _ => 0
                };
        return new WallTexture(pixels);
    }

    private static WallTexture CreateLampTexture()
    {
        var pixels = new uint[24, 16];
        for (int y = 0; y < 24; y++)
            for (int x = 0; x < 16; x++)
                pixels[y, x] = (x, y) switch
                {
                    (>= 2 and <= 13, >= 21) => 0xFF394752,
                    (>= 4 and <= 11, 20) or (>= 6 and <= 9, >= 11 and < 20) => 0xFF7896A0,
                    (>= 3 and <= 12, 2 or 10) or (>= 5 and <= 10, 1) => 0xFF304854,
                    (>= 5 and <= 7, >= 4 and <= 6) => 0xFFCAFFF2,
                    (>= 4 and <= 11, >= 3 and <= 9) => 0xFF22DBB0,
                    _ => 0
                };
        return new WallTexture(pixels);
    }
}
