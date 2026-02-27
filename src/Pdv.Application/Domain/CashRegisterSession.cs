namespace Pdv.Application.Domain;

public sealed class CashRegisterSession
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public DateTimeOffset OpenedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ClosedAt { get; init; }
    public int OpeningAmountCents { get; init; }
    public int? ClosingAmountCents { get; init; }
    public string Status { get; init; } = "OPEN";
    public string BusinessDate { get; init; } = DateTimeOffset.Now.ToString("yyyy-MM-dd");
    public string OpenedByUserId { get; init; } = string.Empty;
    public string? ClosedByUserId { get; init; }
}
