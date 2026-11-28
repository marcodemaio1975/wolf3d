using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using RetroRaycaster.Core;

namespace RetroRaycaster.Desktop;

internal sealed class DemoForm : Form
{
    private const int FrameWidth = 320;
    private const int FrameHeight = 200;
    private const double HorizontalFov = 70 * Math.PI / 180;
    private GridMap map;
    private PlayerState player;
    private readonly HashSet<Keys> pressed = new();
    private readonly Timer timer = new() { Interval = 16 };
    private readonly Stopwatch clock = new();

    // GDI+ reads the Core's packed ARGB pixels directly from this stable address.
    private readonly uint[] pixels = GC.AllocateArray<uint>(FrameWidth * FrameHeight, pinned: true);
    private readonly Bitmap frame;
    private readonly double[] wallDepths = new double[FrameWidth];
    private readonly Label energyIndicator = new()
    {
        AutoSize = true, ForeColor = Color.White, BackColor = Color.Black,
        Location = new Point(8, 8), Padding = new Padding(4, 2, 4, 2)
    };

    public DemoForm(GridMap initialMap, PlayerState spawn)
    {
        map = initialMap;
        player = spawn;
        frame = new Bitmap(FrameWidth, FrameHeight, FrameWidth * sizeof(uint),
            PixelFormat.Format32bppArgb, Marshal.UnsafeAddrOfPinnedArrayElement(pixels, 0));
        Text = "RetroRaycaster";
        ClientSize = new Size(FrameWidth * 3, FrameHeight * 3);
        BackColor = Color.Black;
        DoubleBuffered = true;
        ResizeRedraw = true;
        Controls.Add(energyIndicator);
        UpdateEnergyIndicator();

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape) Close();
            else if (pressed.Add(e.KeyCode))
            {
                if (map.IsCompleted && e.KeyCode == Keys.R) Replay();
                else if (!map.IsCompleted && e.KeyCode == Keys.E) map.TryToggleDoor(player);
            }
        };
        KeyUp += (_, e) => pressed.Remove(e.KeyCode);
        Deactivate += (_, _) => pressed.Clear();
        timer.Tick += (_, _) =>
        {
            double dt = Math.Min(clock.Elapsed.TotalSeconds, 0.05);
            clock.Restart();
            var input = new MovementInput(Axis(Keys.W, Keys.S), Axis(Keys.D, Keys.A),
                Axis(Keys.Right, Keys.Left));
            player = PlayerMovement.Update(map, player, input, dt);
            bool collected = map.CollectEnergyCells(player);
            bool completed = map.TryCompleteLevel(player);
            if (completed)
            {
                timer.Stop();
                clock.Reset();
            }
            if (collected || completed) UpdateEnergyIndicator();
            RenderFrame();
        };
        Shown += (_, _) =>
        {
            clock.Start();
            timer.Start();
        };
        RenderFrame();
    }

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Escape || base.IsInputKey(keyData);

    private int Axis(Keys positive, Keys negative) =>
        (pressed.Contains(positive) ? 1 : 0) - (pressed.Contains(negative) ? 1 : 0);

    private void RenderFrame()
    {
        SoftwareRenderer.Render(map, player.X, player.Y, player.Angle, HorizontalFov,
            FrameWidth, FrameHeight, pixels, wallDepths);
        Invalidate();
    }

    private void UpdateEnergyIndicator()
    {
        energyIndicator.Visible = map.TotalEnergyCells > 0 || map.IsExitActive;
        string energy = map.TotalEnergyCells > 0 ? $"Energia: {map.CollectedEnergyCells}/{map.TotalEnergyCells}" : "";
        string status = map.IsCompleted ? "Livello completato   R per rigiocare · Esc per uscire" :
            map.IsExitActive ? "Uscita attiva" : "";
        energyIndicator.Text = energy.Length > 0 && status.Length > 0 ? energy + Environment.NewLine + status : energy + status;
    }

    private void Replay()
    {
        if (Program.LoadLevel() is not { } level)
        {
            Close();
            return;
        }
        (map, player) = level;
        pressed.Clear();
        UpdateEnergyIndicator();
        RenderFrame();
        clock.Restart();
        timer.Start();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var size = ClientSize;
        float scale = Math.Min((float)size.Width / FrameWidth, (float)size.Height / FrameHeight);
        if (scale <= 0) return;
        float width = FrameWidth * scale;
        float height = FrameHeight * scale;
        var area = new RectangleF((size.Width - width) / 2, (size.Height - height) / 2, width, height);
        e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
        e.Graphics.DrawImage(frame, area, new RectangleF(0, 0, FrameWidth, FrameHeight), GraphicsUnit.Pixel);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            timer.Dispose();
            frame.Dispose();
            GC.KeepAlive(pixels);
        }
        base.Dispose(disposing);
    }
}
