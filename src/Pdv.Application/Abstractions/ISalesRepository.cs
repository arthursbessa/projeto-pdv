using Pdv.Application.Domain;

namespace Pdv.Application.Abstractions;

public interface ISalesRepository
{
    Task SaveSaleWithOutboxAsync(Sale sale, string outboxPayloadJson, string? cashRegisterSessionId = null, CancellationToken cancellationToken = default);
}
