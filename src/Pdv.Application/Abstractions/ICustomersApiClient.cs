using Pdv.Application.Domain;

namespace Pdv.Application.Abstractions;

public interface ICustomersApiClient
{
    Task<IReadOnlyCollection<CustomerRecord>> GetCustomersAsync(string? search = null, int limit = 200, CancellationToken cancellationToken = default);
    Task<CustomerRecord> CreateCustomerAsync(CustomerRecord customer, CancellationToken cancellationToken = default);
    Task<CustomerRecord> UpdateCustomerAsync(CustomerRecord customer, CancellationToken cancellationToken = default);
    Task DeleteCustomerAsync(string customerId, CancellationToken cancellationToken = default);
}
