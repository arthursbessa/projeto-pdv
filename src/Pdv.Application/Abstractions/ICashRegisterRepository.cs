using Pdv.Application.Domain;

namespace Pdv.Application.Abstractions;

public interface ICashRegisterRepository
{
    Task<CashRegisterSession?> GetOpenSessionAsync(CancellationToken cancellationToken = default);
    Task<CashRegisterSession> OpenAsync(int openingAmountCents, string userId, DateTimeOffset now, CancellationToken cancellationToken = default);
    Task CloseAsync(string sessionId, int closingAmountCents, string userId, DateTimeOffset now, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SaleSummary>> GetSalesBySessionAsync(string sessionId, CancellationToken cancellationToken = default);
}
