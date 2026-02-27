using System.Security.Cryptography;
using System.Text;
using Pdv.Application.Abstractions;
using Pdv.Application.Domain;
using Pdv.Infrastructure.Persistence;

namespace Pdv.Infrastructure.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public UserRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<UserAccount?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"SELECT id, username, full_name, password_hash, active, created_at, updated_at FROM users WHERE lower(username) = lower($username) LIMIT 1;";
        cmd.Parameters.AddWithValue("$username", username.Trim());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var user = ReadUser(reader);
        if (!user.Active || user.PasswordHash != HashPassword(password))
        {
            return null;
        }

        return user;
    }

    public async Task<IReadOnlyList<UserAccount>> SearchAsync(string? query, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"SELECT id, username, full_name, password_hash, active, created_at, updated_at FROM users WHERE $query = '' OR username LIKE $like OR full_name LIKE $like ORDER BY active DESC, username ASC;";
        cmd.Parameters.AddWithValue("$query", query?.Trim() ?? string.Empty);
        cmd.Parameters.AddWithValue("$like", $"%{query?.Trim() ?? string.Empty}%");

        var result = new List<UserAccount>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(ReadUser(reader));
        }

        return result;
    }

    public async Task<UserAccount?> FindByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"SELECT id, username, full_name, password_hash, active, created_at, updated_at FROM users WHERE id = $id LIMIT 1;";
        cmd.Parameters.AddWithValue("$id", id);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadUser(reader) : null;
    }

    public async Task AddAsync(UserAccount user, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"INSERT INTO users (id, username, full_name, password_hash, active, created_at, updated_at) VALUES ($id, $username, $fullName, $passwordHash, $active, $createdAt, $updatedAt);";
        BindUser(cmd, user);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(UserAccount user, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"UPDATE users SET username = $username, full_name = $fullName, password_hash = $passwordHash, active = $active, updated_at = $updatedAt WHERE id = $id;";
        BindUser(cmd, user);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ToggleActiveAsync(string id, bool active, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"UPDATE users SET active = $active, updated_at = $updatedAt WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$active", active ? 1 : 0);
        cmd.Parameters.AddWithValue("$updatedAt", DateTimeOffset.UtcNow.ToString("O"));
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SeedAdminAsync(CancellationToken cancellationToken = default)
    {
        var existing = await SearchAsync("admin", cancellationToken);
        if (existing.Any(x => x.Username.Equals("admin", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        await AddAsync(new UserAccount
        {
            Id = Guid.NewGuid().ToString(),
            Username = "admin",
            FullName = "Administrador",
            PasswordHash = HashPassword("admin"),
            Active = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);
    }

    public static string HashPassword(string password)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(hash);
    }

    private static UserAccount ReadUser(Microsoft.Data.Sqlite.SqliteDataReader reader) => new()
    {
        Id = reader.GetString(0),
        Username = reader.GetString(1),
        FullName = reader.GetString(2),
        PasswordHash = reader.GetString(3),
        Active = reader.GetInt32(4) == 1,
        CreatedAt = DateTimeOffset.Parse(reader.GetString(5)),
        UpdatedAt = DateTimeOffset.Parse(reader.GetString(6))
    };

    private static void BindUser(Microsoft.Data.Sqlite.SqliteCommand cmd, UserAccount user)
    {
        cmd.Parameters.AddWithValue("$id", user.Id);
        cmd.Parameters.AddWithValue("$username", user.Username.Trim());
        cmd.Parameters.AddWithValue("$fullName", user.FullName.Trim());
        cmd.Parameters.AddWithValue("$passwordHash", user.PasswordHash);
        cmd.Parameters.AddWithValue("$active", user.Active ? 1 : 0);
        cmd.Parameters.AddWithValue("$createdAt", user.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$updatedAt", user.UpdatedAt.ToString("O"));
    }
}
