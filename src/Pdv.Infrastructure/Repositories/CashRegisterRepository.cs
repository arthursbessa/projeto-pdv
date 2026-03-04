using Pdv.Application.Abstractions;
using Pdv.Application.Domain;
using Pdv.Infrastructure.Persistence;
using System.Text.Json;
using System.Net;

namespace Pdv.Infrastructure.Repositories;

public sealed class CashRegisterRepository : ICashRegisterRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly ICashRegisterApiClient _cashRegisterApiClient;

    public CashRegisterRepository(SqliteConnectionFactory connectionFactory, ICashRegisterApiClient cashRegisterApiClient)
    {
        _connectionFactory = connectionFactory;
        _cashRegisterApiClient = cashRegisterApiClient;
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

    public async Task<CashRegisterSession?> GetLastClosedSessionAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"SELECT id, opened_at, closed_at, opening_amount_cents, closing_amount_cents, status, business_date, opened_by_user_id, closed_by_user_id FROM cash_register_sessions WHERE status = 'CLOSED' ORDER BY closed_at DESC LIMIT 1;";
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

        var remoteSessionId = await _cashRegisterApiClient.OpenAsync(userId, openingAmountCents / 100m, now, cancellationToken);

        var session = new CashRegisterSession
        {
            Id = remoteSessionId,
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

    public async Task CloseAsync(string sessionId, string userId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        var totalSales = await GetTotalSalesAsync(connection, sessionId, cancellationToken);
        var totalWithdrawals = await GetTotalWithdrawalsAsync(connection, sessionId, cancellationToken);
        var openingAmount = await GetOpeningAmountAsync(connection, sessionId, cancellationToken);
        var closingAmountCents = openingAmount + totalSales - totalWithdrawals;

        try
        {
            await _cashRegisterApiClient.CloseAsync(sessionId, userId, closingAmountCents / 100m, now, null, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Sessões antigas podem já ter sido removidas/encerradas remotamente.
            // Ainda assim precisamos fechar o caixa local para permitir o login.
        }

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

    public async Task RegisterWithdrawalAsync(string sessionId, int amountCents, string reason, string userId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        if (amountCents <= 0)
        {
            throw new InvalidOperationException("Valor de sangria deve ser maior que zero.");
        }

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        var existingUserId = await ResolveExistingUserIdAsync(connection, userId, cancellationToken);

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"INSERT INTO cash_withdrawals (id, cash_register_session_id, amount_cents, reason, created_at, created_by_user_id)
                            VALUES ($id, $sessionId, $amountCents, $reason, $createdAt, $createdByUserId);";
        cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        cmd.Parameters.AddWithValue("$sessionId", sessionId);
        cmd.Parameters.AddWithValue("$amountCents", amountCents);
        cmd.Parameters.AddWithValue("$reason", reason);
        cmd.Parameters.AddWithValue("$createdAt", now.ToString("O"));
        cmd.Parameters.AddWithValue("$createdByUserId", existingUserId ?? (object)DBNull.Value);
        await cmd.ExecuteNonQueryAsync(cancellationToken);

        var outboxPayload = JsonSerializer.Serialize(new
        {
            session_id = sessionId,
            operator_id = userId,
            amount = amountCents / 100m,
            description = reason
        });

        var outboxCommand = connection.CreateCommand();
        outboxCommand.CommandText = @"INSERT INTO outbox_events (id, type, payload_json, status, attempts, next_retry_at, last_error, created_at, sent_at)
VALUES ($id, $type, $payloadJson, 'Pending', 0, NULL, NULL, $createdAt, NULL);";
        outboxCommand.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        outboxCommand.Parameters.AddWithValue("$type", "CashWithdrawalCreated");
        outboxCommand.Parameters.AddWithValue("$payloadJson", outboxPayload);
        outboxCommand.Parameters.AddWithValue("$createdAt", DateTimeOffset.UtcNow.ToString("O"));
        await outboxCommand.ExecuteNonQueryAsync(cancellationToken);
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

    public async Task<CashStatusSnapshot> GetCashStatusSnapshotAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var openSession = await GetOpenSessionAsync(cancellationToken);
        if (openSession is null)
        {
            return new CashStatusSnapshot
            {
                IsOpen = false,
                BusinessDate = now.ToString("yyyy-MM-dd")
            };
        }

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        var salesTotalCents = await GetTotalSalesAsync(connection, openSession.Id, cancellationToken);
        var withdrawalsTotalCents = await GetTotalWithdrawalsAsync(connection, openSession.Id, cancellationToken);

        var txCommand = connection.CreateCommand();
        txCommand.CommandText = @"
SELECT 'SALE' AS tx_type,
       s.id AS reference_id,
       s.created_at,
       s.total_cents AS amount_cents
FROM sales s
WHERE s.cash_register_session_id = $sessionId
  AND DATE(s.created_at) = DATE($businessDate)
UNION ALL
SELECT 'WITHDRAWAL' AS tx_type,
       COALESCE(w.reason, 'Sangria') AS reference_id,
       w.created_at,
       -w.amount_cents AS amount_cents
FROM cash_withdrawals w
WHERE w.cash_register_session_id = $sessionId
  AND DATE(w.created_at) = DATE($businessDate)
ORDER BY created_at DESC;";
        txCommand.Parameters.AddWithValue("$sessionId", openSession.Id);
        txCommand.Parameters.AddWithValue("$businessDate", openSession.BusinessDate);

        var transactions = new List<CashStatusTransaction>();
        await using var reader = await txCommand.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            transactions.Add(new CashStatusTransaction
            {
                Type = reader.GetString(0),
                Reference = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                CreatedAt = DateTimeOffset.Parse(reader.GetString(2)),
                AmountCents = reader.GetInt32(3)
            });
        }

        return new CashStatusSnapshot
        {
            IsOpen = true,
            SessionId = openSession.Id,
            BusinessDate = openSession.BusinessDate,
            OpeningAmountCents = openSession.OpeningAmountCents,
            SalesTotalCents = salesTotalCents,
            WithdrawalsTotalCents = withdrawalsTotalCents,
            CurrentBalanceCents = openSession.OpeningAmountCents + salesTotalCents - withdrawalsTotalCents,
            Transactions = transactions
        };
    }

    private static async Task<int> GetOpeningAmountAsync(Microsoft.Data.Sqlite.SqliteConnection connection, string sessionId, CancellationToken cancellationToken)
    {
        var opening = connection.CreateCommand();
        opening.CommandText = "SELECT opening_amount_cents FROM cash_register_sessions WHERE id = $sessionId LIMIT 1;";
        opening.Parameters.AddWithValue("$sessionId", sessionId);
        var openingValue = await opening.ExecuteScalarAsync(cancellationToken);
        return openingValue is null || openingValue is DBNull ? 0 : Convert.ToInt32(openingValue);
    }

    private static async Task<int> GetTotalSalesAsync(Microsoft.Data.Sqlite.SqliteConnection connection, string sessionId, CancellationToken cancellationToken)
    {
        var sales = connection.CreateCommand();
        sales.CommandText = "SELECT COALESCE(SUM(total_cents), 0) FROM sales WHERE cash_register_session_id = $sessionId;";
        sales.Parameters.AddWithValue("$sessionId", sessionId);
        var salesValue = await sales.ExecuteScalarAsync(cancellationToken);
        return salesValue is null || salesValue is DBNull ? 0 : Convert.ToInt32(salesValue);
    }

    private static async Task<int> GetTotalWithdrawalsAsync(Microsoft.Data.Sqlite.SqliteConnection connection, string sessionId, CancellationToken cancellationToken)
    {
        var withdrawals = connection.CreateCommand();
        withdrawals.CommandText = "SELECT COALESCE(SUM(amount_cents), 0) FROM cash_withdrawals WHERE cash_register_session_id = $sessionId;";
        withdrawals.Parameters.AddWithValue("$sessionId", sessionId);
        var withdrawalValue = await withdrawals.ExecuteScalarAsync(cancellationToken);
        return withdrawalValue is null || withdrawalValue is DBNull ? 0 : Convert.ToInt32(withdrawalValue);
    }

    private static async Task<string?> ResolveExistingUserIdAsync(Microsoft.Data.Sqlite.SqliteConnection connection, string userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var command = connection.CreateCommand();
        command.CommandText = "SELECT id FROM users WHERE id = $userId LIMIT 1;";
        command.Parameters.AddWithValue("$userId", userId);

        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is string existingUserId ? existingUserId : null;
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
