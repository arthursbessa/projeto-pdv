using Pdv.Application.Domain;

namespace Pdv.Application.Abstractions;

public interface ICustomerRepository
{
    Task<IReadOnlyList<CustomerRecord>> SearchAsync(string? query, CancellationToken cancellationToken = default);
    Task<CustomerRecord?> FindByIdAsync(string id, CancellationToken cancellationToken = default);
    Task UpsertAsync(CustomerRecord customer, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
