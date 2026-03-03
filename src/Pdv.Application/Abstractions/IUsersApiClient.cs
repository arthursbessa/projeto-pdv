using Pdv.Application.Domain;

namespace Pdv.Application.Abstractions;

public interface IUsersApiClient
{
    Task<IReadOnlyCollection<UserAccount>> GetUsersAsync(CancellationToken cancellationToken = default);
}
