using Microsoft.Data.Sqlite;
using Pdv.Application.Domain;
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

        await CreateBaseSchemaAsync(connection, cancellationToken);
        await EnsureSchemaEvolutionAsync(connection, cancellationToken);
        await SeedPaymentMethodsAsync(connection, cancellationToken);
    }

    private static async Task CreateBaseSchemaAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = @"
PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS products (
    id TEXT PRIMARY KEY,
    sku TEXT NULL,
    barcode TEXT NOT NULL UNIQUE,
    description TEXT NOT NULL,
    category_id TEXT NULL,
    supplier_id TEXT NULL,
    cost_price_cents INTEGER NOT NULL DEFAULT 0,
    ncm TEXT NULL,
    cfop TEXT NULL,
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

CREATE TABLE IF NOT EXISTS users (
    id TEXT PRIMARY KEY,
    username TEXT NOT NULL UNIQUE,
    full_name TEXT NOT NULL,
    password_hash TEXT NOT NULL,
    active INTEGER NOT NULL DEFAULT 1,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS store_settings (
    id INTEGER PRIMARY KEY CHECK (id = 1),
    store_name TEXT NOT NULL,
    terminal_name TEXT NULL,
    cnpj TEXT NOT NULL,
    address TEXT NOT NULL,
    timezone TEXT NOT NULL,
    currency TEXT NOT NULL,
    logo_url TEXT NOT NULL,
    logo_local_path TEXT NOT NULL,
    updated_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS pdv_settings (
    id INTEGER PRIMARY KEY CHECK (id = 1),
    product_text_case TEXT NOT NULL DEFAULT 'Original',
    ask_printer_before_print INTEGER NOT NULL DEFAULT 1,
    preferred_printer_name TEXT NULL,
    shortcut_add_item TEXT NOT NULL DEFAULT 'Enter',
    shortcut_finalize_sale TEXT NOT NULL DEFAULT 'F2',
    shortcut_search_product TEXT NOT NULL DEFAULT 'F3',
    shortcut_remove_item TEXT NOT NULL DEFAULT 'F4',
    shortcut_cancel_sale TEXT NOT NULL DEFAULT 'Space',
    updated_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS cash_register_sessions (
    id TEXT PRIMARY KEY,
    opened_at TEXT NOT NULL,
    closed_at TEXT NULL,
    opening_amount_cents INTEGER NOT NULL DEFAULT 0,
    closing_amount_cents INTEGER NULL,
    status TEXT NOT NULL DEFAULT 'OPEN',
    business_date TEXT NULL,
    opened_by_user_id TEXT NULL,
    closed_by_user_id TEXT NULL,
    remote_session_id TEXT NULL
);

CREATE TABLE IF NOT EXISTS cash_withdrawals (
    id TEXT PRIMARY KEY,
    cash_register_session_id TEXT NOT NULL,
    amount_cents INTEGER NOT NULL,
    reason TEXT NULL,
    created_at TEXT NOT NULL,
    created_by_user_id TEXT NULL,
    FOREIGN KEY (cash_register_session_id) REFERENCES cash_register_sessions(id),
    FOREIGN KEY (created_by_user_id) REFERENCES users(id)
);

CREATE TABLE IF NOT EXISTS customers (
    id TEXT PRIMARY KEY,
    document_number TEXT NULL,
    name TEXT NOT NULL,
    phone TEXT NULL,
    email TEXT NULL,
    address TEXT NULL,
    notes TEXT NULL,
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
    received_amount_cents INTEGER NULL,
    change_amount_cents INTEGER NOT NULL DEFAULT 0,
    status TEXT NOT NULL DEFAULT 'COMPLETED',
    customer_id TEXT NULL,
    operator_id TEXT NULL,
    cash_register_session_id TEXT NULL,
    remote_sale_id TEXT NULL,
    sale_number INTEGER NULL,
    discount_cents INTEGER NOT NULL DEFAULT 0,
    discount_percent REAL NOT NULL DEFAULT 0,
    surcharge_cents INTEGER NOT NULL DEFAULT 0,
    notes TEXT NULL,
    receipt_requested INTEGER NOT NULL DEFAULT 0,
    receipt_tax_id TEXT NULL,
    FOREIGN KEY (payment_method_id) REFERENCES payment_methods(id),
    FOREIGN KEY (customer_id) REFERENCES customers(id),
    FOREIGN KEY (operator_id) REFERENCES users(id),
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

CREATE TABLE IF NOT EXISTS sale_refunds (
    id TEXT PRIMARY KEY,
    sale_id TEXT NOT NULL,
    reason TEXT NOT NULL,
    created_at TEXT NOT NULL,
    operator_id TEXT NULL,
    status TEXT NOT NULL DEFAULT 'PENDING',
    synced_at TEXT NULL,
    FOREIGN KEY (sale_id) REFERENCES sales(id),
    FOREIGN KEY (operator_id) REFERENCES users(id)
);

CREATE TABLE IF NOT EXISTS sale_refund_items (
    id TEXT PRIMARY KEY,
    refund_id TEXT NOT NULL,
    sale_item_id TEXT NOT NULL,
    product_id TEXT NOT NULL,
    quantity INTEGER NOT NULL,
    FOREIGN KEY (refund_id) REFERENCES sale_refunds(id),
    FOREIGN KEY (sale_item_id) REFERENCES sale_items(id)
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
CREATE INDEX IF NOT EXISTS idx_users_username ON users (username);
CREATE INDEX IF NOT EXISTS idx_cash_withdrawals_session_id ON cash_withdrawals (cash_register_session_id);
CREATE INDEX IF NOT EXISTS idx_sales_created_at ON sales (created_at);
CREATE INDEX IF NOT EXISTS idx_sales_status ON sales (status);
CREATE INDEX IF NOT EXISTS idx_sale_items_sale_id ON sale_items (sale_id);
CREATE INDEX IF NOT EXISTS idx_sale_items_barcode ON sale_items (barcode);
CREATE INDEX IF NOT EXISTS idx_sale_payments_sale_id ON sale_payments (sale_id);
CREATE INDEX IF NOT EXISTS idx_sale_refunds_sale_id ON sale_refunds (sale_id);
CREATE INDEX IF NOT EXISTS idx_sale_refunds_status ON sale_refunds (status);
CREATE INDEX IF NOT EXISTS idx_sale_refund_items_refund_id ON sale_refund_items (refund_id);
CREATE INDEX IF NOT EXISTS idx_sale_refund_items_sale_item_id ON sale_refund_items (sale_item_id);
CREATE INDEX IF NOT EXISTS idx_customers_document_number ON customers (document_number);
CREATE INDEX IF NOT EXISTS idx_outbox_status ON outbox_events (status);
CREATE INDEX IF NOT EXISTS idx_outbox_next_retry_at ON outbox_events (next_retry_at);
";

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureSchemaEvolutionAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var createStoreSettings = connection.CreateCommand();
        createStoreSettings.CommandText = @"
CREATE TABLE IF NOT EXISTS store_settings (
    id INTEGER PRIMARY KEY CHECK (id = 1),
    store_name TEXT NOT NULL,
    cnpj TEXT NOT NULL,
    address TEXT NOT NULL,
    timezone TEXT NOT NULL,
    currency TEXT NOT NULL,
    logo_url TEXT NOT NULL,
    logo_local_path TEXT NOT NULL,
    updated_at TEXT NOT NULL
);";
        await createStoreSettings.ExecuteNonQueryAsync(cancellationToken);

        var createPdvSettings = connection.CreateCommand();
        createPdvSettings.CommandText = @"
CREATE TABLE IF NOT EXISTS pdv_settings (
    id INTEGER PRIMARY KEY CHECK (id = 1),
    product_text_case TEXT NOT NULL DEFAULT 'Original',
    ask_printer_before_print INTEGER NOT NULL DEFAULT 1,
    preferred_printer_name TEXT NULL,
    shortcut_add_item TEXT NOT NULL DEFAULT 'Enter',
    shortcut_finalize_sale TEXT NOT NULL DEFAULT 'F2',
    shortcut_search_product TEXT NOT NULL DEFAULT 'F3',
    shortcut_remove_item TEXT NOT NULL DEFAULT 'F4',
    shortcut_cancel_sale TEXT NOT NULL DEFAULT 'Space',
    updated_at TEXT NOT NULL
);";
        await createPdvSettings.ExecuteNonQueryAsync(cancellationToken);

        await EnsureColumnAsync(connection, "sales", "payment_method_id", "INTEGER NULL", cancellationToken);
        await EnsureColumnAsync(connection, "sales", "received_amount_cents", "INTEGER NULL", cancellationToken);
        await EnsureColumnAsync(connection, "sales", "change_amount_cents", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await EnsureColumnAsync(connection, "sales", "customer_id", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "sales", "operator_id", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "sales", "cash_register_session_id", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "sales", "remote_sale_id", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "sales", "sale_number", "INTEGER NULL", cancellationToken);
        await EnsureColumnAsync(connection, "sales", "discount_cents", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await EnsureColumnAsync(connection, "sales", "discount_percent", "REAL NOT NULL DEFAULT 0", cancellationToken);
        await EnsureColumnAsync(connection, "sales", "surcharge_cents", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await EnsureColumnAsync(connection, "sales", "notes", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "sales", "receipt_requested", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await EnsureColumnAsync(connection, "sales", "receipt_tax_id", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "products", "sku", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "products", "category_id", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "products", "supplier_id", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "products", "cost_price_cents", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await EnsureColumnAsync(connection, "products", "ncm", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "products", "cfop", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "store_settings", "terminal_name", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "pdv_settings", "product_text_case", "TEXT NOT NULL DEFAULT 'Original'", cancellationToken);
        await EnsureColumnAsync(connection, "customers", "address", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "customers", "notes", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "sale_items", "discount_cents", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await EnsureColumnAsync(connection, "cash_register_sessions", "business_date", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "cash_register_sessions", "opened_by_user_id", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "cash_register_sessions", "closed_by_user_id", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "cash_register_sessions", "remote_session_id", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "cash_withdrawals", "reason", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "cash_withdrawals", "created_by_user_id", "TEXT NULL", cancellationToken);

        var createSaleRefunds = connection.CreateCommand();
        createSaleRefunds.CommandText = @"
CREATE TABLE IF NOT EXISTS sale_refunds (
    id TEXT PRIMARY KEY,
    sale_id TEXT NOT NULL,
    reason TEXT NOT NULL,
    created_at TEXT NOT NULL,
    operator_id TEXT NULL,
    status TEXT NOT NULL DEFAULT 'PENDING',
    synced_at TEXT NULL,
    FOREIGN KEY (sale_id) REFERENCES sales(id),
    FOREIGN KEY (operator_id) REFERENCES users(id)
);";
        await createSaleRefunds.ExecuteNonQueryAsync(cancellationToken);

        var createSaleRefundItems = connection.CreateCommand();
        createSaleRefundItems.CommandText = @"
CREATE TABLE IF NOT EXISTS sale_refund_items (
    id TEXT PRIMARY KEY,
    refund_id TEXT NOT NULL,
    sale_item_id TEXT NOT NULL,
    product_id TEXT NOT NULL,
    quantity INTEGER NOT NULL,
    FOREIGN KEY (refund_id) REFERENCES sale_refunds(id),
    FOREIGN KEY (sale_item_id) REFERENCES sale_items(id)
);";
        await createSaleRefundItems.ExecuteNonQueryAsync(cancellationToken);

        var createRefundIndexes = connection.CreateCommand();
        createRefundIndexes.CommandText = @"
CREATE INDEX IF NOT EXISTS idx_sale_refunds_sale_id ON sale_refunds (sale_id);
CREATE INDEX IF NOT EXISTS idx_sale_refunds_status ON sale_refunds (status);
CREATE INDEX IF NOT EXISTS idx_sale_refund_items_refund_id ON sale_refund_items (refund_id);
CREATE INDEX IF NOT EXISTS idx_sale_refund_items_sale_item_id ON sale_refund_items (sale_item_id);";
        await createRefundIndexes.ExecuteNonQueryAsync(cancellationToken);
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

        foreach (var method in Enum.GetValues<PaymentMethod>().Distinct())
        {
            var (code, name, allowsChange, sortOrder) = method switch
            {
                PaymentMethod.Cash => ("CASH", "Dinheiro", 1, 1),
                PaymentMethod.CreditCard => ("CREDIT_CARD", "Cartao de credito", 0, 2),
                PaymentMethod.DebitCard => ("DEBIT_CARD", "Cartao de debito", 0, 3),
                PaymentMethod.Pix => ("PIX", "PIX", 0, 4),
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

        var settingsCommand = connection.CreateCommand();
        settingsCommand.CommandText = @"
INSERT INTO pdv_settings (
    id,
    product_text_case,
    ask_printer_before_print,
    preferred_printer_name,
    shortcut_add_item,
    shortcut_finalize_sale,
    shortcut_search_product,
    shortcut_remove_item,
    shortcut_cancel_sale,
    updated_at)
VALUES (1, 'Original', 1, NULL, 'Enter', 'F2', 'F3', 'F4', 'Space', $updatedAt)
ON CONFLICT(id) DO NOTHING;";
        settingsCommand.Parameters.AddWithValue("$updatedAt", now);
        await settingsCommand.ExecuteNonQueryAsync(cancellationToken);
    }
}
