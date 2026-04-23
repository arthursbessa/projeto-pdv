namespace Pdv.Application.Domain;

public sealed class PdvSettings
{
    public ProductTextCaseMode ProductTextCase { get; set; } = ProductTextCaseMode.Original;
    public bool AskPrinterBeforePrint { get; set; } = true;
    public string? PreferredPrinterName { get; set; }
    public string ShortcutAddItem { get; set; } = "Enter";
    public string ShortcutFinalizeSale { get; set; } = "F2";
    public string ShortcutSearchProduct { get; set; } = "F3";
    public string ShortcutChangeQuantity { get; set; } = "F4";
    public string ShortcutChangePrice { get; set; } = "F5";
    public string ShortcutOpenPayment { get; set; } = "F6";
    public string ShortcutSelectCustomer { get; set; } = "F7";
    public string ShortcutReprintLastSale { get; set; } = "F8";
    public string ShortcutPrintReceipt { get; set; } = "F9";
    public string ShortcutRemoveItem { get; set; } = "Delete";
    public string ShortcutCancelSale { get; set; } = "Escape";
    public decimal DefaultDiscountPercent { get; set; }
}
