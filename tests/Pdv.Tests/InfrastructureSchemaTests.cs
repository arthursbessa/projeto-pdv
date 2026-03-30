using Microsoft.Data.Sqlite;
using Pdv.Application.Domain;
using Pdv.Infrastructure.Persistence;
using Pdv.Infrastructure.Repositories;
using Pdv.Infrastructure.Setup;
using Xunit;

namespace Pdv.Tests;

public sealed class InfrastructureSchemaTests
{
    [Fact]
    public async Task InitializeAsync_ShouldCreateCorePdvTablesAndSeedPaymentMethods()
    {
        var dbPath = CreateTempDbPath();
        var factory = new SqliteConnectionFactory(dbPath);
        var initializer = new DatabaseInitializer(factory);

        await initializer.InitializeAsync();

        await using var connection = factory.Create();
        await connection.OpenAsync();

        Assert.True(await TableExistsAsync(connection, "payment_methods"));
        Assert.True(await TableExistsAsync(connection, "sale_payments"));
        Assert.True(await TableExistsAsync(connection, "cash_register_sessions"));
        Assert.True(await TableExistsAsync(connection, "customers"));
        Assert.True(await TableExistsAsync(connection, "store_settings"));
        Assert.True(await TableExistsAsync(connection, "sale_refunds"));
        Assert.True(await TableExistsAsync(connection, "sale_refund_items"));

        var paymentCount = await ScalarIntAsync(connection, "SELECT COUNT(1) FROM payment_methods;");
        Assert.True(paymentCount >= 3);
    }

