using Pdv.Application.Domain;

namespace Pdv.Application.Abstractions;

public interface ISalesApiClient
{
    Task<SaleSyncResult> SendSaleAsync(string payloadJson, CancellationToken cancellationToken = default);
    Task MarkPrintedAsync(string payloadJson, CancellationToken cancellationToken = default);
}
