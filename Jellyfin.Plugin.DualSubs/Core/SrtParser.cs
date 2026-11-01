using System.Text;
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.DualSubs.Core;

/// <summary>
/// Tolerant SRT parser. Pure logic, no Jellyfin types.
/// </summary>
public static class SrtParser
{
    private static readonly Regex TimeLine = new(
        @"^\s*(\d+):(\d{1,2}):(\d{1,2})[,.](\d{1,3})\s*-->\s*(\d+):(\d{1,2}):(\d{1,2})[,.](\d{1,3})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex HtmlTag = new(@"<[^>]*>", RegexOptions.Compiled);
    private static readonly Regex AssTag = new(@"\{[^}]*\}", RegexOptions.Compiled);
    private static readonly Regex HorizontalSpace = new(@"[ \t]+", RegexOptions.Compiled);

    /// <summary>
    /// Parses an SRT document.
    /// </summary>
    /// <param name="text">SRT text.</param>
    /// <returns>Cues sorted by start time.</returns>
    public static IReadOnlyList<Cue> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        text = text.TrimStart('\uFEFF');
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

        var cues = new List<Cue>();
        var i = 0;

        while (i < lines.Length)
        {
            if (!TimeLine.IsMatch(lines[i]))
            {
                i++;
                continue;
            }

            var match = TimeLine.Match(lines[i]);
            var start = ToTimeSpan(match, 1);
            var end = ToTimeSpan(match, 5);
            i++;

            var textLines = new List<string>();
            while (i < lines.Length)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    i++;
                    break;
                }

                // A new cue without a separating blank line: "42" followed by a time line.
                if (i + 1 < lines.Length
                    && int.TryParse(lines[i].Trim(), out _)
                    && TimeLine.IsMatch(lines[i + 1]))
                {
                    break;
                }

                if (TimeLine.IsMatch(lines[i]))
                {
                    break;
                }

                textLines.Add(lines[i]);
                i++;
            }

            var cleaned = CleanText(string.Join('\n', textLines));
            if (end > start && cleaned.Length > 0)
            {
                cues.Add(new Cue(start, end, cleaned));
            }
        }

        return cues.OrderBy(cue => cue.Start).ToList();
    }

    /// <summary>
    /// Removes HTML and ASS tags, collapses horizontal whitespace and trims empty lines.
    /// </summary>
    /// <param name="text">Raw cue text.</param>
    /// <returns>Clean cue text with internal newlines preserved.</returns>
    public static string CleanText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var withoutTags = AssTag.Replace(HtmlTag.Replace(text, string.Empty), string.Empty);
        var lines = withoutTags.Split('\n')
            .Select(line => HorizontalSpace.Replace(line, " ").Trim())
            .Where(line => line.Length > 0);

        return string.Join('\n', lines);
    }

    private static TimeSpan ToTimeSpan(Match match, int group)
    {
        var hours = long.Parse(match.Groups[group].Value, System.Globalization.CultureInfo.InvariantCulture);
        var minutes = int.Parse(match.Groups[group + 1].Value, System.Globalization.CultureInfo.InvariantCulture);
        var seconds = int.Parse(match.Groups[group + 2].Value, System.Globalization.CultureInfo.InvariantCulture);
        var fraction = match.Groups[group + 3].Value.PadRight(3, '0')[..3];
        var milliseconds = int.Parse(fraction, System.Globalization.CultureInfo.InvariantCulture);

        return TimeSpan.FromHours(hours)
            + TimeSpan.FromMinutes(minutes)
            + TimeSpan.FromSeconds(seconds)
            + TimeSpan.FromMilliseconds(milliseconds);
    }
}
