namespace RetroRaycaster.Core;

public readonly record struct WorldSprite(double X, double Y, double Height, WallTexture Texture)
{
    private static readonly WallTexture LampTexture = CreateLampTexture();

    internal static WorldSprite SignalLamp(double x, double y) => new(x, y, 0.75, LampTexture);

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
