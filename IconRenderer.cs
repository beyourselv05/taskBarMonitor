using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace TaskbarMonitor;

static class IconRenderer
{
    // Two-digit calibration keeps the rendered size constant across 0-99;
    // without a fixed reference, a narrow single digit (e.g. "9") would be
    // scaled up far more than a two-digit value (e.g. "23") to fill the icon.
    private const string CalibrationText = "88";
    private const float ReferenceEm = 100f;

    private static readonly FontFamily SegoeUiFamily = new("Segoe UI");

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    public static Icon CreateNumberIcon(string value, Color foreground, int size)
    {
        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var fontFamily = SegoeUiFamily;

            using var refPath = BuildPath(CalibrationText, fontFamily, ReferenceEm);
            var refBounds = refPath.GetBounds();
            float scale = Math.Min(size / refBounds.Width, size / refBounds.Height);
            float finalEm = ReferenceEm * scale;

            var path = BuildPath(value, fontFamily, finalEm);
            var bounds = path.GetBounds();

            // Longer values like "100" can overflow the two-digit calibration; shrink just enough to fit.
            if (bounds.Width > size || bounds.Height > size)
            {
                float overflowShrink = Math.Min(size / bounds.Width, size / bounds.Height);
                finalEm *= overflowShrink;
                path.Dispose();
                path = BuildPath(value, fontFamily, finalEm);
                bounds = path.GetBounds();
            }

            float dx = (size - bounds.Width) / 2f - bounds.X;
            float dy = (size - bounds.Height) / 2f - bounds.Y;

            using var matrix = new Matrix();
            matrix.Translate(dx, dy);
            path.Transform(matrix);

            using var brush = new SolidBrush(foreground);
            g.FillPath(brush, path);
            path.Dispose();
        }

        var hIcon = bmp.GetHicon();
        return Icon.FromHandle(hIcon);
    }

    private static GraphicsPath BuildPath(string text, FontFamily fontFamily, float emSize)
    {
        var path = new GraphicsPath();
        path.AddString(text, fontFamily, (int)FontStyle.Bold, emSize, PointF.Empty, StringFormat.GenericTypographic);
        return path;
    }

    public static void SafeReplace(NotifyIcon notifyIcon, Icon newIcon)
    {
        var oldIcon = notifyIcon.Icon;
        notifyIcon.Icon = newIcon;
        if (oldIcon != null)
        {
            DestroyIcon(oldIcon.Handle);
            oldIcon.Dispose();
        }
    }
}
