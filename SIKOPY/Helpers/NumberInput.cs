using System.Globalization;
using System.Text.RegularExpressions;

namespace SIKOPY.Helpers;

/// <summary>
/// Parses numbers typed by users. Indonesian notation is the default
/// ("1.000" is one thousand, "25,5" is twenty-five and a half), but a single
/// "." followed by other than three digits is read as a decimal point, so
/// "25.5" and "40.25" also work.
/// </summary>
public static partial class NumberInput
{
    public static bool TryParseDecimal(string? text, out decimal value)
    {
        value = 0;
        var normalized = (text ?? string.Empty).Trim().Replace(" ", string.Empty);
        if (normalized.Length == 0)
            return false;

        if (normalized.Contains(','))
        {
            // "1.250,5" → dots group thousands, the comma is the decimal separator.
            if (normalized.IndexOf(',') != normalized.LastIndexOf(',') ||
                (normalized.Contains('.') && !IndonesianGroupedWithDecimal().IsMatch(normalized)))
                return false;
            normalized = normalized.Replace(".", string.Empty).Replace(',', '.');
        }
        else if (ThousandsGroupedOnly().IsMatch(normalized))
        {
            // "1.000" or "12.500.000" → thousands separators.
            normalized = normalized.Replace(".", string.Empty);
        }

        return decimal.TryParse(
            normalized,
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out value);
    }

    /// <summary>Parses a Rupiah amount such as "200000", "200.000" or "Rp 200.000".</summary>
    public static bool TryParseRupiah(string? text, out decimal value) =>
        TryParseDecimal(
            (text ?? string.Empty).Replace("Rp", string.Empty, System.StringComparison.OrdinalIgnoreCase),
            out value);

    public static bool TryParseInteger(string? text, out int value)
    {
        value = 0;
        return TryParseDecimal(text, out var number) &&
               decimal.Truncate(number) == number &&
               number <= int.MaxValue &&
               int.TryParse(number.ToString(CultureInfo.InvariantCulture), out value);
    }

    [GeneratedRegex(@"^\d{1,3}(\.\d{3})+$")]
    private static partial Regex ThousandsGroupedOnly();

    [GeneratedRegex(@"^\d{1,3}(\.\d{3})+,\d+$")]
    private static partial Regex IndonesianGroupedWithDecimal();
}
