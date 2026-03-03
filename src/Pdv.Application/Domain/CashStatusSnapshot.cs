namespace Pdv.Application.Domain;

public sealed class CashStatusSnapshot
{
    public bool IsOpen { get; init; }
    public string? SessionId { get; init; }
    public string BusinessDate { get; init; } = string.Empty;
    public int OpeningAmountCents { get; init; }
    public int SalesTotalCents { get; init; }
    public int WithdrawalsTotalCents { get; init; }
    public int CurrentBalanceCents { get; init; }
    public IReadOnlyList<CashStatusTransaction> Transactions { get; init; } = [];
}

public sealed class CashStatusTransaction
{
    public string Type { get; init; } = string.Empty;
    public string Reference { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public int AmountCents { get; init; }
}
