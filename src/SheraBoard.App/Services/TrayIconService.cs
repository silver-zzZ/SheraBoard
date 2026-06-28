using System.Drawing;
using System.Drawing.Drawing2D;
using System.Diagnostics;
using Forms = System.Windows.Forms;
using SheraBoard.Core.Settings;

namespace SheraBoard.App.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ToolStripMenuItem _pauseItem;

    public TrayIconService(AppSettings settings)
    {
        _pauseItem = CreateMenuItem(string.Empty, (_, _) => TogglePauseRequested?.Invoke(this, EventArgs.Empty));

        var menu = CreateModernMenu();
        menu.Items.Add(CreateMenuItem("打开 SheraBoard", (_, _) => ShowRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(_pauseItem);
        menu.Items.Add(CreateMenuItem("设置", (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(CreateSeparator());
        menu.Items.Add(CreateMenuItem("退出", (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty)));

        _notifyIcon = new Forms.NotifyIcon
        {
            Text = "SheraBoard",
            Icon = ResolveAppIcon(),
            ContextMenuStrip = menu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => ShowRequested?.Invoke(this, EventArgs.Empty);

        Refresh(settings);
    }

    public event EventHandler? ShowRequested;

    public event EventHandler? SettingsRequested;

    public event EventHandler? TogglePauseRequested;

    public event EventHandler? ExitRequested;

    public void Refresh(AppSettings settings)
    {
        _pauseItem.Text = settings.CapturePaused ? "恢复记录" : "暂停记录";
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }

    private static Icon ResolveAppIcon()
    {
        try
        {
            var resource = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/AppIcon.ico", UriKind.Absolute));
            if (resource?.Stream is not null)
            {
                using var icon = new Icon(resource.Stream);
                return (Icon)icon.Clone();
            }
        }
        catch
        {
            // Fall back to the executable icon below. Tray icon failure should
            // never prevent the clipboard monitor from starting.
        }

        var processPath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
        if (!string.IsNullOrWhiteSpace(processPath))
        {
            var icon = Icon.ExtractAssociatedIcon(processPath);
            if (icon is not null)
            {
                return icon;
            }
        }

        return SystemIcons.Application;
    }

    private static Forms.ContextMenuStrip CreateModernMenu()
    {
        var menu = new Forms.ContextMenuStrip
        {
            BackColor = Color.White,
            ForeColor = Color.FromArgb(17, 24, 39),
            ShowImageMargin = false,
            ShowCheckMargin = false,
            Padding = new Forms.Padding(6),
            Renderer = new ModernToolStripRenderer(),
            DropShadowEnabled = true,
            Font = new Font("Segoe UI Variable Text", 9F, FontStyle.Regular, GraphicsUnit.Point)
        };

        menu.Opening += (_, _) =>
        {
            menu.Region?.Dispose();
            menu.Region = new Region(CreateRoundRectangle(new Rectangle(Point.Empty, menu.Size), 10));
        };
        return menu;
    }

    private static Forms.ToolStripMenuItem CreateMenuItem(string text, EventHandler onClick)
    {
        var item = new Forms.ToolStripMenuItem(text)
        {
            AutoSize = false,
            Width = 168,
            Height = 32,
            Padding = new Forms.Padding(10, 0, 10, 0),
            Margin = new Forms.Padding(0, 1, 0, 1),
            ForeColor = Color.FromArgb(17, 24, 39)
        };
        item.Click += onClick;
        return item;
    }

    private static Forms.ToolStripSeparator CreateSeparator()
    {
        return new Forms.ToolStripSeparator
        {
            Margin = new Forms.Padding(8, 5, 8, 5)
        };
    }

    private static GraphicsPath CreateRoundRectangle(Rectangle rectangle, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        var arc = new Rectangle(rectangle.Location, new Size(diameter, diameter));
        path.AddArc(arc, 180, 90);
        arc.X = rectangle.Right - diameter - 1;
        path.AddArc(arc, 270, 90);
        arc.Y = rectangle.Bottom - diameter - 1;
        path.AddArc(arc, 0, 90);
        arc.X = rectangle.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    private sealed class ModernToolStripRenderer : Forms.ToolStripProfessionalRenderer
    {
        private static readonly Color BorderColor = Color.FromArgb(221, 232, 247);
        private static readonly Color HoverColor = Color.FromArgb(243, 248, 255);
        private static readonly Color PressedColor = Color.FromArgb(234, 242, 255);
        private static readonly Color TextColor = Color.FromArgb(17, 24, 39);
        private static readonly Color MutedColor = Color.FromArgb(100, 116, 139);

        protected override void OnRenderToolStripBackground(Forms.ToolStripRenderEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(Color.White);
            using var path = CreateRoundRectangle(new Rectangle(Point.Empty, e.ToolStrip.Size), 10);
            e.Graphics.FillPath(brush, path);
        }

        protected override void OnRenderToolStripBorder(Forms.ToolStripRenderEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(BorderColor);
            using var path = CreateRoundRectangle(new Rectangle(Point.Empty, e.ToolStrip.Size - new Size(1, 1)), 10);
            e.Graphics.DrawPath(pen, path);
        }

        protected override void OnRenderMenuItemBackground(Forms.ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected && !e.Item.Pressed)
            {
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var menuWidth = e.ToolStrip?.Width ?? e.Item.Width;
            var bounds = new Rectangle(4, 1, menuWidth - 8, e.Item.Height - 2);
            using var brush = new SolidBrush(e.Item.Pressed ? PressedColor : HoverColor);
            using var path = CreateRoundRectangle(bounds, 7);
            e.Graphics.FillPath(brush, path);
        }

        protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? TextColor : MutedColor;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderSeparator(Forms.ToolStripSeparatorRenderEventArgs e)
        {
            using var pen = new Pen(BorderColor);
            var y = e.Item.Height / 2;
            e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
        }
    }
}
