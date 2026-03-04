namespace Pdv.Application.Abstractions;

public interface ICashRegisterApiClient
{
    Task<string> OpenAsync(string operatorId, decimal amount, DateTimeOffset datetime, CancellationToken cancellationToken = default);
    Task CloseAsync(string sessionId, string operatorId, decimal amount, DateTimeOffset datetime, string? notes, CancellationToken cancellationToken = default);
    Task RegisterWithdrawalAsync(string sessionId, string operatorId, decimal amount, string description, CancellationToken cancellationToken = default);
}
