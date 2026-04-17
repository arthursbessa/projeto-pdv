using Pdv.Application.Abstractions;
using Pdv.Application.Domain;
using Pdv.Application.Utilities;
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
        cmd.CommandText = @"SELECT store_name, terminal_name, cnpj, address, timezone, currency, logo_url, logo_local_path, updated_at FROM store_settings WHERE id = 1 LIMIT 1;";

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new StoreSettings
        {
            StoreName = TextNormalization.TrimToEmpty(reader.GetString(0)),
            TerminalName = reader.IsDBNull(1) ? string.Empty : TextNormalization.TrimToEmpty(reader.GetString(1)),
            Cnpj = TextNormalization.FormatTaxIdPartial(reader.GetString(2)),
            Address = TextNormalization.TrimToEmpty(reader.GetString(3)),
            Timezone = TextNormalization.TrimToEmpty(reader.GetString(4)),
            Currency = TextNormalization.TrimToEmpty(reader.GetString(5)),
            LogoUrl = TextNormalization.TrimToEmpty(reader.GetString(6)),
            LogoLocalPath = TextNormalization.TrimToEmpty(reader.GetString(7)),
            UpdatedAt = DateTimeOffset.Parse(reader.GetString(8))
        };
    }

    public async Task UpsertAsync(StoreSettings settings, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
INSERT INTO store_settings (id, store_name, terminal_name, cnpj, address, timezone, currency, logo_url, logo_local_path, updated_at)
VALUES (1, $storeName, $terminalName, $cnpj, $address, $timezone, $currency, $logoUrl, $logoLocalPath, $updatedAt)
ON CONFLICT(id) DO UPDATE SET
    store_name = excluded.store_name,
    terminal_name = excluded.terminal_name,
    cnpj = excluded.cnpj,
    address = excluded.address,
    timezone = excluded.timezone,
    currency = excluded.currency,
    logo_url = excluded.logo_url,
    logo_local_path = excluded.logo_local_path,
    updated_at = excluded.updated_at;";

        cmd.Parameters.AddWithValue("$storeName", TextNormalization.TrimToEmpty(settings.StoreName));
        cmd.Parameters.AddWithValue("$terminalName", TextNormalization.TrimToEmpty(settings.TerminalName));
        cmd.Parameters.AddWithValue("$cnpj", TextNormalization.FormatTaxId(settings.Cnpj) ?? string.Empty);
        cmd.Parameters.AddWithValue("$address", TextNormalization.TrimToEmpty(settings.Address));
        cmd.Parameters.AddWithValue("$timezone", TextNormalization.TrimToEmpty(settings.Timezone));
        cmd.Parameters.AddWithValue("$currency", TextNormalization.TrimToEmpty(settings.Currency));
        cmd.Parameters.AddWithValue("$logoUrl", TextNormalization.TrimToEmpty(settings.LogoUrl));
        cmd.Parameters.AddWithValue("$logoLocalPath", TextNormalization.TrimToEmpty(settings.LogoLocalPath));
        cmd.Parameters.AddWithValue("$updatedAt", settings.UpdatedAt.ToString("O"));

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
