using Pdv.Application.Abstractions;
using Pdv.Application.Domain;
using Pdv.Infrastructure.Persistence;

namespace Pdv.Infrastructure.Repositories;

public sealed class StoreSettingsRepository : IStoreSettingsRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public StoreSettingsRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<StoreSettings?> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"SELECT store_name, cnpj, address, timezone, currency, logo_url, logo_local_path, updated_at FROM store_settings WHERE id = 1 LIMIT 1;";

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new StoreSettings
        {
            StoreName = reader.GetString(0),
            Cnpj = reader.GetString(1),
            Address = reader.GetString(2),
            Timezone = reader.GetString(3),
            Currency = reader.GetString(4),
            LogoUrl = reader.GetString(5),
            LogoLocalPath = reader.GetString(6),
            UpdatedAt = DateTimeOffset.Parse(reader.GetString(7))
        };
    }

    public async Task UpsertAsync(StoreSettings settings, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
INSERT INTO store_settings (id, store_name, cnpj, address, timezone, currency, logo_url, logo_local_path, updated_at)
VALUES (1, $storeName, $cnpj, $address, $timezone, $currency, $logoUrl, $logoLocalPath, $updatedAt)
ON CONFLICT(id) DO UPDATE SET
    store_name = excluded.store_name,
    cnpj = excluded.cnpj,
    address = excluded.address,
    timezone = excluded.timezone,
    currency = excluded.currency,
    logo_url = excluded.logo_url,
    logo_local_path = excluded.logo_local_path,
    updated_at = excluded.updated_at;";

        cmd.Parameters.AddWithValue("$storeName", settings.StoreName);
        cmd.Parameters.AddWithValue("$cnpj", settings.Cnpj);
        cmd.Parameters.AddWithValue("$address", settings.Address);
        cmd.Parameters.AddWithValue("$timezone", settings.Timezone);
        cmd.Parameters.AddWithValue("$currency", settings.Currency);
        cmd.Parameters.AddWithValue("$logoUrl", settings.LogoUrl);
        cmd.Parameters.AddWithValue("$logoLocalPath", settings.LogoLocalPath);
        cmd.Parameters.AddWithValue("$updatedAt", settings.UpdatedAt.ToString("O"));

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
