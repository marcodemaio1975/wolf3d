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
        try
        {
            var campaign = ReadFile("campaign.json", Campaign.ReadManifest);
            Application.Run(new DemoForm(campaign));
        }
        catch (IOException ex)
        {
            MessageBox.Show(ex.Message, "RetroRaycaster", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    internal static (GridMap Map, PlayerState Spawn) LoadLevel(string filename) =>
        ReadFile(filename, file => LevelLoader.Load(file, DemoTextures.Brick, DemoTextures.Metal));

    private static T ReadFile<T>(string filename, Func<Stream, T> read)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Levels", filename);
        try
        {
            using var file = File.OpenRead(path);
            return read(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        {
            throw new IOException($"Could not load {path}\n\n{ex.Message}", ex);
        }
    }
}
