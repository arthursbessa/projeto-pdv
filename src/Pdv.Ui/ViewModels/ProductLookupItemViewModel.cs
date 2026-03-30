namespace Pdv.Ui.ViewModels;

public sealed class ProductLookupItemViewModel
{
    public required string Id { get; init; }
    public required string Barcode { get; init; }
    public required string Description { get; init; }
    public required string PriceFormatted { get; init; }
    public int PriceCents { get; init; }
}
