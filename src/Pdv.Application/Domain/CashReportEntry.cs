namespace Pdv.Application.Domain;

public sealed class CashReportEntry
{
    public string Type { get; init; } = string.Empty;
    public string ReferenceId { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public int AmountCents { get; init; }
}
