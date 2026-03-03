using Pdv.Application.Domain;

namespace Pdv.Application.Abstractions;

public interface IUserRepository
{
    Task<UserAccount?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserAccount>> SearchAsync(string? query, CancellationToken cancellationToken = default);
    Task<UserAccount?> FindByIdAsync(string id, CancellationToken cancellationToken = default);
    Task AddAsync(UserAccount user, CancellationToken cancellationToken = default);
    Task UpdateAsync(UserAccount user, CancellationToken cancellationToken = default);
    Task ToggleActiveAsync(string id, bool active, CancellationToken cancellationToken = default);
    Task SeedAdminAsync(CancellationToken cancellationToken = default);
    Task UpsertSyncedUsersAsync(IEnumerable<UserAccount> users, CancellationToken cancellationToken = default);
}