    [Fact]
    public async Task SaveSaleWithOutboxAsync_ShouldPersistSalePayment()
    {
        var dbPath = CreateTempDbPath();
        var factory = new SqliteConnectionFactory(dbPath);
        var initializer = new DatabaseInitializer(factory);
        var salesRepository = new SalesRepository(factory);
        var productRepository = new ProductCacheRepository(factory);

        await initializer.InitializeAsync();

        await productRepository.AddAsync(new ProductCacheItem
        {
            ProductId = Guid.NewGuid().ToString(),
            Barcode = "789000000001",
            Description = "Produto Teste",
            PriceCents = 1599,
            Active = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var product = (await productRepository.FindByBarcodeAsync("789000000001"))!;
        var sale = new Sale
        {
            PaymentMethod = PaymentMethod.Pix,
            ChangeAmountCents = 0,
            Items =
            [
                new SaleItem
                {
                    ProductId = product!.ProductId,
                    Barcode = product.Barcode,
                    Description = product.Description,
                    PriceCents = product.PriceCents
                }
            ]
        };

        await salesRepository.SaveSaleWithOutboxAsync(sale, "{}");

        await using var connection = factory.Create();
        await connection.OpenAsync();

        var salePayments = await ScalarIntAsync(connection, "SELECT COUNT(1) FROM sale_payments WHERE sale_id = $saleId;", ("$saleId", sale.SaleId.ToString()));
        Assert.Equal(1, salePayments);

        var paymentMethodId = await ScalarIntAsync(connection, "SELECT payment_method_id FROM sales WHERE id = $saleId;", ("$saleId", sale.SaleId.ToString()));
        Assert.Equal((int)PaymentMethod.Pix, paymentMethodId);
    }

    [Fact]
    public async Task SaveSaleWithOutboxAsync_ShouldPersistCashReceivedAndReturnSaleForReprint()
    {
        var dbPath = CreateTempDbPath();
        var factory = new SqliteConnectionFactory(dbPath);
        var initializer = new DatabaseInitializer(factory);
        var salesRepository = new SalesRepository(factory);
        var productRepository = new ProductCacheRepository(factory);

        await initializer.InitializeAsync();

        await productRepository.AddAsync(new ProductCacheItem
        {
            ProductId = Guid.NewGuid().ToString(),
            Barcode = "789000000002",
            Description = "Produto Dinheiro",
            PriceCents = 1200,
            Active = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var product = (await productRepository.FindByBarcodeAsync("789000000002"))!;
        var sale = new Sale
        {
            PaymentMethod = PaymentMethod.Cash,
            ReceivedAmountCents = 2000,
            ChangeAmountCents = 800,
            Items =
            [
                new SaleItem
                {
                    ProductId = product.ProductId,
                    Barcode = product.Barcode,
                    Description = product.Description,
                    PriceCents = product.PriceCents
                }
            ]
        };

        await salesRepository.SaveSaleWithOutboxAsync(sale, "{}");

        var history = await salesRepository.GetHistoryAsync(DateTime.Today);
        var loadedSale = await salesRepository.FindByIdAsync(sale.SaleId);

        Assert.Single(history);
        Assert.Equal(2000, history[0].ReceivedAmountCents);
        Assert.Equal(800, history[0].ChangeAmountCents);
        Assert.NotNull(loadedSale);
        Assert.Equal(2000, loadedSale!.ReceivedAmountCents);
        Assert.Equal(800, loadedSale.ChangeAmountCents);
        Assert.Single(loadedSale.Items);
    }

    [Fact]
    public async Task SaveRefundWithOutboxAsync_ShouldPersistRefundAndUpdateSaleStatus()
    {
        var dbPath = CreateTempDbPath();
        var factory = new SqliteConnectionFactory(dbPath);
        var initializer = new DatabaseInitializer(factory);
        var salesRepository = new SalesRepository(factory);
        var productRepository = new ProductCacheRepository(factory);

        await initializer.InitializeAsync();

        await productRepository.AddAsync(new ProductCacheItem
        {
            ProductId = Guid.NewGuid().ToString(),
            Barcode = "789000000003",
            Description = "Produto para Devolucao",
            PriceCents = 900,
            Active = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var product = (await productRepository.FindByBarcodeAsync("789000000003"))!;
        var sale = new Sale
        {
            PaymentMethod = PaymentMethod.Card,
            RemoteSaleId = "remote-sale-1",
            Items =
            [
                new SaleItem
                {
                    ProductId = product.ProductId,
                    Barcode = product.Barcode,
                    Description = product.Description,
                    PriceCents = product.PriceCents
                }
            ]
        };

        await salesRepository.SaveSaleWithOutboxAsync(sale, "{}");
        var loadedSale = await salesRepository.FindByIdAsync(sale.SaleId);

        Assert.NotNull(loadedSale);

        await salesRepository.SaveRefundWithOutboxAsync(
            sale.SaleId,
            "Produto com defeito",
            [
                new SaleRefundItem
                {
                    SaleItemId = loadedSale!.Items.First().SaleItemId!,
                    ProductId = loadedSale.Items.First().ProductId,
                    Description = loadedSale.Items.First().Description,
                    Quantity = 1
                }
            ],
            "{}",
            null);

        var refundedSale = await salesRepository.FindByIdAsync(sale.SaleId);

        await using var connection = factory.Create();
        await connection.OpenAsync();
        var refundCount = await ScalarIntAsync(connection, "SELECT COUNT(1) FROM sale_refunds WHERE sale_id = $saleId;", ("$saleId", sale.SaleId.ToString()));
        var refundOutboxCount = await ScalarIntAsync(connection, "SELECT COUNT(1) FROM outbox_events WHERE type = 'SaleRefundCreated' AND status = 'Pending';");

        Assert.NotNull(refundedSale);
        Assert.Equal("REFUNDED", refundedSale!.Status);
        Assert.Equal(1, refundedSale.Items.First().RefundedQuantity);
        Assert.Equal(1, refundCount);
        Assert.Equal(1, refundOutboxCount);
    }

    [Fact]
    public async Task SaveRefundAsync_ShouldPersistRefundWithoutCreatingOutboxEvent()
    {
        var dbPath = CreateTempDbPath();
        var factory = new SqliteConnectionFactory(dbPath);
        var initializer = new DatabaseInitializer(factory);
        var salesRepository = new SalesRepository(factory);
        var productRepository = new ProductCacheRepository(factory);

        await initializer.InitializeAsync();

        await productRepository.AddAsync(new ProductCacheItem
        {
            ProductId = Guid.NewGuid().ToString(),
            Barcode = "789000000004",
            Description = "Produto para Devolucao Direta",
            PriceCents = 1500,
            Active = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var product = (await productRepository.FindByBarcodeAsync("789000000004"))!;
        var sale = new Sale
        {
            PaymentMethod = PaymentMethod.Cash,
            Items =
            [
                new SaleItem
                {
                    ProductId = product.ProductId,
                    Barcode = product.Barcode,
                    Description = product.Description,
                    PriceCents = product.PriceCents
                }
            ]
        };

        await salesRepository.SaveSaleWithOutboxAsync(sale, "{}");
        var loadedSale = await salesRepository.FindByIdAsync(sale.SaleId);

        await salesRepository.SaveRefundAsync(
            sale.SaleId,
            "Cliente desistiu",
            [
                new SaleRefundItem
                {
                    SaleItemId = loadedSale!.Items.First().SaleItemId!,
                    ProductId = loadedSale.Items.First().ProductId,
                    Description = loadedSale.Items.First().Description,
                    Quantity = 1
                }
            ],
            null);

        await using var connection = factory.Create();
        await connection.OpenAsync();
        var refundCount = await ScalarIntAsync(connection, "SELECT COUNT(1) FROM sale_refunds WHERE sale_id = $saleId;", ("$saleId", sale.SaleId.ToString()));
        var refundOutboxCount = await ScalarIntAsync(connection, "SELECT COUNT(1) FROM outbox_events WHERE type = 'SaleRefundCreated';");

        Assert.Equal(1, refundCount);
        Assert.Equal(0, refundOutboxCount);
    }


    [Fact]
    public async Task CashStatusSnapshot_ShouldReturnCurrentBalanceAndTransactions()
    {
        var dbPath = CreateTempDbPath();
        var factory = new SqliteConnectionFactory(dbPath);
        var initializer = new DatabaseInitializer(factory);
        var cashRegisterRepository = new CashRegisterRepository(factory);

        await initializer.InitializeAsync();

        var now = DateTimeOffset.UtcNow;
        var session = await cashRegisterRepository.OpenAsync(10000, "user-1", now);

        await using (var connection = factory.Create())
        {
            await connection.OpenAsync();

            var saleCommand = connection.CreateCommand();
            saleCommand.CommandText = @"INSERT INTO sales (id, created_at, total_cents, payment_method, payment_method_id, status, cash_register_session_id)
VALUES ($id, $createdAt, $totalCents, $paymentMethod, $paymentMethodId, 'COMPLETED', $sessionId);";
            saleCommand.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
            saleCommand.Parameters.AddWithValue("$createdAt", now.ToString("O"));
            saleCommand.Parameters.AddWithValue("$totalCents", 2500);
            saleCommand.Parameters.AddWithValue("$paymentMethod", "Cash");
            saleCommand.Parameters.AddWithValue("$paymentMethodId", (int)PaymentMethod.Cash);
            saleCommand.Parameters.AddWithValue("$sessionId", session.Id);
            await saleCommand.ExecuteNonQueryAsync();
        }

        await cashRegisterRepository.RegisterWithdrawalAsync(session.Id, 1000, "Troco", "user-1", now);

        var snapshot = await cashRegisterRepository.GetCashStatusSnapshotAsync(now);

        Assert.True(snapshot.IsOpen);
        Assert.Equal(session.Id, snapshot.SessionId);
        Assert.Equal(10000, snapshot.OpeningAmountCents);
        Assert.Equal(2500, snapshot.SalesTotalCents);
        Assert.Equal(1000, snapshot.WithdrawalsTotalCents);
        Assert.Equal(11500, snapshot.CurrentBalanceCents);
        Assert.Equal(2, snapshot.Transactions.Count);

        await using var verifyConnection = factory.Create();
        await verifyConnection.OpenAsync();
        var pendingWithdrawals = await ScalarIntAsync(verifyConnection, "SELECT COUNT(1) FROM outbox_events WHERE type = 'CashWithdrawalCreated' AND status = 'Pending';");

        Assert.Equal(1, pendingWithdrawals);
    }


    [Fact]
    public async Task SaveRemoteSessionIdAsync_ShouldPersistRemoteSessionId()
    {
        var dbPath = CreateTempDbPath();
        var factory = new SqliteConnectionFactory(dbPath);
        var initializer = new DatabaseInitializer(factory);
        var cashRegisterRepository = new CashRegisterRepository(factory);

        await initializer.InitializeAsync();

        var session = await cashRegisterRepository.OpenAsync(1000, "user-1", DateTimeOffset.UtcNow);
        await cashRegisterRepository.SaveRemoteSessionIdAsync(session.Id, "remote-session-123");

        var remoteSessionId = await cashRegisterRepository.GetRemoteSessionIdAsync(session.Id);
        var openSession = await cashRegisterRepository.GetOpenSessionAsync();

        Assert.Equal("remote-session-123", remoteSessionId);
        Assert.Equal("remote-session-123", openSession?.RemoteSessionId);
    }

    [Fact]
    public async Task CloseAsync_ShouldCloseOpenSession()
    {
        var dbPath = CreateTempDbPath();
        var factory = new SqliteConnectionFactory(dbPath);
        var initializer = new DatabaseInitializer(factory);
        var cashRegisterRepository = new CashRegisterRepository(factory);

        await initializer.InitializeAsync();

        var now = DateTimeOffset.UtcNow;
        var session = await cashRegisterRepository.OpenAsync(5000, "user-1", now);

        await cashRegisterRepository.CloseAsync(session.Id, "user-1", now.AddMinutes(1));

        var openSession = await cashRegisterRepository.GetOpenSessionAsync();
        var closedSession = await cashRegisterRepository.GetLastClosedSessionAsync();

        Assert.Null(openSession);
        Assert.NotNull(closedSession);
        Assert.Equal("CLOSED", closedSession!.Status);
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string table)
    {
        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = $table;";
        command.Parameters.AddWithValue("$table", table);

        var value = await command.ExecuteScalarAsync();
        return Convert.ToInt32(value) == 1;
    }

    private static async Task<int> ScalarIntAsync(SqliteConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;

        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        }

        var value = await command.ExecuteScalarAsync();
        return Convert.ToInt32(value);
    }

    private static string CreateTempDbPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pdv-tests-{Guid.NewGuid():N}.db");
        return path;
    }
}
