using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace MicOff;

static class TrayIcons
{
    // glifos de segoe mdl2: microfono y microfono tachado
    const string MicGlyph = "\uE720";
    const string MicOffGlyph = "\uEC54";

    public static Icon Create(bool? muted)
    {
        var size = SystemInformation.SmallIconSize.Width * 2;
        using var bitmap = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            var color = muted switch
            {
                true => Color.FromArgb(239, 68, 68),
                false => TaskbarIsLight() ? Color.FromArgb(32, 32, 32) : Color.White,
                null => Color.Gray,
            };
            using var font = new Font("Segoe MDL2 Assets", size * 0.62f, GraphicsUnit.Pixel);
            using var brush = new SolidBrush(color);
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(muted == true ? MicOffGlyph : MicGlyph, font, brush, new RectangleF(0, 0, size, size), format);
        }
        var handle = bitmap.GetHicon();
        var icon = (Icon)Icon.FromHandle(handle).Clone();
        DestroyIcon(handle);
        return icon;
    }

    static bool TaskbarIsLight()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("SystemUsesLightTheme") is int value && value == 1;
    }

    [DllImport("user32.dll")]
    static extern bool DestroyIcon(IntPtr handle);
}
