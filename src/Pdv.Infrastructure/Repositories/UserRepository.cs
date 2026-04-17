using System.Security.Cryptography;
using System.Text;
using Pdv.Application.Abstractions;
using Pdv.Application.Domain;
using Pdv.Application.Utilities;
using Pdv.Infrastructure.Persistence;

namespace Pdv.Infrastructure.Repositories;

public sealed class UserRepository : IUserRepository
{
    private const string EncryptedPrefix = "enc:";
    private static readonly byte[] EncryptionKey = SHA256.HashData(Encoding.UTF8.GetBytes(Environment.MachineName));
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
        cmd.Parameters.AddWithValue("$username", TextNormalization.TrimToEmpty(username));

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var user = ReadUser(reader);
        var passwordHash = DecryptHashIfNeeded(user.PasswordHash);
        if (!user.Active || passwordHash != HashPassword(password))
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
        var normalizedQuery = TextNormalization.TrimToEmpty(query);
        cmd.Parameters.AddWithValue("$query", normalizedQuery);
        cmd.Parameters.AddWithValue("$like", $"%{normalizedQuery}%");

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
        BindUser(cmd, user, encryptPasswordHash: false);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(UserAccount user, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"UPDATE users SET username = $username, full_name = $fullName, password_hash = $passwordHash, active = $active, updated_at = $updatedAt WHERE id = $id;";
        BindUser(cmd, user, encryptPasswordHash: false);
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

    public async Task UpsertSyncedUsersAsync(IEnumerable<UserAccount> users, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        foreach (var user in users)
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
INSERT INTO users (id, username, full_name, password_hash, active, created_at, updated_at)
VALUES ($id, $username, $fullName, $passwordHash, $active, $createdAt, $updatedAt)
ON CONFLICT(id) DO UPDATE SET
    username = excluded.username,
    full_name = excluded.full_name,
    password_hash = excluded.password_hash,
    active = excluded.active,
    updated_at = excluded.updated_at;";

            BindUser(cmd, user, encryptPasswordHash: true);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
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

    private static void BindUser(Microsoft.Data.Sqlite.SqliteCommand cmd, UserAccount user, bool encryptPasswordHash)
    {
        cmd.Parameters.AddWithValue("$id", user.Id);
        cmd.Parameters.AddWithValue("$username", TextNormalization.TrimToEmpty(user.Username));
        cmd.Parameters.AddWithValue("$fullName", TextNormalization.TrimToEmpty(user.FullName));
        cmd.Parameters.AddWithValue("$passwordHash", encryptPasswordHash ? EncryptHash(user.PasswordHash) : user.PasswordHash);
        cmd.Parameters.AddWithValue("$active", user.Active ? 1 : 0);
        cmd.Parameters.AddWithValue("$createdAt", user.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$updatedAt", user.UpdatedAt.ToString("O"));
    }

    private static string DecryptHashIfNeeded(string value)
    {
        if (!value.StartsWith(EncryptedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        var payload = Convert.FromBase64String(value[EncryptedPrefix.Length..]);
        var iv = payload[..16];
        var cipher = payload[16..];

        using var aes = Aes.Create();
        aes.Key = EncryptionKey;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
        return Encoding.UTF8.GetString(plainBytes);
    }

    private static string EncryptHash(string plainHash)
    {
        using var aes = Aes.Create();
        aes.Key = EncryptionKey;
        aes.GenerateIV();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainHash);
        var cipher = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        var payload = new byte[aes.IV.Length + cipher.Length];
        Buffer.BlockCopy(aes.IV, 0, payload, 0, aes.IV.Length);
        Buffer.BlockCopy(cipher, 0, payload, aes.IV.Length, cipher.Length);

        return EncryptedPrefix + Convert.ToBase64String(payload);
    }
}
