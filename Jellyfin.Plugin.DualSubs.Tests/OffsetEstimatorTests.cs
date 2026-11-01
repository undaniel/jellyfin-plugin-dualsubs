using Jellyfin.Plugin.DualSubs.Core;

namespace Jellyfin.Plugin.DualSubs.Tests;

public class OffsetEstimatorTests
{
    [Fact]
    public void DetectsSyntheticShiftWithNoise()
    {
        var random = new Random(12345);
        var primary = Enumerable.Range(0, 1500)
            .Select(index => 5000L + (index * 2500L))
            .ToArray();
        var secondary = primary
            .Select(start => start + 2300 + random.Next(-80, 81))
            .ToArray();

        var offset = OffsetEstimator.Estimate(primary, secondary, 10);

        Assert.InRange(offset, -2350, -2250);
    }

    [Fact]
    public void ReturnsZeroForUnrelatedTracks()
    {
        var primary = Enumerable.Range(0, 100).Select(index => 1000L + (index * 5000L)).ToArray();
        var secondary = Enumerable.Range(0, 37).Select(index => 900_000L + (index * 7777L)).ToArray();

        Assert.Equal(0, OffsetEstimator.Estimate(primary, secondary, 10));
    }

    [Fact]
    public void ReturnsZeroWhenImprovementIsTooSmall()
    {
        var primary = Enumerable.Range(0, 100).Select(index => 1000L + (index * 2000L)).ToArray();
        var secondary = primary.Select(start => start + 50).ToArray();

        Assert.Equal(0, OffsetEstimator.Estimate(primary, secondary, 10));
    }

    [Fact]
    public void ReturnsZeroForEmptyInputOrNoRange()
    {
        Assert.Equal(0, OffsetEstimator.Estimate([], [1000], 10));
        Assert.Equal(0, OffsetEstimator.Estimate([1000], [], 10));
        Assert.Equal(0, OffsetEstimator.Estimate([1000], [1000], 0));
    }

    [Fact]
    public void FindsNegativeOffsetFromCues()
    {
        var primary = Enumerable.Range(0, 50)
            .Select(index => new Cue(TimeSpan.FromMilliseconds(index * 3700), TimeSpan.FromMilliseconds((index * 3700) + 2000), "P"))
            .ToList();
        var secondary = primary
            .Select(cue => new Cue(cue.Start + TimeSpan.FromMilliseconds(3000), cue.End + TimeSpan.FromMilliseconds(3000), "S"))
            .ToList();

        Assert.Equal(-3000, OffsetEstimator.Estimate(primary, secondary, 5));
    }

    [Fact]
    public void RejectsAliasesCreatedByCueSpacing()
    {
        var primary = Enumerable.Range(0, 8)
            .Select(index => 3000L + (index * 5000L))
            .ToArray();
        var secondary = Enumerable.Range(0, 10)
            .Select(index => 6000L + (index * 5000L))
            .ToArray();

        Assert.Equal(-3000, OffsetEstimator.Estimate(primary, secondary, 10));
    }

    [Fact]
    public void ApplyOffsetShiftsAndClampsNegativeTimes()
    {
        var cues = new List<Cue>
        {
            new(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), "A"),
            new(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(6), "B")
        };

        var shifted = OffsetEstimator.ApplyOffset(cues, -1500);

        Assert.Equal(2, shifted.Count);
        Assert.Equal(TimeSpan.Zero, shifted[0].Start);
        Assert.Equal(TimeSpan.FromMilliseconds(500), shifted[0].End);
        Assert.Equal(TimeSpan.FromMilliseconds(3500), shifted[1].Start);
        Assert.Equal(TimeSpan.FromMilliseconds(4500), shifted[1].End);
    }

    [Fact]
    public void ApplyOffsetDropsCuesFullyBeforeZero()
    {
        var cues = new List<Cue> { new(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(200), "A") };

        Assert.Empty(OffsetEstimator.ApplyOffset(cues, -1000));
    }

    [Fact]
    public void ApplyOffsetKeepsCuesWhenOffsetIsZero()
    {
        var cues = new List<Cue> { new(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), "A") };

        Assert.Same(cues, OffsetEstimator.ApplyOffset(cues, 0));
    }
}
