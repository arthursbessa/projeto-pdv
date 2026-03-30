namespace Pdv.Application.Abstractions;

public interface IRefundsApiClient
{
    Task RegisterRefundAsync(string payloadJson, CancellationToken cancellationToken = default);
}
