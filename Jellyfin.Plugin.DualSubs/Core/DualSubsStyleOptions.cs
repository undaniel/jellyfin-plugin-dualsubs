namespace Jellyfin.Plugin.DualSubs.Core;

/// <summary>
/// ASS styling options. Values mirror the plugin configuration.
/// </summary>
public sealed record DualSubsStyleOptions
{
    /// <summary>
    /// Gets the layout: TopBottom or StackedBottom.
    /// </summary>
    public string Layout { get; init; } = "TopBottom";

    /// <summary>
    /// Gets the primary (learned language) font size.
    /// </summary>
    public int PrimaryFontSize { get; init; } = 62;

    /// <summary>
    /// Gets the secondary (native language) font size.
    /// </summary>
    public int SecondaryFontSize { get; init; } = 50;

    /// <summary>
    /// Gets the primary color as #RRGGBB.
    /// </summary>
    public string PrimaryColor { get; init; } = "#FFFFFF";

    /// <summary>
    /// Gets the secondary color as #RRGGBB.
    /// </summary>
    public string SecondaryColor { get; init; } = "#FFE680";

    /// <summary>
    /// Gets a value indicating whether the secondary style is italic.
    /// </summary>
    public bool SecondaryItalic { get; init; }

    /// <summary>
    /// Gets the outline size.
    /// </summary>
    public int OutlineSize { get; init; } = 3;

    /// <summary>
    /// Gets the primary bottom margin.
    /// </summary>
    public int PrimaryMarginV { get; init; } = 50;

    /// <summary>
    /// Gets the secondary top margin for TopBottom layout.
    /// </summary>
    public int SecondaryMarginV { get; init; } = 40;

    /// <summary>
    /// Gets a value indicating whether the offset estimator is enabled.
    /// </summary>
    public bool AutoAlignOffset { get; init; } = true;

    /// <summary>
    /// Gets the maximum auto-align offset in seconds.
    /// </summary>
    public int MaxOffsetSeconds { get; init; } = 10;
}
