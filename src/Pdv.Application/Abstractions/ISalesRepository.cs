using Pdv.Application.Domain;

namespace Pdv.Application.Abstractions;

public interface ISalesRepository
{
    Task SaveSaleWithOutboxAsync(Sale sale, string outboxPayloadJson, string? cashRegisterSessionId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SaleHistoryEntry>> GetHistoryAsync(DateTime date, CancellationToken cancellationToken = default);
    Task<Sale?> FindByIdAsync(Guid saleId, CancellationToken cancellationToken = default);
    Task SaveRemoteSaleReferenceAsync(Guid localSaleId, string remoteSaleId, int? saleNumber, CancellationToken cancellationToken = default);
    Task SaveRefundAsync(Guid saleId, string reason, IReadOnlyCollection<SaleRefundItem> items, string? operatorId, CancellationToken cancellationToken = default);
    Task SaveRefundWithOutboxAsync(Guid saleId, string reason, IReadOnlyCollection<SaleRefundItem> items, string outboxPayloadJson, string? operatorId, CancellationToken cancellationToken = default);
}
