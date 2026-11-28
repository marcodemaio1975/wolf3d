using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using RetroRaycaster.Core;

namespace RetroRaycaster.Desktop;

internal sealed class DemoForm : Form
{
    private const int FrameWidth = 320;
    private const int FrameHeight = 200;
    private const double HorizontalFov = 70 * Math.PI / 180;
    private readonly Campaign campaign;
    private GridMap map = null!; // Assigned by LoadLevel before the simulation starts.
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

    public DemoForm(Campaign campaign)
    {
        this.campaign = campaign;
        frame = new Bitmap(FrameWidth, FrameHeight, FrameWidth * sizeof(uint),
            PixelFormat.Format32bppArgb, Marshal.UnsafeAddrOfPinnedArrayElement(pixels, 0));
        Text = "RetroRaycaster";
        ClientSize = new Size(FrameWidth * 3, FrameHeight * 3);
        BackColor = Color.Black;
        DoubleBuffered = true;
        ResizeRedraw = true;
        Controls.Add(energyIndicator);

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape) Close();
            else if (pressed.Add(e.KeyCode))
            {
                if (map.IsCompleted && e.KeyCode is Keys.R or Keys.Enter)
                    LoadLevel(advance: e.KeyCode == Keys.Enter);
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
        Load += (_, _) => LoadLevel(advance: false);
    }

    protected override void WndProc(ref Message m)
    {
        // The previous-key-state bit survives clearing the input set on a level change.
        const int keyDown = 0x0100;
        if (m.Msg == keyDown && (Keys)(int)m.WParam is Keys.Enter or Keys.R &&
            (m.LParam.ToInt64() & (1L << 30)) != 0) return;
        base.WndProc(ref m);
    }

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Enter or Keys.Escape || base.IsInputKey(keyData);

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
        string hud = $"Livello {campaign.LevelNumber}/{campaign.LevelCount}";
        if (map.TotalEnergyCells > 0) hud += $"   Energia: {map.CollectedEnergyCells}/{map.TotalEnergyCells}";
        if (map.IsCompleted)
        {
            hud += Environment.NewLine + (campaign.IsLastLevel
                ? "Campagna completata   Invio per ricominciare"
                : "Livello completato   Invio per il livello successivo");
            hud += " · R per rigiocare · Esc per uscire";
        }
        else if (map.IsExitActive) hud += Environment.NewLine + "Uscita attiva";
        energyIndicator.Text = hud;
    }

    private void LoadLevel(bool advance)
    {
        try
        {
            (map, player) = campaign.LoadLevel(advance, Program.LoadLevel);
        }
        catch (IOException ex)
        {
            MessageBox.Show(this, ex.Message, "RetroRaycaster", MessageBoxButtons.OK, MessageBoxIcon.Error);
            if (campaign.LevelNumber == 0) Close();
            return;
        }
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
