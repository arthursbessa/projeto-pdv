using Pdv.Application.Domain;

namespace Pdv.Application.Abstractions;

public interface ISuppliersApiClient
{
    Task<LookupOption> CreateAsync(SupplierCreateRequest request, CancellationToken cancellationToken = default);
}
