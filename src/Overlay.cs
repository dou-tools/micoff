using System.Drawing.Drawing2D;

namespace MicOff;

sealed class Overlay : Form
{
    const int WS_EX_LAYERED = 0x80000;
    const int WS_EX_TRANSPARENT = 0x20;
    const int WS_EX_TOOLWINDOW = 0x80;
    const int WS_EX_NOACTIVATE = 0x8000000;
    const int WS_EX_TOPMOST = 0x8;

    readonly System.Windows.Forms.Timer hideTimer = new() { Interval = 1200 };
    string text = "";
    Color accent;

    public Overlay()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        BackColor = Color.FromArgb(24, 24, 27);
        Opacity = 0.92;
        Font = new Font("Segoe UI Semibold", 11f);
        DoubleBuffered = true;
        hideTimer.Tick += (_, _) => { hideTimer.Stop(); Hide(); };
    }

    protected override bool ShowWithoutActivation => true;

    // click-through y fuera del alt-tab
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST;
            return cp;
        }
    }

    public void ShowState(bool muted, bool persistent)
    {
        hideTimer.Stop();
        text = muted ? "MIC OFF" : "MIC ON";
        accent = muted ? Color.FromArgb(239, 68, 68) : Color.FromArgb(34, 197, 94);

        var textSize = TextRenderer.MeasureText(text, Font);
        Size = new Size(textSize.Width + 44, textSize.Height + 16);
        var area = Screen.PrimaryScreen?.WorkingArea ?? Screen.GetWorkingArea(Point.Empty);
        Location = new Point(area.Right - Width - 16, area.Bottom - Height - 16);

        using (var path = RoundedRect(new Rectangle(Point.Empty, Size), Height / 2))
            Region = new Region(path);

        Invalidate();
        if (!Visible) Show();
        if (!persistent) hideTimer.Start();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var dot = Height / 3;
        using (var brush = new SolidBrush(accent))
            g.FillEllipse(brush, 14, (Height - dot) / 2, dot, dot);
        var textRect = new Rectangle(14 + dot + 8, 0, Width - dot - 22, Height);
        TextRenderer.DrawText(g, text, Font, textRect, Color.White, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
    }

    static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var d = radius * 2;
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) hideTimer.Dispose();
        base.Dispose(disposing);
    }
}
