using Pdv.Application.Abstractions;

namespace Pdv.Infrastructure.Api;

public sealed class HttpSalesApiClient : ISalesApiClient
{
    public Task SendSaleAsync(string payloadJson, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
