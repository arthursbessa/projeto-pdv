using Pdv.Application.Abstractions;
using Pdv.Application.Domain;
using Pdv.Infrastructure.Persistence;

namespace Pdv.Infrastructure.Repositories;

public sealed class CashRegisterRepository : ICashRegisterRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public CashRegisterRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<CashRegisterSession?> GetOpenSessionAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"SELECT id, opened_at, closed_at, opening_amount_cents, closing_amount_cents, status, business_date, opened_by_user_id, closed_by_user_id FROM cash_register_sessions WHERE status = 'OPEN' ORDER BY opened_at DESC LIMIT 1;";
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadSession(reader) : null;
    }

    public async Task<CashRegisterSession> OpenAsync(int openingAmountCents, string userId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var openSession = await GetOpenSessionAsync(cancellationToken);
        if (openSession is not null)
        {
            throw new InvalidOperationException("Já existe um caixa aberto.");
        }

        var session = new CashRegisterSession
        {
            Id = Guid.NewGuid().ToString(),
            OpenedAt = now,
            OpeningAmountCents = openingAmountCents,
            Status = "OPEN",
            BusinessDate = now.ToString("yyyy-MM-dd"),
            OpenedByUserId = userId
        };

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"INSERT INTO cash_register_sessions (id, opened_at, closed_at, opening_amount_cents, closing_amount_cents, status, business_date, opened_by_user_id, closed_by_user_id) VALUES ($id, $openedAt, NULL, $openingAmountCents, NULL, 'OPEN', $businessDate, $openedByUserId, NULL);";
        cmd.Parameters.AddWithValue("$id", session.Id);
        cmd.Parameters.AddWithValue("$openedAt", session.OpenedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$openingAmountCents", session.OpeningAmountCents);
        cmd.Parameters.AddWithValue("$businessDate", session.BusinessDate);
        cmd.Parameters.AddWithValue("$openedByUserId", userId);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
        return session;
    }

    public async Task CloseAsync(string sessionId, int closingAmountCents, string userId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"UPDATE cash_register_sessions SET status = 'CLOSED', closed_at = $closedAt, closing_amount_cents = $closingAmountCents, closed_by_user_id = $closedByUserId WHERE id = $id AND status = 'OPEN';";
        cmd.Parameters.AddWithValue("$id", sessionId);
        cmd.Parameters.AddWithValue("$closedAt", now.ToString("O"));
        cmd.Parameters.AddWithValue("$closingAmountCents", closingAmountCents);
        cmd.Parameters.AddWithValue("$closedByUserId", userId);
        var count = await cmd.ExecuteNonQueryAsync(cancellationToken);
        if (count == 0)
        {
            throw new InvalidOperationException("Caixa não encontrado ou já está fechado.");
        }
    }

    public async Task<IReadOnlyList<SaleSummary>> GetSalesBySessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"SELECT id, created_at, payment_method, total_cents FROM sales WHERE cash_register_session_id = $sessionId ORDER BY created_at DESC;";
        cmd.Parameters.AddWithValue("$sessionId", sessionId);

        var result = new List<SaleSummary>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new SaleSummary
            {
                SaleId = reader.GetString(0),
                CreatedAt = DateTimeOffset.Parse(reader.GetString(1)),
                PaymentMethod = reader.GetString(2),
                TotalCents = reader.GetInt32(3)
            });
        }

        return result;
    }

    private static CashRegisterSession ReadSession(Microsoft.Data.Sqlite.SqliteDataReader reader) => new()
    {
        Id = reader.GetString(0),
        OpenedAt = DateTimeOffset.Parse(reader.GetString(1)),
        ClosedAt = reader.IsDBNull(2) ? null : DateTimeOffset.Parse(reader.GetString(2)),
        OpeningAmountCents = reader.GetInt32(3),
        ClosingAmountCents = reader.IsDBNull(4) ? null : reader.GetInt32(4),
        Status = reader.GetString(5),
        BusinessDate = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
        OpenedByUserId = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
        ClosedByUserId = reader.IsDBNull(8) ? null : reader.GetString(8)
    };
}
