using System.Text;

namespace Pdv.Infrastructure.Utilities;

internal static class SearchPatternHelper
{
    public static string BuildLikePattern(string? query)
    {
        var normalized = NormalizeQuery(query);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder("%");
        for (var i = 0; i < parts.Length; i++)
        {
            if (i > 0)
            {
                builder.Append('%');
            }

            builder.Append(EscapeLikeWildcard(parts[i]));
        }

        builder.Append('%');
        return builder.ToString();
    }

    private static string NormalizeQuery(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return string.Empty;
        }

        return string.Join(' ', query.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string EscapeLikeWildcard(string value)
    {
        return value
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
    }
}
