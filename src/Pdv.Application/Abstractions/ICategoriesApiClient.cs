using Pdv.Application.Domain;

namespace Pdv.Application.Abstractions;

public interface ICategoriesApiClient
{
    Task<LookupOption> CreateAsync(string name, string? parentId = null, CancellationToken cancellationToken = default);
}
