namespace Pdv.Application.Abstractions;

public interface ISalesApiClient
{
    Task SendSaleAsync(string payloadJson, CancellationToken cancellationToken = default);
}
