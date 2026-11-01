using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.DualSubs.Configuration;

/// <summary>
/// One language pair.
/// </summary>
public class SubtitlePair
{
    /// <summary>
    /// Gets or sets the language being learned. Shown at the bottom.
    /// </summary>
    public string PrimaryLanguage { get; set; } = "eng";

    /// <summary>
    /// Gets or sets the native language. Shown at the top.
    /// </summary>
    public string SecondaryLanguage { get; set; } = "spa";

    /// <summary>
    /// Gets or sets a value indicating whether this pair is generated.
    /// </summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets configured language pairs.
    /// </summary>
    public List<SubtitlePair> Pairs { get; set; } = [new SubtitlePair()];

    /// <summary>
    /// Gets or sets the layout: TopBottom or StackedBottom.
    /// </summary>
    public string Layout { get; set; } = "TopBottom";

    /// <summary>
    /// Gets or sets the primary font size.
    /// </summary>
    public int PrimaryFontSize { get; set; } = 62;

    /// <summary>
    /// Gets or sets the secondary font size.
    /// </summary>
    public int SecondaryFontSize { get; set; } = 50;

    /// <summary>
    /// Gets or sets the primary color as #RRGGBB.
    /// </summary>
    public string PrimaryColor { get; set; } = "#FFFFFF";

    /// <summary>
    /// Gets or sets the secondary color as #RRGGBB.
    /// </summary>
    public string SecondaryColor { get; set; } = "#FFE680";

    /// <summary>
    /// Gets or sets a value indicating whether the secondary style is italic.
    /// </summary>
    public bool SecondaryItalic { get; set; }

    /// <summary>
    /// Gets or sets the outline size.
    /// </summary>
    public int OutlineSize { get; set; } = 3;

    /// <summary>
    /// Gets or sets a value indicating whether offsets are estimated automatically.
    /// </summary>
    public bool AutoAlignOffset { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum automatic offset in seconds.
    /// </summary>
    public int MaxOffsetSeconds { get; set; } = 10;

    /// <summary>
    /// Gets or sets library ids to process. Empty means every video library.
    /// </summary>
    public List<Guid> LibraryIds { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether new items are processed automatically.
    /// </summary>
    public bool ProcessOnItemAdded { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether files generated from older sources are replaced.
    /// </summary>
    public bool OverwritePluginFiles { get; set; } = true;

    /// <summary>
    /// Gets or sets the debounce delay before processing a library event.
    /// </summary>
    public int DebounceSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets the maximum number of items processed in parallel by the event handler.
    /// </summary>
    public int MaxConcurrency { get; set; } = 1;

    /// <summary>
    /// Creates style options from the current configuration.
    /// </summary>
    /// <returns>Style options.</returns>
    public Core.DualSubsStyleOptions ToStyleOptions()
    {
        return new Core.DualSubsStyleOptions
        {
            Layout = Layout,
            PrimaryFontSize = PrimaryFontSize,
            SecondaryFontSize = SecondaryFontSize,
            PrimaryColor = PrimaryColor,
            SecondaryColor = SecondaryColor,
            SecondaryItalic = SecondaryItalic,
            OutlineSize = OutlineSize,
            AutoAlignOffset = AutoAlignOffset,
            MaxOffsetSeconds = MaxOffsetSeconds
        };
    }
}
