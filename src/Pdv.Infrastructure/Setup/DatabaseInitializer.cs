using Pdv.Application.Abstractions;
using Pdv.Infrastructure.Persistence;

namespace Pdv.Infrastructure.Setup;

public sealed class DatabaseInitializer
{
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly IProductCacheRepository _productRepository;

    public DatabaseInitializer(SqliteConnectionFactory connectionFactory, IProductCacheRepository productRepository)
    {
        _connectionFactory = connectionFactory;
        _productRepository = productRepository;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS products (
    id TEXT PRIMARY KEY,
    barcode TEXT NOT NULL UNIQUE,
    description TEXT NOT NULL,
    price_cents INTEGER NOT NULL,
    active INTEGER NOT NULL DEFAULT 1,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS sales (
    id TEXT PRIMARY KEY,
    created_at TEXT NOT NULL,
    total_cents INTEGER NOT NULL,
    payment_method TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'COMPLETED'
);

CREATE TABLE IF NOT EXISTS sale_items (
    id TEXT PRIMARY KEY,
    sale_id TEXT NOT NULL,
    product_id TEXT NOT NULL,
    barcode TEXT NOT NULL,
    description TEXT NOT NULL,
    quantity INTEGER NOT NULL,
    price_cents INTEGER NOT NULL,
    subtotal_cents INTEGER NOT NULL,
    FOREIGN KEY (sale_id) REFERENCES sales(id),
    FOREIGN KEY (product_id) REFERENCES products(id)
);

CREATE TABLE IF NOT EXISTS outbox_events (
    id TEXT PRIMARY KEY,
    type TEXT NOT NULL,
    payload_json TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'Pending',
    attempts INTEGER NOT NULL DEFAULT 0,
    next_retry_at TEXT NULL,
    last_error TEXT NULL,
    created_at TEXT NOT NULL,
    sent_at TEXT NULL
);

CREATE INDEX IF NOT EXISTS idx_products_active ON products (active);
CREATE INDEX IF NOT EXISTS idx_sale_items_sale_id ON sale_items (sale_id);
CREATE INDEX IF NOT EXISTS idx_sale_items_barcode ON sale_items (barcode);
CREATE INDEX IF NOT EXISTS idx_outbox_status ON outbox_events (status);
CREATE INDEX IF NOT EXISTS idx_outbox_next_retry_at ON outbox_events (next_retry_at);
";

        await command.ExecuteNonQueryAsync(cancellationToken);
        await _productRepository.SeedIfEmptyAsync(ProductSeedData.Create(), cancellationToken);
    }
}
