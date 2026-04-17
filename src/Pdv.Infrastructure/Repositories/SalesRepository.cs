using System.Globalization;
using Microsoft.Data.Sqlite;
using Pdv.Application.Abstractions;
using Pdv.Application.Domain;
using Pdv.Infrastructure.Persistence;

namespace Pdv.Infrastructure.Repositories;

public sealed class SalesRepository : ISalesRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public SalesRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task SaveSaleWithOutboxAsync(Sale sale, string outboxPayloadJson, string? cashRegisterSessionId = null, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        using var tx = connection.BeginTransaction();

        var saleCommand = connection.CreateCommand();
        saleCommand.Transaction = tx;
        saleCommand.CommandText = @"
INSERT INTO sales (
    id,
    created_at,
    total_cents,
    payment_method,
    payment_method_id,
    received_amount_cents,
    change_amount_cents,
    status,
    customer_id,
    operator_id,
    cash_register_session_id,
    remote_sale_id,
    sale_number,
    discount_cents,
    discount_percent,
    receipt_requested,
    receipt_tax_id)
VALUES (
    $id,
    $createdAt,
    $totalCents,
    $paymentMethod,
    $paymentMethodId,
    $receivedAmountCents,
    $changeAmountCents,
    $status,
    $customerId,
    $operatorId,
    $cashRegisterSessionId,
    $remoteSaleId,
    $saleNumber,
    $discountCents,
    $discountPercent,
    $receiptRequested,
    $receiptTaxId);";
        saleCommand.Parameters.AddWithValue("$id", sale.SaleId.ToString());
        saleCommand.Parameters.AddWithValue("$createdAt", sale.CreatedAt.ToString("O"));
        saleCommand.Parameters.AddWithValue("$totalCents", sale.TotalCents);
        saleCommand.Parameters.AddWithValue("$paymentMethod", GetPaymentCode(sale.PaymentMethod));
        saleCommand.Parameters.AddWithValue("$paymentMethodId", (int)sale.PaymentMethod);
        saleCommand.Parameters.AddWithValue("$receivedAmountCents", (object?)sale.ReceivedAmountCents ?? DBNull.Value);
        saleCommand.Parameters.AddWithValue("$changeAmountCents", sale.ChangeAmountCents);
        saleCommand.Parameters.AddWithValue("$status", sale.Status);
        saleCommand.Parameters.AddWithValue("$customerId", (object?)sale.CustomerId ?? DBNull.Value);
        saleCommand.Parameters.AddWithValue("$operatorId", (object?)sale.OperatorId ?? DBNull.Value);
        saleCommand.Parameters.AddWithValue("$cashRegisterSessionId", (object?)(cashRegisterSessionId ?? sale.CashRegisterSessionId) ?? DBNull.Value);
        saleCommand.Parameters.AddWithValue("$remoteSaleId", (object?)sale.RemoteSaleId ?? DBNull.Value);
        saleCommand.Parameters.AddWithValue("$saleNumber", (object?)sale.SaleNumber ?? DBNull.Value);
        saleCommand.Parameters.AddWithValue("$discountCents", sale.DiscountCents);
        saleCommand.Parameters.AddWithValue("$discountPercent", sale.DiscountPercent);
        saleCommand.Parameters.AddWithValue("$receiptRequested", sale.ReceiptRequested ? 1 : 0);
        saleCommand.Parameters.AddWithValue("$receiptTaxId", (object?)sale.ReceiptTaxId ?? DBNull.Value);
        await saleCommand.ExecuteNonQueryAsync(cancellationToken);

        foreach (var item in sale.Items)
        {
            var itemId = item.SaleItemId ?? Guid.NewGuid().ToString();
            var itemCommand = connection.CreateCommand();
            itemCommand.Transaction = tx;
            itemCommand.CommandText = @"
INSERT INTO sale_items (id, sale_id, product_id, barcode, description, quantity, price_cents, subtotal_cents)
VALUES ($id, $saleId, $productId, $barcode, $description, $quantity, $priceCents, $subtotalCents);";
            itemCommand.Parameters.AddWithValue("$id", itemId);
            itemCommand.Parameters.AddWithValue("$saleId", sale.SaleId.ToString());
            itemCommand.Parameters.AddWithValue("$productId", item.ProductId);
            itemCommand.Parameters.AddWithValue("$barcode", item.Barcode);
            itemCommand.Parameters.AddWithValue("$description", item.Description);
            itemCommand.Parameters.AddWithValue("$quantity", item.Quantity);
            itemCommand.Parameters.AddWithValue("$priceCents", item.PriceCents);
            itemCommand.Parameters.AddWithValue("$subtotalCents", item.SubtotalCents);
            await itemCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        var paymentCommand = connection.CreateCommand();
        paymentCommand.Transaction = tx;
        paymentCommand.CommandText = @"
INSERT INTO sale_payments (id, sale_id, payment_method_id, amount_cents, paid_at)
VALUES ($id, $saleId, $paymentMethodId, $amountCents, $paidAt);";
        paymentCommand.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        paymentCommand.Parameters.AddWithValue("$saleId", sale.SaleId.ToString());
        paymentCommand.Parameters.AddWithValue("$paymentMethodId", (int)sale.PaymentMethod);
        paymentCommand.Parameters.AddWithValue("$amountCents", sale.TotalCents);
        paymentCommand.Parameters.AddWithValue("$paidAt", DateTimeOffset.UtcNow.ToString("O"));
        await paymentCommand.ExecuteNonQueryAsync(cancellationToken);

        await InsertOutboxEventAsync(connection, tx, "SaleCreated", outboxPayloadJson, cancellationToken);
        tx.Commit();
    }

    public async Task<IReadOnlyList<SaleHistoryEntry>> GetHistoryAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var localStart = date.Date;
        var localOffset = TimeZoneInfo.Local.GetUtcOffset(localStart);
        var start = new DateTimeOffset(localStart, localOffset).ToUniversalTime();
        var end = start.AddDays(1);

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
SELECT s.id,
       COALESCE(s.remote_sale_id, s.id) AS sale_identifier,
       s.created_at,
       s.payment_method,
       s.total_cents,
       s.received_amount_cents,
       s.change_amount_cents,
       COALESCE(c.name, 'Consumidor final') AS customer_name,
       COALESCE(u.full_name, '-') AS cashier_name,
       COALESCE(GROUP_CONCAT(si.description || ' x' || si.quantity, ' | '), 'Venda sem itens') AS products_summary,
       COALESCE(s.status, 'COMPLETED') AS sale_status
FROM sales s
LEFT JOIN sale_items si ON si.sale_id = s.id
LEFT JOIN customers c ON c.id = s.customer_id
LEFT JOIN users u ON u.id = s.operator_id
WHERE s.created_at >= $start AND s.created_at < $end
GROUP BY s.id, s.remote_sale_id, s.created_at, s.payment_method, s.total_cents, s.received_amount_cents, s.change_amount_cents, c.name, u.full_name, s.status
ORDER BY s.created_at DESC;";
        command.Parameters.AddWithValue("$start", start.ToString("O"));
        command.Parameters.AddWithValue("$end", end.ToString("O"));

        var result = new List<SaleHistoryEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new SaleHistoryEntry
            {
                SaleId = Guid.Parse(reader.GetString(0)),
                SaleIdentifier = reader.GetString(1),
                CreatedAt = DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture),
                PaymentMethod = ParsePaymentMethod(reader.GetString(3)),
                TotalCents = reader.GetInt32(4),
                ReceivedAmountCents = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5),
                ChangeAmountCents = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                CustomerName = reader.GetString(7),
                CashierName = reader.GetString(8),
                ProductsSummary = reader.GetString(9),
                Status = reader.GetString(10)
            });
        }

        return result;
    }

    public async Task<Sale?> FindByIdAsync(Guid saleId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        var saleCommand = connection.CreateCommand();
        saleCommand.CommandText = @"
SELECT s.id,
       s.created_at,
       s.payment_method,
       s.received_amount_cents,
       s.change_amount_cents,
       s.customer_id,
       COALESCE(c.name, 'Consumidor final') AS customer_name,
       s.operator_id,
       COALESCE(u.full_name, '-') AS operator_name,
       COALESCE(s.status, 'COMPLETED') AS sale_status,
       s.remote_sale_id,
       s.sale_number,
       s.cash_register_session_id,
       COALESCE(s.discount_percent, 0),
       COALESCE(s.discount_cents, 0),
       COALESCE(s.receipt_requested, 0),
       s.receipt_tax_id
FROM sales s
LEFT JOIN customers c ON c.id = s.customer_id
LEFT JOIN users u ON u.id = s.operator_id
WHERE s.id = $id
LIMIT 1;";
        saleCommand.Parameters.AddWithValue("$id", saleId.ToString());

        await using var saleReader = await saleCommand.ExecuteReaderAsync(cancellationToken);
        if (!await saleReader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var saleMetadata = new
        {
            SaleId = Guid.Parse(saleReader.GetString(0)),
            CreatedAt = DateTimeOffset.Parse(saleReader.GetString(1), CultureInfo.InvariantCulture),
            PaymentMethod = ParsePaymentMethod(saleReader.GetString(2)),
            ReceivedAmountCents = saleReader.IsDBNull(3) ? (int?)null : saleReader.GetInt32(3),
            ChangeAmountCents = saleReader.IsDBNull(4) ? 0 : saleReader.GetInt32(4),
            CustomerId = saleReader.IsDBNull(5) ? null : saleReader.GetString(5),
            CustomerName = saleReader.GetString(6),
            OperatorId = saleReader.IsDBNull(7) ? null : saleReader.GetString(7),
            OperatorName = saleReader.GetString(8),
            Status = saleReader.GetString(9),
            RemoteSaleId = saleReader.IsDBNull(10) ? null : saleReader.GetString(10),
            SaleNumber = saleReader.IsDBNull(11) ? (int?)null : saleReader.GetInt32(11),
            CashRegisterSessionId = saleReader.IsDBNull(12) ? null : saleReader.GetString(12),
            DiscountPercent = saleReader.IsDBNull(13) ? 0m : saleReader.GetDecimal(13),
            DiscountCents = saleReader.IsDBNull(14) ? 0 : saleReader.GetInt32(14),
            ReceiptRequested = !saleReader.IsDBNull(15) && saleReader.GetInt32(15) == 1,
            ReceiptTaxId = saleReader.IsDBNull(16) ? null : saleReader.GetString(16)
        };

        await saleReader.DisposeAsync();

        var itemsCommand = connection.CreateCommand();
        itemsCommand.CommandText = @"
SELECT si.id,
       si.product_id,
       si.barcode,
       si.description,
       si.quantity,
       si.price_cents,
       COALESCE(SUM(sri.quantity), 0) AS refunded_quantity
FROM sale_items si
LEFT JOIN sale_refund_items sri ON sri.sale_item_id = si.id
LEFT JOIN sale_refunds sr ON sr.id = sri.refund_id
WHERE si.sale_id = $saleId
GROUP BY si.id, si.product_id, si.barcode, si.description, si.quantity, si.price_cents
ORDER BY si.rowid;";
        itemsCommand.Parameters.AddWithValue("$saleId", saleId.ToString());

        var items = new List<SaleItem>();
        await using var itemsReader = await itemsCommand.ExecuteReaderAsync(cancellationToken);
        while (await itemsReader.ReadAsync(cancellationToken))
        {
            var item = new SaleItem
            {
                SaleItemId = itemsReader.GetString(0),
                ProductId = itemsReader.GetString(1),
                Barcode = itemsReader.GetString(2),
                Description = itemsReader.GetString(3),
                PriceCents = itemsReader.GetInt32(5),
                RefundedQuantity = itemsReader.GetInt32(6)
            };

            item.SetQuantity(itemsReader.GetInt32(4));
            items.Add(item);
        }

        return new Sale
        {
            SaleId = saleMetadata.SaleId,
            CreatedAt = saleMetadata.CreatedAt,
            PaymentMethod = saleMetadata.PaymentMethod,
            CustomerId = saleMetadata.CustomerId,
            CustomerName = saleMetadata.CustomerName,
            OperatorId = saleMetadata.OperatorId,
            OperatorName = saleMetadata.OperatorName,
            ReceivedAmountCents = saleMetadata.ReceivedAmountCents,
            ChangeAmountCents = saleMetadata.ChangeAmountCents,
            Status = saleMetadata.Status,
            RemoteSaleId = saleMetadata.RemoteSaleId,
            SaleNumber = saleMetadata.SaleNumber,
            CashRegisterSessionId = saleMetadata.CashRegisterSessionId,
            DiscountPercent = saleMetadata.DiscountPercent,
            DiscountCents = saleMetadata.DiscountCents,
            ReceiptRequested = saleMetadata.ReceiptRequested,
            ReceiptTaxId = saleMetadata.ReceiptTaxId,
            Items = items
        };
    }

    public async Task SaveRemoteSaleReferenceAsync(Guid localSaleId, string remoteSaleId, int? saleNumber, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
UPDATE sales
SET remote_sale_id = $remoteSaleId,
    sale_number = $saleNumber
WHERE id = $id;";
        command.Parameters.AddWithValue("$remoteSaleId", remoteSaleId);
        command.Parameters.AddWithValue("$saleNumber", (object?)saleNumber ?? DBNull.Value);
        command.Parameters.AddWithValue("$id", localSaleId.ToString());

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public Task SaveRefundAsync(Guid saleId, string reason, IReadOnlyCollection<SaleRefundItem> items, string? operatorId, CancellationToken cancellationToken = default)
        => SaveRefundInternalAsync(saleId, reason, items, null, operatorId, cancellationToken);

    public Task SaveRefundWithOutboxAsync(Guid saleId, string reason, IReadOnlyCollection<SaleRefundItem> items, string outboxPayloadJson, string? operatorId, CancellationToken cancellationToken = default)
        => SaveRefundInternalAsync(saleId, reason, items, outboxPayloadJson, operatorId, cancellationToken);

    private async Task SaveRefundInternalAsync(
        Guid saleId,
        string reason,
        IReadOnlyCollection<SaleRefundItem> items,
        string? outboxPayloadJson,
        string? operatorId,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            throw new InvalidOperationException("Selecione ao menos um item para devolucao.");
        }

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        using var tx = connection.BeginTransaction();

        var saleItemsState = new Dictionary<string, (string ProductId, int Quantity, int RefundedQuantity)>(StringComparer.OrdinalIgnoreCase);
        var loadItemsCommand = connection.CreateCommand();
        loadItemsCommand.Transaction = tx;
        loadItemsCommand.CommandText = @"
SELECT si.id,
       si.product_id,
       si.quantity,
       COALESCE(SUM(sri.quantity), 0) AS refunded_quantity
FROM sale_items si
LEFT JOIN sale_refund_items sri ON sri.sale_item_id = si.id
LEFT JOIN sale_refunds sr ON sr.id = sri.refund_id
WHERE si.sale_id = $saleId
GROUP BY si.id, si.product_id, si.quantity;";
        loadItemsCommand.Parameters.AddWithValue("$saleId", saleId.ToString());

        await using (var itemsReader = await loadItemsCommand.ExecuteReaderAsync(cancellationToken))
        {
            while (await itemsReader.ReadAsync(cancellationToken))
            {
                saleItemsState[itemsReader.GetString(0)] = (
                    itemsReader.GetString(1),
                    itemsReader.GetInt32(2),
                    itemsReader.GetInt32(3));
            }
        }

        if (saleItemsState.Count == 0)
        {
            throw new InvalidOperationException("Venda nao encontrada para devolucao.");
        }

        foreach (var item in items)
        {
            if (!saleItemsState.TryGetValue(item.SaleItemId, out var state))
            {
                throw new InvalidOperationException("Item da venda nao encontrado para devolucao.");
            }

            if (item.Quantity <= 0)
            {
                throw new InvalidOperationException("Quantidade de devolucao invalida.");
            }

            var remaining = state.Quantity - state.RefundedQuantity;
            if (item.Quantity > remaining)
            {
                throw new InvalidOperationException("A quantidade de devolucao excede o disponivel para o item.");
            }
        }

        var refundId = Guid.NewGuid().ToString();
        var refundCommand = connection.CreateCommand();
        refundCommand.Transaction = tx;
        refundCommand.CommandText = @"
INSERT INTO sale_refunds (id, sale_id, reason, created_at, operator_id, status, synced_at)
VALUES ($id, $saleId, $reason, $createdAt, $operatorId, 'PENDING', NULL);";
        refundCommand.Parameters.AddWithValue("$id", refundId);
        refundCommand.Parameters.AddWithValue("$saleId", saleId.ToString());
        refundCommand.Parameters.AddWithValue("$reason", reason);
        refundCommand.Parameters.AddWithValue("$createdAt", DateTimeOffset.UtcNow.ToString("O"));
        refundCommand.Parameters.AddWithValue("$operatorId", (object?)operatorId ?? DBNull.Value);
        await refundCommand.ExecuteNonQueryAsync(cancellationToken);

        foreach (var item in items)
        {
            var refundItemCommand = connection.CreateCommand();
            refundItemCommand.Transaction = tx;
            refundItemCommand.CommandText = @"
INSERT INTO sale_refund_items (id, refund_id, sale_item_id, product_id, quantity)
VALUES ($id, $refundId, $saleItemId, $productId, $quantity);";
            refundItemCommand.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
            refundItemCommand.Parameters.AddWithValue("$refundId", refundId);
            refundItemCommand.Parameters.AddWithValue("$saleItemId", item.SaleItemId);
            refundItemCommand.Parameters.AddWithValue("$productId", item.ProductId);
            refundItemCommand.Parameters.AddWithValue("$quantity", item.Quantity);
            await refundItemCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(outboxPayloadJson))
        {
            await InsertOutboxEventAsync(connection, tx, "SaleRefundCreated", outboxPayloadJson, cancellationToken);
        }

        var status = await CalculateSaleStatusAsync(connection, tx, saleId, cancellationToken);
        var updateSaleCommand = connection.CreateCommand();
        updateSaleCommand.Transaction = tx;
        updateSaleCommand.CommandText = @"
UPDATE sales
SET status = $status
WHERE id = $saleId;";
        updateSaleCommand.Parameters.AddWithValue("$status", status);
        updateSaleCommand.Parameters.AddWithValue("$saleId", saleId.ToString());
        await updateSaleCommand.ExecuteNonQueryAsync(cancellationToken);

        tx.Commit();
    }

    private static async Task InsertOutboxEventAsync(SqliteConnection connection, SqliteTransaction tx, string type, string payloadJson, CancellationToken cancellationToken)
    {
        var outboxCommand = connection.CreateCommand();
        outboxCommand.Transaction = tx;
        outboxCommand.CommandText = @"
INSERT INTO outbox_events (id, type, payload_json, status, attempts, next_retry_at, last_error, created_at, sent_at)
VALUES ($id, $type, $payloadJson, $status, $attempts, $nextRetryAt, $lastError, $createdAt, $sentAt);";
        outboxCommand.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        outboxCommand.Parameters.AddWithValue("$type", type);
        outboxCommand.Parameters.AddWithValue("$payloadJson", payloadJson);
        outboxCommand.Parameters.AddWithValue("$status", "Pending");
        outboxCommand.Parameters.AddWithValue("$attempts", 0);
        outboxCommand.Parameters.AddWithValue("$nextRetryAt", DBNull.Value);
        outboxCommand.Parameters.AddWithValue("$lastError", DBNull.Value);
        outboxCommand.Parameters.AddWithValue("$createdAt", DateTimeOffset.UtcNow.ToString("O"));
        outboxCommand.Parameters.AddWithValue("$sentAt", DBNull.Value);
        await outboxCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<string> CalculateSaleStatusAsync(SqliteConnection connection, SqliteTransaction tx, Guid saleId, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = @"
SELECT
    (SELECT COALESCE(SUM(quantity), 0) FROM sale_items WHERE sale_id = $saleId) AS sold_quantity,
    (SELECT COALESCE(SUM(sri.quantity), 0)
     FROM sale_refund_items sri
     INNER JOIN sale_refunds sr ON sr.id = sri.refund_id
     WHERE sr.sale_id = $saleId) AS refunded_quantity;";
        command.Parameters.AddWithValue("$saleId", saleId.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return "COMPLETED";
        }

        var soldQuantity = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
        var refundedQuantity = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
        if (soldQuantity > 0 && refundedQuantity >= soldQuantity)
        {
            return "REFUNDED";
        }

        return refundedQuantity > 0 ? "PARTIALLY_REFUNDED" : "COMPLETED";
    }

    private static string GetPaymentCode(PaymentMethod paymentMethod)
    {
        return paymentMethod switch
        {
            PaymentMethod.Cash => "cash",
            PaymentMethod.CreditCard => "credit_card",
            PaymentMethod.DebitCard => "debit_card",
            PaymentMethod.Pix => "pix",
            _ => paymentMethod.ToString().ToLowerInvariant()
        };
    }

    private static PaymentMethod ParsePaymentMethod(string value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "cash" or "dinheiro" => PaymentMethod.Cash,
            "credit_card" or "credit" or "credito" or "cartao" or "cartão" => PaymentMethod.CreditCard,
            "debit_card" or "debit" or "debito" or "débito" => PaymentMethod.DebitCard,
            _ => PaymentMethod.Pix
        };
    }
}
