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
INSERT INTO sales (id, created_at, total_cents, payment_method, payment_method_id, status, cash_register_session_id)
VALUES ($id, $createdAt, $totalCents, $paymentMethod, $paymentMethodId, 'COMPLETED', $cashRegisterSessionId);";
        saleCommand.Parameters.AddWithValue("$id", sale.SaleId.ToString());
        saleCommand.Parameters.AddWithValue("$createdAt", sale.CreatedAt.ToString("O"));
        saleCommand.Parameters.AddWithValue("$totalCents", sale.TotalCents);
        saleCommand.Parameters.AddWithValue("$paymentMethod", sale.PaymentMethod.ToString());
        saleCommand.Parameters.AddWithValue("$paymentMethodId", (int)sale.PaymentMethod);
        saleCommand.Parameters.AddWithValue("$cashRegisterSessionId", (object?)cashRegisterSessionId ?? DBNull.Value);
        await saleCommand.ExecuteNonQueryAsync(cancellationToken);

        foreach (var item in sale.Items)
        {
            var itemCommand = connection.CreateCommand();
            itemCommand.Transaction = tx;
            itemCommand.CommandText = @"
INSERT INTO sale_items (id, sale_id, product_id, barcode, description, quantity, price_cents, subtotal_cents)
VALUES ($id, $saleId, $productId, $barcode, $description, $quantity, $priceCents, $subtotalCents);";
            itemCommand.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
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

        var outboxCommand = connection.CreateCommand();
        outboxCommand.Transaction = tx;
        outboxCommand.CommandText = @"
INSERT INTO outbox_events (id, type, payload_json, status, attempts, next_retry_at, last_error, created_at, sent_at)
VALUES ($id, $type, $payloadJson, $status, $attempts, $nextRetryAt, $lastError, $createdAt, $sentAt);";
        outboxCommand.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        outboxCommand.Parameters.AddWithValue("$type", "SaleCreated");
        outboxCommand.Parameters.AddWithValue("$payloadJson", outboxPayloadJson);
        outboxCommand.Parameters.AddWithValue("$status", "Pending");
        outboxCommand.Parameters.AddWithValue("$attempts", 0);
        outboxCommand.Parameters.AddWithValue("$nextRetryAt", DBNull.Value);
        outboxCommand.Parameters.AddWithValue("$lastError", DBNull.Value);
        outboxCommand.Parameters.AddWithValue("$createdAt", DateTimeOffset.UtcNow.ToString("O"));
        outboxCommand.Parameters.AddWithValue("$sentAt", DBNull.Value);
        await outboxCommand.ExecuteNonQueryAsync(cancellationToken);

        tx.Commit();
    }
}
