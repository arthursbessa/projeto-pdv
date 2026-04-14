using Pdv.Application.Domain;

namespace Pdv.Application.Abstractions;

public interface IReferenceDataApiClient
{
    Task<ReferenceDataSnapshot> GetReferenceDataAsync(CancellationToken cancellationToken = default);
}
