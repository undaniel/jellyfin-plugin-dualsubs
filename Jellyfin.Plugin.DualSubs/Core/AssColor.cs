using System.Globalization;

namespace Jellyfin.Plugin.DualSubs.Core;

/// <summary>
/// Converts CSS hex colors to ASS colors.
/// </summary>
public static class AssColor
{
    /// <summary>
    /// Converts <c>#RRGGBB</c> to <c>&amp;H00BBGGRR</c>.
    /// </summary>
    /// <param name="hex">Color in CSS form, with or without the leading #.</param>
    /// <returns>ASS color literal.</returns>
    public static string FromHtml(string hex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hex);

        var value = hex.Trim().TrimStart('#');
        if (value.Length != 6 || !int.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
        {
            throw new ArgumentException($"Invalid color '{hex}'. Expected #RRGGBB.", nameof(hex));
        }

        var red = value[..2];
        var green = value[2..4];
        var blue = value[4..];
        return $"&H00{blue}{green}{red}".ToUpperInvariant();
    }
}
