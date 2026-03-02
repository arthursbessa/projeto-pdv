using Microsoft.Extensions.DependencyInjection;
using Pdv.Application.Abstractions;
using Pdv.Application.Configuration;
using Pdv.Application.Services;
using Pdv.Infrastructure.Api;
using Pdv.Infrastructure.Persistence;
using Pdv.Infrastructure.Repositories;

namespace Pdv.Infrastructure.Setup;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPdvInfrastructure(this IServiceCollection services, PdvOptions options, string dbPath)
    {
        services.AddSingleton(options);
        services.AddSingleton(new SqliteConnectionFactory(dbPath));

        services.AddHttpClient<ICatalogApiClient, HttpCatalogApiClient>();
        services.AddHttpClient<ISalesApiClient, HttpSalesApiClient>();
        services.AddHttpClient<IAuthApiClient, HttpAuthApiClient>();

        services.AddSingleton<IProductCacheRepository, ProductCacheRepository>();
        services.AddSingleton<ISalesRepository, SalesRepository>();
        services.AddSingleton<ICashRegisterRepository, CashRegisterRepository>();
        services.AddSingleton<IOutboxRepository, OutboxRepository>();

        services.AddSingleton<DatabaseInitializer>();
        services.AddSingleton<SaleBuilderService>();
        services.AddSingleton<SyncService>();

        return services;
    }
}
