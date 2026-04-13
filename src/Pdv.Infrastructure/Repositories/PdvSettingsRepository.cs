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
SELECT default_discount_percent,
       ask_printer_before_print,
       preferred_printer_name,
       shortcut_add_item,
       shortcut_finalize_sale,
       shortcut_search_product,
       shortcut_remove_item,
       shortcut_cancel_sale
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
            DefaultDiscountPercent = reader.IsDBNull(0) ? 5m : reader.GetDecimal(0),
            AskPrinterBeforePrint = !reader.IsDBNull(1) && reader.GetInt32(1) == 1,
            PreferredPrinterName = reader.IsDBNull(2) ? null : reader.GetString(2),
            ShortcutAddItem = reader.IsDBNull(3) ? "Enter" : reader.GetString(3),
            ShortcutFinalizeSale = reader.IsDBNull(4) ? "F2" : reader.GetString(4),
            ShortcutSearchProduct = reader.IsDBNull(5) ? "F3" : reader.GetString(5),
            ShortcutRemoveItem = reader.IsDBNull(6) ? "F4" : reader.GetString(6),
            ShortcutCancelSale = reader.IsDBNull(7) ? "Escape" : reader.GetString(7)
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
    default_discount_percent,
    ask_printer_before_print,
    preferred_printer_name,
    shortcut_add_item,
    shortcut_finalize_sale,
    shortcut_search_product,
    shortcut_remove_item,
    shortcut_cancel_sale,
    updated_at)
VALUES (
    1,
    $defaultDiscountPercent,
    $askPrinterBeforePrint,
    $preferredPrinterName,
    $shortcutAddItem,
    $shortcutFinalizeSale,
    $shortcutSearchProduct,
    $shortcutRemoveItem,
    $shortcutCancelSale,
    $updatedAt)
ON CONFLICT(id) DO UPDATE SET
    default_discount_percent = excluded.default_discount_percent,
    ask_printer_before_print = excluded.ask_printer_before_print,
    preferred_printer_name = excluded.preferred_printer_name,
    shortcut_add_item = excluded.shortcut_add_item,
    shortcut_finalize_sale = excluded.shortcut_finalize_sale,
    shortcut_search_product = excluded.shortcut_search_product,
    shortcut_remove_item = excluded.shortcut_remove_item,
    shortcut_cancel_sale = excluded.shortcut_cancel_sale,
    updated_at = excluded.updated_at;";

        command.Parameters.AddWithValue("$defaultDiscountPercent", settings.DefaultDiscountPercent);
        command.Parameters.AddWithValue("$askPrinterBeforePrint", settings.AskPrinterBeforePrint ? 1 : 0);
        command.Parameters.AddWithValue("$preferredPrinterName", (object?)settings.PreferredPrinterName ?? DBNull.Value);
        command.Parameters.AddWithValue("$shortcutAddItem", settings.ShortcutAddItem);
        command.Parameters.AddWithValue("$shortcutFinalizeSale", settings.ShortcutFinalizeSale);
        command.Parameters.AddWithValue("$shortcutSearchProduct", settings.ShortcutSearchProduct);
        command.Parameters.AddWithValue("$shortcutRemoveItem", settings.ShortcutRemoveItem);
        command.Parameters.AddWithValue("$shortcutCancelSale", settings.ShortcutCancelSale);
        command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
