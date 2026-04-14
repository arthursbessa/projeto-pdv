using Pdv.Application.Domain;

namespace Pdv.Ui.Formatting;

public static class ProductTextFormatter
{
    public static string Format(string? value, ProductTextCaseMode mode)
    {
        var text = value ?? string.Empty;
        return mode switch
        {
            ProductTextCaseMode.Uppercase => text.ToUpperInvariant(),
            ProductTextCaseMode.Lowercase => text.ToLowerInvariant(),
            _ => text
        };
    }

    public static string ToDisplayLabel(ProductTextCaseMode mode)
    {
        return mode switch
        {
            ProductTextCaseMode.Uppercase => "Maiusculas",
            ProductTextCaseMode.Lowercase => "Minusculas",
            _ => "Original"
        };
    }

    public static ProductTextCaseMode ParseDisplayLabel(string? label)
    {
        return (label ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "maiusculas" => ProductTextCaseMode.Uppercase,
            "minusculas" => ProductTextCaseMode.Lowercase,
            _ => ProductTextCaseMode.Original
        };
    }
}
