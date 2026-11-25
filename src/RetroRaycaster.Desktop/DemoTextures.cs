using RetroRaycaster.Core;

namespace RetroRaycaster.Desktop;

internal static class DemoTextures
{
    public static readonly WallTexture Brick = CreateBrick();
    public static readonly WallTexture Metal = CreateMetal();

    private static WallTexture CreateBrick()
    {
        var pixels = new uint[32, 32];
        for (int y = 0; y < 32; y++)
        {
            int course = y / 8;
            for (int x = 0; x < 32; x++)
            {
                int offsetX = x + 8 * (course % 2);
                int joint = offsetX % 16;
                uint clay = (course + offsetX / 16) % 2 == 0 ? 0xFFAD5842u : 0xFF99503Cu;
                pixels[y, x] = y % 8 == 0 || joint == 0 ? 0xFF635851u :
                    y % 8 == 1 || joint == 1 ? 0xFFC47557u : clay;
            }
        }
        return new WallTexture(pixels);
    }

    private static WallTexture CreateMetal()
    {
        var pixels = new uint[32, 32];
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                bool rivet = x is 3 or 28 && y is 3 or 28;
                pixels[y, x] = x == 0 || y == 0 ? 0xFF344551u :
                    x == 1 || y == 1 || rivet ? 0xFFB5CAD1u :
                    y % 4 == 2 ? 0xFF708795u : 0xFF788D99u;
            }
        return new WallTexture(pixels);
    }
}
