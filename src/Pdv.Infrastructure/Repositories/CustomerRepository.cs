using Pdv.Application.Abstractions;
using Pdv.Application.Domain;
using Pdv.Infrastructure.Persistence;

namespace Pdv.Infrastructure.Repositories;

public sealed class CustomerRepository : ICustomerRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public CustomerRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<CustomerRecord>> SearchAsync(string? query, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        var normalizedQuery = query?.Trim() ?? string.Empty;
        var command = connection.CreateCommand();
        command.CommandText = @"
SELECT id, document_number, name, phone, email, address, notes, active, created_at, updated_at
FROM customers
WHERE active = 1
  AND ($query = ''
       OR name LIKE $like
       OR document_number LIKE $like
       OR phone LIKE $like
       OR email LIKE $like)
ORDER BY name ASC
LIMIT 200;";
        command.Parameters.AddWithValue("$query", normalizedQuery);
        command.Parameters.AddWithValue("$like", $"%{normalizedQuery}%");

        var customers = new List<CustomerRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            customers.Add(ReadCustomer(reader));
        }

        return customers;
    }

    public async Task<CustomerRecord?> FindByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
SELECT id, document_number, name, phone, email, address, notes, active, created_at, updated_at
FROM customers
WHERE id = $id
LIMIT 1;";
        command.Parameters.AddWithValue("$id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadCustomer(reader) : null;
    }

    public async Task UpsertAsync(CustomerRecord customer, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO customers (id, document_number, name, phone, email, address, notes, active, created_at, updated_at)
VALUES ($id, $documentNumber, $name, $phone, $email, $address, $notes, $active, $createdAt, $updatedAt)
ON CONFLICT(id) DO UPDATE SET
    document_number = excluded.document_number,
    name = excluded.name,
    phone = excluded.phone,
    email = excluded.email,
    address = excluded.address,
    notes = excluded.notes,
    active = excluded.active,
    updated_at = excluded.updated_at;";

        command.Parameters.AddWithValue("$id", customer.Id);
        command.Parameters.AddWithValue("$documentNumber", customer.Cpf);
        command.Parameters.AddWithValue("$name", customer.Name);
        command.Parameters.AddWithValue("$phone", customer.Phone);
        command.Parameters.AddWithValue("$email", customer.Email);
        command.Parameters.AddWithValue("$address", customer.Address);
        command.Parameters.AddWithValue("$notes", customer.Notes);
        command.Parameters.AddWithValue("$active", customer.Active ? 1 : 0);
        command.Parameters.AddWithValue("$createdAt", customer.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$updatedAt", customer.UpdatedAt.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM customers WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static CustomerRecord ReadCustomer(Microsoft.Data.Sqlite.SqliteDataReader reader) => new()
    {
        Id = reader.GetString(0),
        Cpf = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
        Name = reader.GetString(2),
        Phone = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
        Email = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
        Address = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
        Notes = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
        Active = reader.GetInt32(7) == 1,
        CreatedAt = DateTimeOffset.Parse(reader.GetString(8)),
        UpdatedAt = DateTimeOffset.Parse(reader.GetString(9))
    };
}
