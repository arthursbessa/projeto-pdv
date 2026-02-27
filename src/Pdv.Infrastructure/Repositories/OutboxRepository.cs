using Pdv.Application.Abstractions;
using Pdv.Application.Domain;
using Pdv.Infrastructure.Persistence;

namespace Pdv.Infrastructure.Repositories;

public sealed class OutboxRepository : IOutboxRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public OutboxRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<OutboxEvent>> GetPendingEventsAsync(DateTimeOffset now, int take, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
SELECT id, type, payload_json, status, attempts, next_retry_at, last_error, created_at
FROM outbox_events
WHERE status = $pendingStatus
  AND (next_retry_at IS NULL OR next_retry_at <= $now)
ORDER BY created_at
LIMIT $take;";

        command.Parameters.AddWithValue("$pendingStatus", (int)OutboxStatus.Pending);
        command.Parameters.AddWithValue("$now", now.ToString("O"));
        command.Parameters.AddWithValue("$take", take);

        var result = new List<OutboxEvent>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new OutboxEvent
            {
                Id = Guid.Parse(reader.GetString(0)),
                Type = reader.GetString(1),
                PayloadJson = reader.GetString(2),
                Status = (OutboxStatus)reader.GetInt32(3),
                Attempts = reader.GetInt32(4),
                NextRetryAt = reader.IsDBNull(5) ? null : DateTimeOffset.Parse(reader.GetString(5)),
                LastError = reader.IsDBNull(6) ? null : reader.GetString(6),
                CreatedAt = DateTimeOffset.Parse(reader.GetString(7))
            });
        }

        return result;
    }

    public async Task MarkAsSentAsync(Guid id, DateTimeOffset sentAt, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
UPDATE outbox_events
SET status = $status, sent_at = $sentAt, last_error = NULL
WHERE id = $id;";
        command.Parameters.AddWithValue("$status", (int)OutboxStatus.Sent);
        command.Parameters.AddWithValue("$sentAt", sentAt.ToString("O"));
        command.Parameters.AddWithValue("$id", id.ToString());

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task MarkForRetryAsync(Guid id, int attempts, DateTimeOffset nextRetryAt, string error, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
UPDATE outbox_events
SET status = $status,
    attempts = $attempts,
    next_retry_at = $nextRetryAt,
    last_error = $lastError
WHERE id = $id;";

        command.Parameters.AddWithValue("$status", (int)OutboxStatus.Pending);
        command.Parameters.AddWithValue("$attempts", attempts);
        command.Parameters.AddWithValue("$nextRetryAt", nextRetryAt.ToString("O"));
        command.Parameters.AddWithValue("$lastError", error);
        command.Parameters.AddWithValue("$id", id.ToString());

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM outbox_events WHERE status = $status;";
        command.Parameters.AddWithValue("$status", (int)OutboxStatus.Pending);

        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(value);
    }
}
