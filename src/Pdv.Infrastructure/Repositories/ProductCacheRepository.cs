using Microsoft.Data.Sqlite;
using Pdv.Application.Abstractions;
using Pdv.Application.Domain;
using Pdv.Infrastructure.Persistence;

namespace Pdv.Infrastructure.Repositories;

public sealed class ProductCacheRepository : IProductCacheRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public ProductCacheRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<ProductCacheItem?> FindByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
SELECT product_id, barcode, description, price, updated_at
FROM products_cache
WHERE barcode = $barcode
LIMIT 1;";
        command.Parameters.AddWithValue("$barcode", barcode);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ProductCacheItem
        {
            ProductId = reader.GetString(0),
            Barcode = reader.GetString(1),
            Description = reader.GetString(2),
            Price = reader.GetDecimal(3),
            UpdatedAt = DateTimeOffset.Parse(reader.GetString(4))
        };
    }

    public async Task ReplaceCatalogAsync(IEnumerable<ProductCacheItem> products, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var delete = connection.CreateCommand();
        delete.Transaction = transaction;
        delete.CommandText = "DELETE FROM products_cache;";
        await delete.ExecuteNonQueryAsync(cancellationToken);

        foreach (var product in products)
        {
            var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = @"
INSERT INTO products_cache (product_id, barcode, description, price, updated_at)
VALUES ($productId, $barcode, $description, $price, $updatedAt);";
            insert.Parameters.AddWithValue("$productId", product.ProductId);
            insert.Parameters.AddWithValue("$barcode", product.Barcode);
            insert.Parameters.AddWithValue("$description", product.Description);
            insert.Parameters.AddWithValue("$price", product.Price);
            insert.Parameters.AddWithValue("$updatedAt", product.UpdatedAt.ToString("O"));
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }
}
