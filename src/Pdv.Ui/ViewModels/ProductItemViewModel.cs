namespace Pdv.Ui.ViewModels;

public sealed class ProductItemViewModel
{
    public required string Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public required string Barcode { get; set; }
    public required string Description { get; set; }
    public required string PriceInput { get; set; }
    public bool Active { get; set; }
}
