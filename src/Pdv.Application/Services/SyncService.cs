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
        var events = await _outboxRepository.GetPendingEventsAsync(now, 100, cancellationToken);
        var sent = 0;

        foreach (var pending in events)
        {
            try
            {
                await _salesApiClient.SendSaleAsync(pending.PayloadJson, cancellationToken);
                await _outboxRepository.MarkAsSentAsync(pending.Id, DateTimeOffset.UtcNow, cancellationToken);
                sent++;
            }
            catch (Exception ex)
            {
                var attempts = pending.Attempts + 1;
                var delay = SyncBackoffPolicy.NextDelay(attempts);
                var nextRetry = DateTimeOffset.UtcNow.Add(delay);

                await _outboxRepository.MarkForRetryAsync(
                    pending.Id,
                    attempts,
                    nextRetry,
                    ex.Message,
                    cancellationToken);
            }
        }

        return sent;
    }
}
