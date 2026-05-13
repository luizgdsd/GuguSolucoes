using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;

namespace GuguSolucoes.Desktop.UI;

internal static class StyleManager
{
    public const string FontFamily = "Segoe UI";

    public static readonly Color AccentCyan = Color.FromArgb(53, 194, 217);
    public static readonly Color AccentMint = Color.FromArgb(45, 212, 191);
    public static readonly Color AccentBlue = Color.FromArgb(56, 189, 248);

    public static readonly ThemePalette DarkTheme = new()
    {
        Background = Color.FromArgb(10, 14, 20),
        Surface = Color.FromArgb(17, 23, 32),
        SurfaceSoft = Color.FromArgb(23, 31, 42),
        HeaderBackground = Color.FromArgb(15, 21, 30),
        Border = Color.FromArgb(0, 0, 0, 0),
        HeaderBorder = Color.FromArgb(0, 0, 0, 0),
        InputBackground = Color.FromArgb(12, 18, 27),
        InputFocusBackground = Color.FromArgb(17, 28, 40),
        TextPrimary = Color.FromArgb(245, 247, 250),
        TextSecondary = Color.FromArgb(168, 179, 194),
        TextMuted = Color.FromArgb(145, 159, 179),
        SecondaryButtonBackground = Color.FromArgb(26, 36, 48),
        SecondaryButtonBorder = Color.FromArgb(0, 0, 0, 0),
        NavInactiveBackground = Color.FromArgb(17, 23, 32),
        NavHoverBackground = Color.FromArgb(28, 38, 52),
        NavActiveBackground = Color.FromArgb(21, 47, 69),
        Accent = AccentCyan,
        Success = Color.FromArgb(74, 222, 128),
        Error = Color.FromArgb(248, 113, 113),
        PreviewCardBackground = Color.FromArgb(24, 34, 47),
        LogBackground = Color.FromArgb(9, 14, 21),
        LogBorder = Color.FromArgb(48, 65, 89),
        ProgressTrack = Color.FromArgb(28, 38, 52),
        ProgressFill = AccentCyan
    };

    public static readonly ThemePalette LightTheme = new()
    {
        Background = Color.FromArgb(239, 244, 250),
        Surface = Color.FromArgb(248, 251, 255),
        SurfaceSoft = Color.FromArgb(236, 242, 250),
        HeaderBackground = Color.FromArgb(248, 251, 255),
        Border = Color.FromArgb(0, 0, 0, 0),
        HeaderBorder = Color.FromArgb(0, 0, 0, 0),
        InputBackground = Color.FromArgb(255, 255, 255),
        InputFocusBackground = Color.FromArgb(241, 247, 255),
        TextPrimary = Color.FromArgb(20, 30, 46),
        TextSecondary = Color.FromArgb(64, 84, 112),
        TextMuted = Color.FromArgb(78, 100, 132),
        SecondaryButtonBackground = Color.FromArgb(226, 234, 246),
        SecondaryButtonBorder = Color.FromArgb(0, 0, 0, 0),
        NavInactiveBackground = Color.FromArgb(250, 252, 255),
        NavHoverBackground = Color.FromArgb(235, 242, 251),
        NavActiveBackground = Color.FromArgb(220, 235, 255),
        Accent = Color.FromArgb(10, 138, 171),
        Success = Color.FromArgb(22, 128, 74),
        Error = Color.FromArgb(185, 28, 28),
        PreviewCardBackground = Color.FromArgb(241, 247, 255),
        LogBackground = Color.FromArgb(252, 254, 255),
        LogBorder = Color.FromArgb(176, 195, 220),
        ProgressTrack = Color.FromArgb(218, 229, 244),
        ProgressFill = Color.FromArgb(10, 138, 171)
    };

    public static ThemePalette ResolveTheme(bool useLightTheme)
    {
        return useLightTheme ? LightTheme : DarkTheme;
    }

    public static GraphicsPath CreateRoundedPath(Rectangle bounds, int radius)
    {
        var safeRadius = Math.Max(2, radius);
        var diameter = safeRadius * 2;
        var path = new GraphicsPath();
        path.StartFigure();
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static void ApplyRoundedRegion(Control control, int radius)
    {
        if (control.Width <= 0 || control.Height <= 0)
        {
            return;
        }

        using var path = CreateRoundedPath(new Rectangle(0, 0, control.Width - 1, control.Height - 1), radius);
        var previous = control.Region;
        control.Region = new Region(path);
        previous?.Dispose();
    }

    public static void ConfigureSurfacePanel(Panel panel, Func<Color> borderColorProvider, int radius, int shadowAlpha = 24)
    {
        panel.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = panel.ClientRectangle;
            rect.Width -= 1;
            rect.Height -= 1;

            using var shadowPath = CreateRoundedPath(new Rectangle(rect.X, rect.Y + 1, rect.Width, rect.Height), radius);
            using var shadowBrush = new SolidBrush(Color.FromArgb(Math.Clamp(shadowAlpha, 0, 255), 0, 0, 0));
            e.Graphics.FillPath(shadowBrush, shadowPath);

            using var shapePath = CreateRoundedPath(rect, radius);
            using var fillBrush = new SolidBrush(panel.BackColor);
            e.Graphics.FillPath(fillBrush, shapePath);

            var borderColor = borderColorProvider();
            if (borderColor.A > 0)
            {
                using var borderPen = new Pen(borderColor, 1F);
                e.Graphics.DrawPath(borderPen, shapePath);
            }
        };

        panel.Resize += (_, _) => ApplyRoundedRegion(panel, radius);
        ApplyRoundedRegion(panel, radius);
    }

    public static void ConfigurePrimaryButton(Button button, ThemePalette theme, Color accent)
    {
        button.BackColor = accent;
        button.ForeColor = Color.White;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = ShiftBrightness(accent, -18);
        button.FlatAppearance.MouseDownBackColor = ShiftBrightness(accent, -30);
    }

    public static void ConfigureSecondaryButton(Button button, ThemePalette theme)
    {
        button.BackColor = theme.SecondaryButtonBackground;
        button.ForeColor = theme.TextPrimary;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.BorderColor = theme.SecondaryButtonBorder;
        button.FlatAppearance.MouseOverBackColor = theme.InputFocusBackground;
        button.FlatAppearance.MouseDownBackColor = theme.InputBackground;
    }

    public static void ConfigureNavigationButton(Button button, ThemePalette theme, bool active, Color accent)
    {
        button.BackColor = active ? theme.NavActiveBackground : theme.NavInactiveBackground;
        button.ForeColor = theme.TextPrimary;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.BorderColor = active ? accent : theme.Border;
        button.FlatAppearance.MouseOverBackColor = theme.NavHoverBackground;
        button.FlatAppearance.MouseDownBackColor = theme.InputFocusBackground;
    }

    public static void EnableDoubleBuffering(Control control)
    {
        var doubleBufferProperty = typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
        doubleBufferProperty?.SetValue(control, true);
    }

    public static void EnableDoubleBufferingRecursive(Control root, Func<Control, bool>? filter = null)
    {
        if (filter is null || filter(root))
        {
            EnableDoubleBuffering(root);
        }

        foreach (Control child in root.Controls)
        {
            EnableDoubleBufferingRecursive(child, filter);
        }
    }

    public static Color ShiftBrightness(Color color, int amount)
    {
        return Color.FromArgb(
            color.A,
            Math.Clamp(color.R + amount, 0, 255),
            Math.Clamp(color.G + amount, 0, 255),
            Math.Clamp(color.B + amount, 0, 255));
    }
}
