using System.Text.Json;
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

    public async Task SaveSaleWithOutboxAsync(Sale sale, string outboxPayloadJson, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        using var tx = connection.BeginTransaction();

        var saleCommand = connection.CreateCommand();
        saleCommand.Transaction = tx;
        saleCommand.CommandText = @"
INSERT INTO sales (sale_id, created_at, payment_method, total, received_amount)
VALUES ($saleId, $createdAt, $paymentMethod, $total, $receivedAmount);";
        saleCommand.Parameters.AddWithValue("$saleId", sale.SaleId.ToString());
        saleCommand.Parameters.AddWithValue("$createdAt", sale.CreatedAt.ToString("O"));
        saleCommand.Parameters.AddWithValue("$paymentMethod", (int)sale.PaymentMethod);
        saleCommand.Parameters.AddWithValue("$total", sale.Total);
        saleCommand.Parameters.AddWithValue("$receivedAmount", (object?)sale.ReceivedAmount ?? DBNull.Value);
        await saleCommand.ExecuteNonQueryAsync(cancellationToken);

        foreach (var item in sale.Items)
        {
            var itemCommand = connection.CreateCommand();
            itemCommand.Transaction = tx;
            itemCommand.CommandText = @"
INSERT INTO sale_items (sale_id, product_id, barcode, description, unit_price, quantity, subtotal)
VALUES ($saleId, $productId, $barcode, $description, $unitPrice, $quantity, $subtotal);";
            itemCommand.Parameters.AddWithValue("$saleId", sale.SaleId.ToString());
            itemCommand.Parameters.AddWithValue("$productId", item.ProductId);
            itemCommand.Parameters.AddWithValue("$barcode", item.Barcode);
            itemCommand.Parameters.AddWithValue("$description", item.Description);
            itemCommand.Parameters.AddWithValue("$unitPrice", item.UnitPrice);
            itemCommand.Parameters.AddWithValue("$quantity", item.Quantity);
            itemCommand.Parameters.AddWithValue("$subtotal", item.Subtotal);
            await itemCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        var outboxCommand = connection.CreateCommand();
        outboxCommand.Transaction = tx;
        outboxCommand.CommandText = @"
INSERT INTO outbox_events (id, type, payload_json, status, attempts, next_retry_at, last_error, created_at, sent_at)
VALUES ($id, $type, $payloadJson, $status, $attempts, $nextRetryAt, $lastError, $createdAt, $sentAt);";

        outboxCommand.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        outboxCommand.Parameters.AddWithValue("$type", "SaleCreated");
        outboxCommand.Parameters.AddWithValue("$payloadJson", outboxPayloadJson);
        outboxCommand.Parameters.AddWithValue("$status", (int)OutboxStatus.Pending);
        outboxCommand.Parameters.AddWithValue("$attempts", 0);
        outboxCommand.Parameters.AddWithValue("$nextRetryAt", DBNull.Value);
        outboxCommand.Parameters.AddWithValue("$lastError", DBNull.Value);
        outboxCommand.Parameters.AddWithValue("$createdAt", DateTimeOffset.UtcNow.ToString("O"));
        outboxCommand.Parameters.AddWithValue("$sentAt", DBNull.Value);

        await outboxCommand.ExecuteNonQueryAsync(cancellationToken);
        tx.Commit();
    }
}
