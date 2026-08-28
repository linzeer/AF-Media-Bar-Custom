namespace AFMediaBar.Models;

internal enum WindowHostMode
{
    Taskbar = 0,
    Floating = 1
}

internal enum PlayerLayoutMode
{
    Automatic = 0,
    Horizontal = 1,
    Vertical = 2
}

internal readonly record struct WindowSettings(
    bool HideWhenNoMedia,
    bool AlwaysOnTop,
    WindowHostMode HostMode,
    PlayerLayoutMode LayoutMode,
    int LengthScalePercent,
    int ThicknessScalePercent,
    bool AutoCollapse,
    bool EdgeAutoCollapse,
    int? FloatingLeft,
    int? FloatingTop,
    bool ShowArtwork,
    int ArtworkCornerRadius,
    bool ShowMediaInfo,
    int MetricsFontSize,
    bool HidePlayerOnNoMedia)
{
    internal const int MinMetricsFontSize = 8;
    internal const int MaxMetricsFontSize = 16;
    internal const int DefaultMetricsFontSize = 11;

    internal static WindowSettings Default { get; } = new(
        false,
        false,
        WindowHostMode.Taskbar,
        PlayerLayoutMode.Automatic,
        100,
        100,
        true,
        false,
        null,
        null,
        true,
        6,
        true,
        DefaultMetricsFontSize,
        false);
}
