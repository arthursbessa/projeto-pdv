using Pdv.Application.Domain;

namespace Pdv.Application.Abstractions;

public interface IOutboxRepository
{
    Task<IReadOnlyList<OutboxEvent>> GetPendingEventsAsync(DateTimeOffset now, int take, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, int>> GetPendingCountsByTypeAsync(CancellationToken cancellationToken = default);
    Task EnqueueAsync(string type, string payloadJson, CancellationToken cancellationToken = default);
    Task MarkAsSentAsync(Guid id, DateTimeOffset sentAt, CancellationToken cancellationToken = default);
    Task MarkForRetryAsync(Guid id, int attempts, DateTimeOffset nextRetryAt, string error, CancellationToken cancellationToken = default);
    Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default);
}
