using System;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using RetroRaycaster.Core;

namespace RetroRaycaster.Desktop;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        if (LoadLevel() is { } level)
            Application.Run(new DemoForm(level.Map, level.Spawn));
    }

    internal static (GridMap Map, PlayerState Spawn)? LoadLevel()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Levels", "demo.json");
        try
        {
            using var file = File.OpenRead(path);
            return LevelLoader.Load(file, DemoTextures.Brick, DemoTextures.Metal);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            MessageBox.Show($"Could not load the level from {path}\n\n{ex.Message}",
                "RetroRaycaster", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return null;
        }
    }
}
