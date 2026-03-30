namespace Pdv.Application.Domain;

public sealed class SaleSyncResult
{
    public string RemoteSaleId { get; init; } = string.Empty;
    public int? SaleNumber { get; init; }
}
