using Microsoft.Data.Sqlite;
using Pdv.Infrastructure.Persistence;

namespace Pdv.Infrastructure.Setup;

public sealed class DatabaseInitializer
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public DatabaseInitializer(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE IF NOT EXISTS products_cache (
    product_id TEXT PRIMARY KEY,
    barcode TEXT NOT NULL UNIQUE,
    description TEXT NOT NULL,
    price REAL NOT NULL,
    updated_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS sales (
    sale_id TEXT PRIMARY KEY,
    created_at TEXT NOT NULL,
    payment_method INTEGER NOT NULL,
    total REAL NOT NULL,
    received_amount REAL NULL
);

CREATE TABLE IF NOT EXISTS sale_items (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    sale_id TEXT NOT NULL,
    product_id TEXT NOT NULL,
    barcode TEXT NOT NULL,
    description TEXT NOT NULL,
    unit_price REAL NOT NULL,
    quantity INTEGER NOT NULL,
    subtotal REAL NOT NULL,
    FOREIGN KEY (sale_id) REFERENCES sales(sale_id)
);

CREATE TABLE IF NOT EXISTS outbox_events (
    id TEXT PRIMARY KEY,
    type TEXT NOT NULL,
    payload_json TEXT NOT NULL,
    status INTEGER NOT NULL,
    attempts INTEGER NOT NULL,
    next_retry_at TEXT NULL,
    last_error TEXT NULL,
    created_at TEXT NOT NULL,
    sent_at TEXT NULL
);

CREATE INDEX IF NOT EXISTS idx_products_cache_barcode ON products_cache (barcode);
CREATE INDEX IF NOT EXISTS idx_outbox_events_status_next_retry ON outbox_events (status, next_retry_at);
";

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
