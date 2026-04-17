namespace Pdv.Application.Domain;

public sealed class PdvSettings
{
    public ProductTextCaseMode ProductTextCase { get; set; } = ProductTextCaseMode.Original;
    public bool AskPrinterBeforePrint { get; set; } = true;
    public string? PreferredPrinterName { get; set; }
    public string ShortcutAddItem { get; set; } = "Enter";
    public string ShortcutFinalizeSale { get; set; } = "F2";
    public string ShortcutSearchProduct { get; set; } = "F3";
    public string ShortcutRemoveItem { get; set; } = "F4";
    public string ShortcutCancelSale { get; set; } = "Space";
}
