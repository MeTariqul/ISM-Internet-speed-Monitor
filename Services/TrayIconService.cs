using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using WinForms = System.Windows.Forms;

namespace SpeedMonitor.Services;

/// <summary>Notification area icon; opens the shared settings menu on click.</summary>
public sealed class TrayIconService : IDisposable
{
    private readonly WinForms.NotifyIcon _icon;

    public TrayIconService(WinForms.ContextMenuStrip menu, Action showMenu)
    {
        _icon = new WinForms.NotifyIcon
        {
            Icon = CreateIcon(),
            Visible = true,
            Text = "Speed Monitor",
            ContextMenuStrip = menu,
        };

        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == WinForms.MouseButtons.Left) showMenu();
        };
        _icon.DoubleClick += (_, _) => showMenu();
    }

    /// <summary>Tooltip, limited to 127 characters by the shell.</summary>
    public void SetText(string text)
    {
        if (text.Length > 127) text = text[..127];
        if (_icon.Text != text) _icon.Text = text;
    }

    private static Icon CreateIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using var background = new SolidBrush(Color.FromArgb(24, 28, 34));
            g.FillEllipse(background, 1, 1, 30, 30);

            // Big arrow down (download) ...
            using (var download = new Pen(Color.FromArgb(78, 161, 255), 4f))
            {
                download.EndCap = LineCap.ArrowAnchor;
                g.DrawLine(download, 18f, 7f, 18f, 25f);
            }

            // ... small arrow up (upload)
            using (var upload = new Pen(Color.FromArgb(38, 194, 129), 3f))
            {
                upload.EndCap = LineCap.ArrowAnchor;
                g.DrawLine(upload, 10f, 25f, 10f, 16f);
            }
        }

        IntPtr handle = bitmap.GetHicon();
        try
        {
            return (Icon)Icon.FromHandle(handle).Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
