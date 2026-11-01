namespace Jellyfin.Plugin.DualSubs.Core;

/// <summary>
/// One subtitle cue.
/// </summary>
/// <param name="Start">Start time.</param>
/// <param name="End">End time.</param>
/// <param name="Text">Cleaned cue text.</param>
public sealed record Cue(TimeSpan Start, TimeSpan End, string Text);
