using System.Globalization;

namespace Pdv.Ui.Formatting;

public static class MoneyFormatter
{
    private static readonly CultureInfo Culture = new("pt-BR");

    public static string FormatFromCents(int cents) => (cents / 100m).ToString("C2", Culture);

    public static bool TryParseToCents(string? value, out int cents)
    {
        var sanitized = (value ?? string.Empty)
            .Replace("R$", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();

        if (decimal.TryParse(sanitized, NumberStyles.Currency, Culture, out var parsed)
            || decimal.TryParse(sanitized, NumberStyles.Any, CultureInfo.InvariantCulture, out parsed)
            || decimal.TryParse(sanitized, out parsed))
        {
            cents = (int)Math.Round(parsed * 100m, MidpointRounding.AwayFromZero);
            return true;
        }

        cents = 0;
        return false;
    }
}
