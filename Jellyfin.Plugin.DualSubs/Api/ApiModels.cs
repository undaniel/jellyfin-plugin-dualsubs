namespace Jellyfin.Plugin.DualSubs.Api;

/// <summary>
/// Result status values used by the API.
/// </summary>
public static class DualSubsStatus
{
    /// <summary>A new file was created.</summary>
    public const string Created = "Created";

    /// <summary>The existing file is up to date.</summary>
    public const string Unchanged = "Unchanged";

    /// <summary>The existing file was replaced.</summary>
    public const string Replaced = "Replaced";

    /// <summary>One of the languages is missing.</summary>
    public const string MissingSource = "MissingSource";

    /// <summary>One of the languages only has bitmap subtitles.</summary>
    public const string BitmapOnly = "BitmapOnly";

    /// <summary>The pair failed.</summary>
    public const string Error = "Error";

    /// <summary>Preview succeeded.</summary>
    public const string Ok = "Ok";
}

/// <summary>
/// Result for one language pair.
/// </summary>
/// <param name="Pair">Normalized pair.</param>
/// <param name="Status">Result status.</param>
/// <param name="PrimaryStreamIndex">Chosen primary stream index.</param>
/// <param name="SecondaryStreamIndex">Chosen secondary stream index.</param>
/// <param name="OffsetMs">Applied offset.</param>
/// <param name="Message">Optional detail.</param>
public sealed record DualSubsPairResult(
    string Pair,
    string Status,
    int? PrimaryStreamIndex,
    int? SecondaryStreamIndex,
    int OffsetMs,
    string? Message);

/// <summary>
/// Generate response.
/// </summary>
/// <param name="ItemId">Item id.</param>
/// <param name="Results">Result per pair.</param>
public sealed record DualSubsGenerateResponse(Guid ItemId, IReadOnlyList<DualSubsPairResult> Results);

/// <summary>
/// Preview response. Written files are not touched.
/// </summary>
/// <param name="ItemId">Item id.</param>
/// <param name="Pair">Normalized pair.</param>
/// <param name="Status">Result status.</param>
/// <param name="PrimaryStreamIndex">Chosen primary stream index.</param>
/// <param name="SecondaryStreamIndex">Chosen secondary stream index.</param>
/// <param name="PrimaryTitle">Primary track title.</param>
/// <param name="SecondaryTitle">Secondary track title.</param>
/// <param name="PrimaryCueCount">Primary cue count.</param>
/// <param name="SecondaryCueCount">Secondary cue count.</param>
/// <param name="OffsetMs">Estimated offset.</param>
/// <param name="Message">Optional detail.</param>
public sealed record DualSubsPreviewResponse(
    Guid ItemId,
    string Pair,
    string Status,
    int? PrimaryStreamIndex,
    int? SecondaryStreamIndex,
    string? PrimaryTitle,
    string? SecondaryTitle,
    int PrimaryCueCount,
    int SecondaryCueCount,
    int OffsetMs,
    string? Message);

/// <summary>
/// Delete response.
/// </summary>
/// <param name="ItemId">Item id.</param>
/// <param name="Deleted">Number of removed files.</param>
public sealed record DualSubsDeleteResponse(Guid ItemId, int Deleted);
