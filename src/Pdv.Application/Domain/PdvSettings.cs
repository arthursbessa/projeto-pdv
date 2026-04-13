namespace Pdv.Application.Domain;

public sealed class PdvSettings
{
    public decimal DefaultDiscountPercent { get; set; } = 5m;
    public bool AskPrinterBeforePrint { get; set; } = true;
    public string? PreferredPrinterName { get; set; }
    public string ShortcutAddItem { get; set; } = "Enter";
    public string ShortcutFinalizeSale { get; set; } = "F2";
    public string ShortcutSearchProduct { get; set; } = "F3";
    public string ShortcutRemoveItem { get; set; } = "F4";
    public string ShortcutCancelSale { get; set; } = "Escape";
}
