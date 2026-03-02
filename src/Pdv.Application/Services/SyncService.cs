using Pdv.Application.Abstractions;

namespace Pdv.Application.Services;

public sealed class SyncService
{
    private readonly IOutboxRepository _outboxRepository;
    private readonly ISalesApiClient _salesApiClient;

    public SyncService(IOutboxRepository outboxRepository, ISalesApiClient salesApiClient)
    {
        _outboxRepository = outboxRepository;
        _salesApiClient = salesApiClient;
    }

    public async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var events = await _outboxRepository.GetPendingEventsAsync(now, 50, cancellationToken);
        var sent = 0;

        foreach (var outboxEvent in events)
        {
            try
            {
                await _salesApiClient.SendSaleAsync(outboxEvent.PayloadJson, cancellationToken);
                await _outboxRepository.MarkAsSentAsync(outboxEvent.Id, DateTimeOffset.UtcNow, cancellationToken);
                sent++;
            }
            catch (Exception ex)
            {
                var attempts = outboxEvent.Attempts + 1;
                var nextRetry = DateTimeOffset.UtcNow.Add(SyncBackoffPolicy.NextDelay(attempts));
                await _outboxRepository.MarkForRetryAsync(outboxEvent.Id, attempts, nextRetry, ex.Message, cancellationToken);
            }
        }

        return sent;
    }
}
