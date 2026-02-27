using System.Globalization;

namespace Pdv.Ui.Formatting;

public static class MoneyFormatter
{
    private static readonly CultureInfo Culture = new("pt-BR");

    public static string FormatFromCents(int cents) => (cents / 100m).ToString("C2", Culture);

    public static bool TryParseToCents(string? value, out int cents)
    {
        if (decimal.TryParse(value, Culture, out var parsed) || decimal.TryParse(value, out parsed))
        {
            cents = (int)Math.Round(parsed * 100m, MidpointRounding.AwayFromZero);
            return true;
        }

        cents = 0;
        return false;
    }
}
