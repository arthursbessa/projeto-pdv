namespace Pdv.Application.Domain;

public sealed class ProductAdminItem
{
    public string? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public int PriceCents { get; set; }
    public int CostPriceCents { get; set; }
    public int StockQuantity { get; set; }
    public int MinStock { get; set; }
    public string Unit { get; set; } = "un";
    public bool Active { get; set; } = true;
}
