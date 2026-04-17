namespace Pdv.Application.Utilities;

public static class TextNormalization
{
    public static string TrimToEmpty(string? value) => value?.Trim() ?? string.Empty;

    public static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string DigitsOnly(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var chars = value.Where(char.IsDigit).ToArray();
        return new string(chars);
    }

    public static bool IsCpfOrCnpj(string? value)
    {
        var digits = DigitsOnly(value);
        return digits.Length is 11 or 14;
    }

    public static string? FormatTaxId(string? value)
    {
        var digits = DigitsOnly(value);
        if (digits.Length == 0)
        {
            return null;
        }

        return FormatTaxIdDigits(digits);
    }

    public static string FormatTaxIdPartial(string? value)
    {
        var digits = DigitsOnly(value);
        if (digits.Length == 0)
        {
            return string.Empty;
        }

        return FormatTaxIdDigits(digits);
    }

    private static string FormatTaxIdDigits(string digits)
    {
        if (digits.Length <= 11)
        {
            return FormatCpfPartial(digits);
        }

        return FormatCnpjPartial(digits);
    }

    private static string FormatCpfPartial(string digits)
    {
        if (digits.Length <= 3)
        {
            return digits;
        }

        if (digits.Length <= 6)
        {
            return $"{digits[..3]}.{digits[3..]}";
        }

        if (digits.Length <= 9)
        {
            return $"{digits[..3]}.{digits[3..6]}.{digits[6..]}";
        }

        if (digits.Length <= 11)
        {
            return $"{digits[..3]}.{digits[3..6]}.{digits[6..9]}-{digits[9..]}";
        }

        return digits[..11];
    }

    private static string FormatCnpjPartial(string digits)
    {
        if (digits.Length <= 2)
        {
            return digits;
        }

        if (digits.Length <= 5)
        {
            return $"{digits[..2]}.{digits[2..]}";
        }

        if (digits.Length <= 8)
        {
            return $"{digits[..2]}.{digits[2..5]}.{digits[5..]}";
        }

        if (digits.Length <= 12)
        {
            return $"{digits[..2]}.{digits[2..5]}.{digits[5..8]}/{digits[8..]}";
        }

        if (digits.Length <= 14)
        {
            return $"{digits[..2]}.{digits[2..5]}.{digits[5..8]}/{digits[8..12]}-{digits[12..]}";
        }

        return digits[..14];
    }
}
