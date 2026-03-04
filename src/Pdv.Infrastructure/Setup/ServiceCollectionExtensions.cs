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
        services.AddHttpClient<IUsersApiClient, HttpUsersApiClient>();
        services.AddHttpClient<IStoreSettingsApiClient, HttpStoreSettingsApiClient>();
        services.AddHttpClient<ICashRegisterApiClient, HttpCashRegisterApiClient>();

        services.AddSingleton<IProductCacheRepository, ProductCacheRepository>();
        services.AddSingleton<ISalesRepository, SalesRepository>();
        services.AddSingleton<ICashRegisterRepository, CashRegisterRepository>();
        services.AddSingleton<IOutboxRepository, OutboxRepository>();
        services.AddSingleton<IUserRepository, UserRepository>();
        services.AddSingleton<IStoreSettingsRepository, StoreSettingsRepository>();

        services.AddSingleton<DatabaseInitializer>();
        services.AddSingleton<SaleBuilderService>();
        services.AddSingleton<SyncService>();
        services.AddSingleton<DataIntegrationService>();

        return services;
    }
}
