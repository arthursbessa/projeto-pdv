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
