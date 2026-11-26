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
        string path = Path.Combine(AppContext.BaseDirectory, "Levels", "demo.json");
        (GridMap Map, PlayerState Spawn) level;
        try
        {
            using var file = File.OpenRead(path);
            level = LevelLoader.Load(file, DemoTextures.Brick, DemoTextures.Metal);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            MessageBox.Show($"Could not load the level from {path}\n\n{ex.Message}",
                "RetroRaycaster", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        Application.Run(new DemoForm(level.Map, level.Spawn));
    }
}
