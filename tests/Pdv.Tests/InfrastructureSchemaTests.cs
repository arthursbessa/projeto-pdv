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
