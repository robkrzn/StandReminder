using WinForms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace StandReminder;

/// <summary>Dark theme for the tray ContextMenuStrip, matching the WPF palette.</summary>
internal sealed class DarkMenuRenderer : WinForms.ToolStripProfessionalRenderer
{
    internal static readonly Drawing.Color MenuBg = Drawing.Color.FromArgb(0x23, 0x27, 0x34);
    internal static readonly Drawing.Color Hover = Drawing.Color.FromArgb(0x32, 0x38, 0x48);
    internal static readonly Drawing.Color Text = Drawing.Color.FromArgb(0xE8, 0xEA, 0xF2);
    internal static readonly Drawing.Color SubText = Drawing.Color.FromArgb(0x9A, 0xA0, 0xB4);
    internal static readonly Drawing.Color Border = Drawing.Color.FromArgb(0x3A, 0x40, 0x54);

    public DarkMenuRenderer() : base(new DarkColorTable()) { }

    protected override void OnRenderMenuItemBackground(WinForms.ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected || !e.Item.Enabled)
            return; // menu background shows through

        var g = e.Graphics;
        g.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var rect = new Drawing.Rectangle(3, 1, e.Item.Width - 6, e.Item.Height - 2);
        using var path = RoundedRect(rect, 6);
        using var brush = new Drawing.SolidBrush(Hover);
        g.FillPath(brush, path);
    }

    protected override void OnRenderItemText(WinForms.ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? Text : SubText;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderSeparator(WinForms.ToolStripSeparatorRenderEventArgs e)
    {
        var rect = e.Item.ContentRectangle;
        int y = rect.Top + rect.Height / 2;
        using var pen = new Drawing.Pen(Border);
        e.Graphics.DrawLine(pen, rect.Left + 10, y, rect.Right - 10, y);
    }

    private static Drawing.Drawing2D.GraphicsPath RoundedRect(Drawing.Rectangle r, int radius)
    {
        int d = radius * 2;
        var path = new Drawing.Drawing2D.GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private sealed class DarkColorTable : WinForms.ProfessionalColorTable
    {
        public override Drawing.Color ToolStripDropDownBackground => MenuBg;
        public override Drawing.Color ImageMarginGradientBegin => MenuBg;
        public override Drawing.Color ImageMarginGradientMiddle => MenuBg;
        public override Drawing.Color ImageMarginGradientEnd => MenuBg;
        public override Drawing.Color MenuBorder => Border;
        public override Drawing.Color MenuItemBorder => Drawing.Color.Transparent;
        public override Drawing.Color SeparatorDark => Border;
        public override Drawing.Color SeparatorLight => Border;
    }
}
