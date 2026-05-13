using System.Drawing;

namespace GuguSolucoes.Desktop.UI;

internal sealed class ThemePalette
{
    public required Color Background { get; init; }
    public required Color Surface { get; init; }
    public required Color SurfaceSoft { get; init; }
    public required Color HeaderBackground { get; init; }
    public required Color Border { get; init; }
    public required Color HeaderBorder { get; init; }
    public required Color InputBackground { get; init; }
    public required Color InputFocusBackground { get; init; }
    public required Color TextPrimary { get; init; }
    public required Color TextSecondary { get; init; }
    public required Color TextMuted { get; init; }
    public required Color SecondaryButtonBackground { get; init; }
    public required Color SecondaryButtonBorder { get; init; }
    public required Color NavInactiveBackground { get; init; }
    public required Color NavHoverBackground { get; init; }
    public required Color NavActiveBackground { get; init; }
    public required Color Accent { get; init; }
    public required Color Success { get; init; }
    public required Color Error { get; init; }
    public required Color PreviewCardBackground { get; init; }
    public required Color LogBackground { get; init; }
    public required Color LogBorder { get; init; }
    public required Color ProgressTrack { get; init; }
    public required Color ProgressFill { get; init; }
}
