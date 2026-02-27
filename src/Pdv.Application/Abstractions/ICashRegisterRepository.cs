using Pdv.Application.Domain;

namespace Pdv.Application.Abstractions;

public interface ICashRegisterRepository
{
    Task<CashRegisterSession?> GetOpenSessionAsync(CancellationToken cancellationToken = default);
    Task<CashRegisterSession> OpenAsync(int openingAmountCents, string userId, DateTimeOffset now, CancellationToken cancellationToken = default);
    Task CloseAsync(string sessionId, string userId, DateTimeOffset now, CancellationToken cancellationToken = default);
    Task RegisterWithdrawalAsync(string sessionId, int amountCents, string reason, string userId, DateTimeOffset now, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SaleSummary>> GetSalesBySessionAsync(string sessionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SalesReportEntry>> GetSalesReportBySessionAsync(string sessionId, CancellationToken cancellationToken = default);
}
