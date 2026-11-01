using Jellyfin.Plugin.DualSubs.Services;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.DualSubs.Tests;

public class SourceTrackSelectorTests
{
    [Fact]
    public void FiltersBitmapForcedWrongLanguageAndPluginFiles()
    {
        var streams = new[]
        {
            Stream(0, "eng", external: false, codec: "subrip", forced: true),
            Stream(1, "eng", external: false, codec: "pgs"),
            Stream(2, "spa", external: false, codec: "subrip"),
            Stream(3, "eng", external: true, codec: "srt"),
            Stream(4, "eng", external: true, codec: "ass")
        };

        var selected = SourceTrackSelector.SelectCandidates(
            streams,
            "eng",
            Normalize,
            stream => stream.Index == 4);

        Assert.Equal([3], selected.Select(stream => stream.Index));
    }

    [Fact]
    public void PrefersNonSdhThenExternalThenIndex()
    {
        var streams = new[]
        {
            Stream(5, "eng", external: false, codec: "subrip", sdh: false),
            Stream(4, "eng", external: true, codec: "subrip", sdh: true),
            Stream(3, "eng", external: false, codec: "subrip", sdh: true),
            Stream(2, "eng", external: true, codec: "subrip", sdh: false),
            Stream(1, "eng", external: true, codec: "subrip", sdh: false)
        };

        var selected = SourceTrackSelector.SelectCandidates(streams, "eng", Normalize);

        Assert.Equal([1, 2, 5, 4, 3], selected.Select(stream => stream.Index));
    }

    [Fact]
    public void ChoosesMostCuesInsideTheTopTier()
    {
        var sdh = Stream(1, "eng", external: true, codec: "subrip", sdh: true);
        var simple = Stream(2, "eng", external: false, codec: "subrip");
        var external = Stream(3, "eng", external: true, codec: "subrip");

        var best = SourceTrackSelector.ChooseBest(
        [
            (sdh, 1000),
            (simple, 10),
            (external, 50)
        ]);

        Assert.Same(external, best);
    }

    [Fact]
    public void ChooseBestReturnsNullWithoutUsableCues()
    {
        Assert.Null(SourceTrackSelector.ChooseBest([(Stream(1, "eng", false, "subrip"), 0)]));
    }

    [Fact]
    public void DetectsBitmapOnlyLanguages()
    {
        var streams = new[] { Stream(1, "eng", false, "pgs"), Stream(2, "spa", false, "subrip") };

        Assert.True(SourceTrackSelector.HasBitmapOnlyForLanguage(streams, "eng", Normalize));
        Assert.False(SourceTrackSelector.HasBitmapOnlyForLanguage(streams, "spa", Normalize));
        Assert.False(SourceTrackSelector.HasBitmapOnlyForLanguage(streams, "fra", Normalize));
    }

    private static string Normalize(string? language)
    {
        return language?.ToLowerInvariant() switch
        {
            "en" or "eng" => "eng",
            "es" or "spa" => "spa",
            "fr" or "fra" => "fra",
            null => string.Empty,
            _ => language.ToLowerInvariant()
        };
    }

    private static MediaStream Stream(
        int index,
        string language,
        bool external,
        string codec,
        bool forced = false,
        bool sdh = false)
    {
        return new MediaStream
        {
            Index = index,
            Type = MediaStreamType.Subtitle,
            Language = language,
            Codec = codec,
            IsExternal = external,
            IsForced = forced,
            IsHearingImpaired = sdh,
            Path = external ? $"/tmp/{index}.srt" : null
        };
    }
}
