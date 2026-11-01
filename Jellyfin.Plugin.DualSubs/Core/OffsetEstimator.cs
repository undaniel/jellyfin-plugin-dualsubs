namespace Jellyfin.Plugin.DualSubs.Core;

/// <summary>
/// Estimates a constant offset between two subtitle tracks.
/// </summary>
public static class OffsetEstimator
{
    private const int StepMilliseconds = 50;
    private const int ToleranceMilliseconds = 250;
    private const double MinimumScoreRatio = 0.3;
    private const double MinimumImprovement = 1.15;

    /// <summary>
    /// Estimates the offset to add to secondary cues.
    /// </summary>
    /// <param name="primaryCues">Primary cues.</param>
    /// <param name="secondaryCues">Secondary cues.</param>
    /// <param name="maxOffsetSeconds">Maximum absolute offset to test.</param>
    /// <returns>Offset in milliseconds, or 0 when no reliable offset is found.</returns>
    public static int Estimate(IReadOnlyList<Cue> primaryCues, IReadOnlyList<Cue> secondaryCues, int maxOffsetSeconds)
    {
        ArgumentNullException.ThrowIfNull(primaryCues);
        ArgumentNullException.ThrowIfNull(secondaryCues);

        return Estimate(
            primaryCues.Select(cue => (long)cue.Start.TotalMilliseconds).ToArray(),
            secondaryCues.Select(cue => (long)cue.Start.TotalMilliseconds).ToArray(),
            maxOffsetSeconds);
    }

    /// <summary>
    /// Estimates the offset to add to secondary starts.
    /// </summary>
    /// <param name="primaryStartsMs">Primary cue starts in milliseconds.</param>
    /// <param name="secondaryStartsMs">Secondary cue starts in milliseconds.</param>
    /// <param name="maxOffsetSeconds">Maximum absolute offset to test.</param>
    /// <returns>Offset in milliseconds, or 0 when no reliable offset is found.</returns>
    public static int Estimate(IReadOnlyList<long> primaryStartsMs, IReadOnlyList<long> secondaryStartsMs, int maxOffsetSeconds)
    {
        ArgumentNullException.ThrowIfNull(primaryStartsMs);
        ArgumentNullException.ThrowIfNull(secondaryStartsMs);

        if (primaryStartsMs.Count == 0 || secondaryStartsMs.Count == 0 || maxOffsetSeconds <= 0)
        {
            return 0;
        }

        var primary = primaryStartsMs.Order().ToArray();
        var secondary = secondaryStartsMs.ToArray();
        var maxOffset = maxOffsetSeconds * 1000;
        var scoreAtZero = Score(primary, secondary, 0);
        var bestScore = scoreAtZero;
        var bestOffsets = new List<int> { 0 };

        for (var offset = -maxOffset; offset <= maxOffset; offset += StepMilliseconds)
        {
            var score = Score(primary, secondary, offset);
            if (score > bestScore)
            {
                bestScore = score;
                bestOffsets.Clear();
                bestOffsets.Add(offset);
            }
            else if (score == bestScore && offset != 0)
            {
                bestOffsets.Add(offset);
            }
        }

        var minimumScore = MinimumScoreRatio * Math.Min(primary.Length, secondary.Length);
        if (bestScore < minimumScore || bestScore <= scoreAtZero * MinimumImprovement)
        {
            return 0;
        }

        // A +/-250 ms tolerance creates a plateau of equal scores. Cue spacing can create
        // aliases one interval apart, so group contiguous offsets, take each plateau's
        // center, and prefer the smallest correction.
        var offsets = bestOffsets.Order().ToArray();
        var clusters = new List<List<int>>();
        foreach (var offset in offsets)
        {
            if (clusters.Count == 0 || offset - clusters[^1][^1] > StepMilliseconds * 2)
            {
                clusters.Add([offset]);
            }
            else
            {
                clusters[^1].Add(offset);
            }
        }

        var median = clusters
            .Select(Median)
            .OrderBy(value => Math.Abs(value))
            .First();
        return (int)(Math.Round(median / StepMilliseconds, MidpointRounding.AwayFromZero) * StepMilliseconds);
    }

    private static double Median(List<int> values)
    {
        var middle = values.Count / 2;
        return values.Count % 2 == 1
            ? values[middle]
            : (values[middle - 1] + values[middle]) / 2.0;
    }

    /// <summary>
    /// Shifts cues and clamps negative times to zero.
    /// </summary>
    /// <param name="cues">Cues to shift.</param>
    /// <param name="offsetMs">Offset in milliseconds.</param>
    /// <returns>Shifted cues sorted by start time.</returns>
    public static IReadOnlyList<Cue> ApplyOffset(IReadOnlyList<Cue> cues, int offsetMs)
    {
        ArgumentNullException.ThrowIfNull(cues);

        if (offsetMs == 0)
        {
            return cues;
        }

        var shifted = new List<Cue>(cues.Count);
        foreach (var cue in cues)
        {
            var start = cue.Start + TimeSpan.FromMilliseconds(offsetMs);
            var end = cue.End + TimeSpan.FromMilliseconds(offsetMs);
            if (start < TimeSpan.Zero)
            {
                start = TimeSpan.Zero;
            }

            if (end < TimeSpan.Zero)
            {
                end = TimeSpan.Zero;
            }

            if (end > start)
            {
                shifted.Add(cue with { Start = start, End = end });
            }
        }

        return shifted.OrderBy(cue => cue.Start).ToList();
    }

    private static int Score(long[] primary, long[] secondary, int offset)
    {
        var score = 0;
        foreach (var start in secondary)
        {
            var shifted = start + offset;
            var index = Array.BinarySearch(primary, shifted);
            if (index < 0)
            {
                index = ~index;
            }

            var matches = index < primary.Length && Math.Abs(primary[index] - shifted) <= ToleranceMilliseconds;
            if (!matches && index > 0)
            {
                matches = Math.Abs(primary[index - 1] - shifted) <= ToleranceMilliseconds;
            }

            if (matches)
            {
                score++;
            }
        }

        return score;
    }
}
