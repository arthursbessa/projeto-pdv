using Pdv.Application.Abstractions;
using Pdv.Application.Domain;
using Pdv.Infrastructure.Persistence;

namespace Pdv.Infrastructure.Repositories;

public sealed class PdvSettingsRepository : IPdvSettingsRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public PdvSettingsRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<PdvSettings> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
SELECT product_text_case,
       ask_printer_before_print,
       preferred_printer_name,
       shortcut_add_item,
       shortcut_finalize_sale,
       shortcut_search_product,
       shortcut_change_quantity,
       shortcut_change_price,
       shortcut_open_payment,
       shortcut_select_customer,
       shortcut_reprint_last_sale,
       shortcut_print_receipt,
       shortcut_remove_item,
       shortcut_cancel_sale,
       default_discount_percent
FROM pdv_settings
WHERE id = 1
LIMIT 1;";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new PdvSettings();
        }

        return new PdvSettings
        {
            ProductTextCase = reader.IsDBNull(0) || !Enum.TryParse<ProductTextCaseMode>(reader.GetString(0), true, out var productTextCase)
                ? ProductTextCaseMode.Original
                : productTextCase,
            AskPrinterBeforePrint = !reader.IsDBNull(1) && reader.GetInt32(1) == 1,
            PreferredPrinterName = reader.IsDBNull(2) ? null : reader.GetString(2),
            ShortcutAddItem = reader.IsDBNull(3) ? "Enter" : reader.GetString(3),
            ShortcutFinalizeSale = reader.IsDBNull(4) ? "F2" : reader.GetString(4),
            ShortcutSearchProduct = reader.IsDBNull(5) ? "F3" : reader.GetString(5),
            ShortcutChangeQuantity = reader.IsDBNull(6) ? "F4" : reader.GetString(6),
            ShortcutChangePrice = reader.IsDBNull(7) ? "F5" : reader.GetString(7),
            ShortcutOpenPayment = reader.IsDBNull(8) ? "F6" : reader.GetString(8),
            ShortcutSelectCustomer = reader.IsDBNull(9) ? "F7" : reader.GetString(9),
            ShortcutReprintLastSale = reader.IsDBNull(10) ? "F8" : reader.GetString(10),
            ShortcutPrintReceipt = reader.IsDBNull(11) ? "F9" : reader.GetString(11),
            ShortcutRemoveItem = reader.IsDBNull(12) ? "Delete" : reader.GetString(12),
            ShortcutCancelSale = reader.IsDBNull(13) ? "Escape" : reader.GetString(13),
            DefaultDiscountPercent = reader.IsDBNull(14) ? 0m : reader.GetDecimal(14)
        };
    }

    public async Task SaveAsync(PdvSettings settings, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO pdv_settings (
    id,
    product_text_case,
    ask_printer_before_print,
    preferred_printer_name,
    shortcut_add_item,
    shortcut_finalize_sale,
    shortcut_search_product,
    shortcut_change_quantity,
    shortcut_change_price,
    shortcut_open_payment,
    shortcut_select_customer,
    shortcut_reprint_last_sale,
    shortcut_print_receipt,
    shortcut_remove_item,
    shortcut_cancel_sale,
    default_discount_percent,
    updated_at)
VALUES (
    1,
    $productTextCase,
    $askPrinterBeforePrint,
    $preferredPrinterName,
    $shortcutAddItem,
    $shortcutFinalizeSale,
    $shortcutSearchProduct,
    $shortcutChangeQuantity,
    $shortcutChangePrice,
    $shortcutOpenPayment,
    $shortcutSelectCustomer,
    $shortcutReprintLastSale,
    $shortcutPrintReceipt,
    $shortcutRemoveItem,
    $shortcutCancelSale,
    $defaultDiscountPercent,
    $updatedAt)
ON CONFLICT(id) DO UPDATE SET
    product_text_case = excluded.product_text_case,
    ask_printer_before_print = excluded.ask_printer_before_print,
    preferred_printer_name = excluded.preferred_printer_name,
    shortcut_add_item = excluded.shortcut_add_item,
    shortcut_finalize_sale = excluded.shortcut_finalize_sale,
    shortcut_search_product = excluded.shortcut_search_product,
    shortcut_change_quantity = excluded.shortcut_change_quantity,
    shortcut_change_price = excluded.shortcut_change_price,
    shortcut_open_payment = excluded.shortcut_open_payment,
    shortcut_select_customer = excluded.shortcut_select_customer,
    shortcut_reprint_last_sale = excluded.shortcut_reprint_last_sale,
    shortcut_print_receipt = excluded.shortcut_print_receipt,
    shortcut_remove_item = excluded.shortcut_remove_item,
    shortcut_cancel_sale = excluded.shortcut_cancel_sale,
    default_discount_percent = excluded.default_discount_percent,
    updated_at = excluded.updated_at;";

        command.Parameters.AddWithValue("$productTextCase", settings.ProductTextCase.ToString());
        command.Parameters.AddWithValue("$askPrinterBeforePrint", settings.AskPrinterBeforePrint ? 1 : 0);
        command.Parameters.AddWithValue("$preferredPrinterName", (object?)settings.PreferredPrinterName ?? DBNull.Value);
        command.Parameters.AddWithValue("$shortcutAddItem", settings.ShortcutAddItem);
        command.Parameters.AddWithValue("$shortcutFinalizeSale", settings.ShortcutFinalizeSale);
        command.Parameters.AddWithValue("$shortcutSearchProduct", settings.ShortcutSearchProduct);
        command.Parameters.AddWithValue("$shortcutChangeQuantity", settings.ShortcutChangeQuantity);
        command.Parameters.AddWithValue("$shortcutChangePrice", settings.ShortcutChangePrice);
        command.Parameters.AddWithValue("$shortcutOpenPayment", settings.ShortcutOpenPayment);
        command.Parameters.AddWithValue("$shortcutSelectCustomer", settings.ShortcutSelectCustomer);
        command.Parameters.AddWithValue("$shortcutReprintLastSale", settings.ShortcutReprintLastSale);
        command.Parameters.AddWithValue("$shortcutPrintReceipt", settings.ShortcutPrintReceipt);
        command.Parameters.AddWithValue("$shortcutRemoveItem", settings.ShortcutRemoveItem);
        command.Parameters.AddWithValue("$shortcutCancelSale", settings.ShortcutCancelSale);
        command.Parameters.AddWithValue("$defaultDiscountPercent", settings.DefaultDiscountPercent);
        command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
