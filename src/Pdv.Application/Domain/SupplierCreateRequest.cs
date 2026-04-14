namespace Pdv.Application.Domain;

public sealed class SupplierCreateRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Cnpj { get; init; }
    public string? Contact { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public int AvgDeliveryDays { get; init; }
    public string? Notes { get; init; }
}
