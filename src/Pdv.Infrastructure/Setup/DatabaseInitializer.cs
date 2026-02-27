using Microsoft.Data.Sqlite;
using Pdv.Application.Abstractions;
using Pdv.Application.Domain;
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

        await CreateBaseSchemaAsync(connection, cancellationToken);
        await EnsureSchemaEvolutionAsync(connection, cancellationToken);
        await SeedPaymentMethodsAsync(connection, cancellationToken);
        await _productRepository.SeedIfEmptyAsync(ProductSeedData.Create(), cancellationToken);
    }

    private static async Task CreateBaseSchemaAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
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

CREATE TABLE IF NOT EXISTS payment_methods (
    id INTEGER PRIMARY KEY,
    code TEXT NOT NULL UNIQUE,
    name TEXT NOT NULL,
    allows_change INTEGER NOT NULL DEFAULT 0,
    active INTEGER NOT NULL DEFAULT 1,
    sort_order INTEGER NOT NULL DEFAULT 0,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS cash_register_sessions (
    id TEXT PRIMARY KEY,
    opened_at TEXT NOT NULL,
    closed_at TEXT NULL,
    opening_amount_cents INTEGER NOT NULL DEFAULT 0,
    closing_amount_cents INTEGER NULL,
    status TEXT NOT NULL DEFAULT 'OPEN'
);

CREATE TABLE IF NOT EXISTS customers (
    id TEXT PRIMARY KEY,
    document_number TEXT NULL,
    name TEXT NOT NULL,
    phone TEXT NULL,
    email TEXT NULL,
    active INTEGER NOT NULL DEFAULT 1,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS sales (
    id TEXT PRIMARY KEY,
    created_at TEXT NOT NULL,
    total_cents INTEGER NOT NULL,
    payment_method TEXT NOT NULL,
    payment_method_id INTEGER NULL,
    status TEXT NOT NULL DEFAULT 'COMPLETED',
    customer_id TEXT NULL,
    cash_register_session_id TEXT NULL,
    discount_cents INTEGER NOT NULL DEFAULT 0,
    surcharge_cents INTEGER NOT NULL DEFAULT 0,
    notes TEXT NULL,
    FOREIGN KEY (payment_method_id) REFERENCES payment_methods(id),
    FOREIGN KEY (customer_id) REFERENCES customers(id),
    FOREIGN KEY (cash_register_session_id) REFERENCES cash_register_sessions(id)
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
    discount_cents INTEGER NOT NULL DEFAULT 0,
    FOREIGN KEY (sale_id) REFERENCES sales(id),
    FOREIGN KEY (product_id) REFERENCES products(id)
);

CREATE TABLE IF NOT EXISTS sale_payments (
    id TEXT PRIMARY KEY,
    sale_id TEXT NOT NULL,
    payment_method_id INTEGER NOT NULL,
    amount_cents INTEGER NOT NULL,
    paid_at TEXT NOT NULL,
    authorization_code TEXT NULL,
    FOREIGN KEY (sale_id) REFERENCES sales(id),
    FOREIGN KEY (payment_method_id) REFERENCES payment_methods(id)
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
CREATE INDEX IF NOT EXISTS idx_sales_created_at ON sales (created_at);
CREATE INDEX IF NOT EXISTS idx_sales_status ON sales (status);
CREATE INDEX IF NOT EXISTS idx_sale_items_sale_id ON sale_items (sale_id);
CREATE INDEX IF NOT EXISTS idx_sale_items_barcode ON sale_items (barcode);
CREATE INDEX IF NOT EXISTS idx_sale_payments_sale_id ON sale_payments (sale_id);
CREATE INDEX IF NOT EXISTS idx_customers_document_number ON customers (document_number);
CREATE INDEX IF NOT EXISTS idx_outbox_status ON outbox_events (status);
CREATE INDEX IF NOT EXISTS idx_outbox_next_retry_at ON outbox_events (next_retry_at);
";

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureSchemaEvolutionAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await EnsureColumnAsync(connection, "sales", "payment_method_id", "INTEGER NULL", cancellationToken);
        await EnsureColumnAsync(connection, "sales", "customer_id", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "sales", "cash_register_session_id", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "sales", "discount_cents", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await EnsureColumnAsync(connection, "sales", "surcharge_cents", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await EnsureColumnAsync(connection, "sales", "notes", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "sale_items", "discount_cents", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
    }

    private static async Task EnsureColumnAsync(SqliteConnection connection, string table, string column, string definition, CancellationToken cancellationToken)
    {
        var pragma = connection.CreateCommand();
        pragma.CommandText = $"PRAGMA table_info({table});";

        var exists = false;
        await using (var reader = await pragma.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                {
                    exists = true;
                    break;
                }
            }
        }

        if (exists)
        {
            return;
        }

        var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
        await alter.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task SeedPaymentMethodsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow.ToString("O");

        foreach (var method in Enum.GetValues<PaymentMethod>())
        {
            var (code, name, allowsChange, sortOrder) = method switch
            {
                PaymentMethod.Cash => ("CASH", "Dinheiro", 1, 1),
                PaymentMethod.Card => ("CARD", "Cartão", 0, 2),
                PaymentMethod.Pix => ("PIX", "PIX", 0, 3),
                _ => (method.ToString().ToUpperInvariant(), method.ToString(), 0, 99)
            };

            var command = connection.CreateCommand();
            command.CommandText = @"
INSERT INTO payment_methods (id, code, name, allows_change, active, sort_order, created_at, updated_at)
VALUES ($id, $code, $name, $allowsChange, 1, $sortOrder, $createdAt, $updatedAt)
ON CONFLICT(id) DO UPDATE SET
    code = excluded.code,
    name = excluded.name,
    allows_change = excluded.allows_change,
    active = 1,
    sort_order = excluded.sort_order,
    updated_at = excluded.updated_at;";

            command.Parameters.AddWithValue("$id", (int)method);
            command.Parameters.AddWithValue("$code", code);
            command.Parameters.AddWithValue("$name", name);
            command.Parameters.AddWithValue("$allowsChange", allowsChange);
            command.Parameters.AddWithValue("$sortOrder", sortOrder);
            command.Parameters.AddWithValue("$createdAt", now);
            command.Parameters.AddWithValue("$updatedAt", now);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
