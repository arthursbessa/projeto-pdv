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
SELECT id, sku, barcode, description, price_cents, active, created_at, updated_at
FROM products
WHERE barcode = $barcode
LIMIT 1;";
        command.Parameters.AddWithValue("$barcode", barcode);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadProduct(reader) : null;
    }

    public async Task<IReadOnlyList<ProductCacheItem>> SearchAsync(string? query, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        var like = $"%{query?.Trim() ?? string.Empty}%";
        command.CommandText = @"
SELECT id, sku, barcode, description, price_cents, active, created_at, updated_at
FROM products
WHERE $query = ''
   OR barcode LIKE $like
   OR description LIKE $like
   OR COALESCE(sku, '') LIKE $like
ORDER BY active DESC, description ASC
LIMIT 500;";
        command.Parameters.AddWithValue("$query", query?.Trim() ?? string.Empty);
        command.Parameters.AddWithValue("$like", like);

        var result = new List<ProductCacheItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(ReadProduct(reader));
        }

        return result;
    }

    public async Task<ProductCacheItem?> FindByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
SELECT id, sku, barcode, description, price_cents, active, created_at, updated_at
FROM products
WHERE id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadProduct(reader) : null;
    }

    public async Task AddAsync(ProductCacheItem product, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
INSERT INTO products (id, sku, barcode, description, price_cents, active, created_at, updated_at)
VALUES ($id, $sku, $barcode, $description, $priceCents, $active, $createdAt, $updatedAt);";
        BindProduct(cmd, product);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(ProductCacheItem product, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
UPDATE products
SET sku = $sku,
    barcode = $barcode,
    description = $description,
    price_cents = $priceCents,
    active = $active,
    updated_at = $updatedAt
WHERE id = $id;";
        BindProduct(cmd, product);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ToggleActiveAsync(string id, bool active, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
UPDATE products
SET active = $active, updated_at = $updatedAt
WHERE id = $id;";
        cmd.Parameters.AddWithValue("$active", active ? 1 : 0);
        cmd.Parameters.AddWithValue("$updatedAt", DateTimeOffset.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM products;";
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken));
    }

    public async Task SeedIfEmptyAsync(IEnumerable<ProductCacheItem> products, CancellationToken cancellationToken = default)
    {
        if (await CountAsync(cancellationToken) > 0)
        {
            return;
        }

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        using var tx = connection.BeginTransaction();

        foreach (var product in products)
        {
            var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
INSERT INTO products (id, sku, barcode, description, price_cents, active, created_at, updated_at)
VALUES ($id, $sku, $barcode, $description, $priceCents, $active, $createdAt, $updatedAt);";
            BindProduct(cmd, product);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        tx.Commit();
    }

    private static ProductCacheItem ReadProduct(Microsoft.Data.Sqlite.SqliteDataReader reader) => new()
    {
        ProductId = reader.GetString(0),
        Sku = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
        Barcode = reader.GetString(2),
        Description = reader.GetString(3),
        PriceCents = reader.GetInt32(4),
        Active = reader.GetInt32(5) == 1,
        CreatedAt = DateTimeOffset.Parse(reader.GetString(6)),
        UpdatedAt = DateTimeOffset.Parse(reader.GetString(7))
    };

    private static void BindProduct(Microsoft.Data.Sqlite.SqliteCommand cmd, ProductCacheItem product)
    {
        cmd.Parameters.AddWithValue("$id", product.ProductId);
        cmd.Parameters.AddWithValue("$sku", string.IsNullOrWhiteSpace(product.Sku) ? DBNull.Value : product.Sku);
        cmd.Parameters.AddWithValue("$barcode", product.Barcode);
        cmd.Parameters.AddWithValue("$description", product.Description);
        cmd.Parameters.AddWithValue("$priceCents", product.PriceCents);
        cmd.Parameters.AddWithValue("$active", product.Active ? 1 : 0);
        cmd.Parameters.AddWithValue("$createdAt", product.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$updatedAt", product.UpdatedAt.ToString("O"));
    }
}
