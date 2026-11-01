using Jellyfin.Plugin.DualSubs.Core;

namespace Jellyfin.Plugin.DualSubs.Tests;

public class AssColorTests
{
    [Theory]
    [InlineData("#FFFFFF", "&H00FFFFFF")]
    [InlineData("#FFE680", "&H0080E6FF")]
    [InlineData("000000", "&H00000000")]
    [InlineData("#ff0000", "&H000000FF")]
    [InlineData("#00FF00", "&H0000FF00")]
    public void ConvertsRgbToAssBgr(string input, string expected)
    {
        Assert.Equal(expected, AssColor.FromHtml(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("#FFF")]
    [InlineData("#GGGGGG")]
    [InlineData("#FFFFFFF")]
    [InlineData("red")]
    public void RejectsInvalidColors(string input)
    {
        Assert.ThrowsAny<ArgumentException>(() => AssColor.FromHtml(input));
    }
}
