using Pdv.Application.Domain;

namespace Pdv.Application.Abstractions;

public interface IAuthApiClient
{
    Task<UserAccount?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default);
}
