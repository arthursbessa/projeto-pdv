namespace Pdv.Application.Domain;

public sealed class UserAccount
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Username { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string PasswordHash { get; init; } = string.Empty;
    public bool Active { get; init; } = true;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
}
