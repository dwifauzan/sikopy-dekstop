using System;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using SIKOPY.Helpers;
using SIKOPY.Models;

namespace SIKOPY.Database;

public sealed class AuthRepository
{
    public async Task<UserAccount?> AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        var trimmedUsername = username.Trim();
        if (trimmedUsername.Length == 0 || string.IsNullOrEmpty(password))
            return null;

        await using var connection = await PostgresConnectionFactory.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT id_pengguna, nama_pengguna, kata_sandi, nama_lengkap, peran
            FROM pengguna
            WHERE nama_pengguna = @username
            LIMIT 1;
            """,
            connection);
        command.Parameters.AddWithValue("username", trimmedUsername);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        var passwordHash = reader.GetString(2);
        if (!PasswordHasher.Verify(password, passwordHash))
            return null;

        return new UserAccount
        {
            Id = reader.GetInt32(0),
            Username = reader.GetString(1),
            FullName = reader.GetString(3),
            Role = reader.GetString(4)
        };
    }

    public async Task<UserAccount> CreateUserAsync(
        string username,
        string password,
        string fullName,
        string role,
        CancellationToken cancellationToken = default)
    {
        var trimmedUsername = username.Trim();
        var trimmedFullName = fullName.Trim();
        var trimmedRole = role.Trim();

        ArgumentException.ThrowIfNullOrWhiteSpace(trimmedUsername);
        ArgumentException.ThrowIfNullOrEmpty(password);
        ArgumentException.ThrowIfNullOrWhiteSpace(trimmedFullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(trimmedRole);

        var passwordHash = PasswordHasher.Hash(password);

        await using var connection = await PostgresConnectionFactory.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO pengguna (nama_pengguna, kata_sandi, nama_lengkap, peran)
            VALUES (@username, @passwordHash, @fullName, @role)
            RETURNING id_pengguna, nama_pengguna, nama_lengkap, peran;
            """,
            connection);
        command.Parameters.AddWithValue("username", trimmedUsername);
        command.Parameters.AddWithValue("passwordHash", passwordHash);
        command.Parameters.AddWithValue("fullName", trimmedFullName);
        command.Parameters.AddWithValue("role", trimmedRole);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);

        return new UserAccount
        {
            Id = reader.GetInt32(0),
            Username = reader.GetString(1),
            FullName = reader.GetString(2),
            Role = reader.GetString(3)
        };
    }
}
