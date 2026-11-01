using Jellyfin.Plugin.DualSubs.Core;

namespace Jellyfin.Plugin.DualSubs.Tests;

public class SrtParserTests
{
    [Fact]
    public void ParsesBomCrLfAndTrailingCoordinates()
    {
        var srt = "\uFEFF1\r\n00:00:01,500 --> 00:00:03,200 X1:10 X2:20\r\nHello\r\n\r\n";

        var cues = SrtParser.Parse(srt);

        var cue = Assert.Single(cues);
        Assert.Equal(TimeSpan.FromMilliseconds(1500), cue.Start);
        Assert.Equal(TimeSpan.FromMilliseconds(3200), cue.End);
        Assert.Equal("Hello", cue.Text);
    }

    [Fact]
    public void AcceptsMissingIndexesDotMillisecondsAndLf()
    {
        var srt = "00:00:01.5 --> 00:00:02.25\nLine one\nLine two\n\n00:00:04,000 --> 00:00:05,000\nNext\n";

        var cues = SrtParser.Parse(srt);

        Assert.Equal(2, cues.Count);
        Assert.Equal(TimeSpan.FromMilliseconds(1500), cues[0].Start);
        Assert.Equal(TimeSpan.FromMilliseconds(2250), cues[0].End);
        Assert.Equal("Line one\nLine two", cues[0].Text);
    }

    [Fact]
    public void RemovesHtmlAndAssTagsAndCollapsesSpaces()
    {
        var srt = "1\n00:00:01,000 --> 00:00:02,000\n<i>Hello</i>   <font color=\"red\">world</font> {\\an8}  now\n\n";

        var cue = Assert.Single(SrtParser.Parse(srt));

        Assert.Equal("Hello world now", cue.Text);
    }

    [Fact]
    public void SortsCuesAndDropsInvalidOrEmptyOnes()
    {
        var srt = "1\n00:00:05,000 --> 00:00:06,000\nSecond\n\n"
                  + "2\n00:00:03,000 --> 00:00:02,000\nInvalid\n\n"
                  + "3\n00:00:01,000 --> 00:00:02,000\n<i></i>   \n\n"
                  + "4\n00:00:01,000 --> 00:00:02,000\nFirst\n";

        var cues = SrtParser.Parse(srt);

        Assert.Equal(["First", "Second"], cues.Select(cue => cue.Text));
    }

    [Fact]
    public void HandlesCuesWithoutBlankSeparator()
    {
        var srt = "1\n00:00:01,000 --> 00:00:02,000\nOne\n2\n00:00:03,000 --> 00:00:04,000\nTwo\n";

        var cues = SrtParser.Parse(srt);

        Assert.Equal(["One", "Two"], cues.Select(cue => cue.Text));
    }

    [Fact]
    public void ParsesHoursLargerThanNine()
    {
        var srt = "1\n10:02:03,004 --> 11:00:00,000\nLong\n";

        var cue = Assert.Single(SrtParser.Parse(srt));

        Assert.Equal(TimeSpan.FromHours(10) + TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(3) + TimeSpan.FromMilliseconds(4), cue.Start);
        Assert.Equal(TimeSpan.FromHours(11), cue.End);
    }

    [Fact]
    public void ReturnsEmptyForNullEmptyOrGarbage()
    {
        Assert.Empty(SrtParser.Parse(null));
        Assert.Empty(SrtParser.Parse("   "));
        Assert.Empty(SrtParser.Parse("not a subtitle"));
    }

    [Theory]
    [InlineData("00:00:00,001", 1)]
    [InlineData("00:00:00,01", 10)]
    [InlineData("00:00:00,1", 100)]
    public void PadsShortMilliseconds(string time, int expectedMs)
    {
        var srt = $"1\n{time} --> 00:00:02,000\nText\n";

        var cue = Assert.Single(SrtParser.Parse(srt));

        Assert.Equal(expectedMs, cue.Start.Milliseconds);
    }
}
