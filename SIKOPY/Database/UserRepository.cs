using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using SIKOPY.Helpers;
using SIKOPY.Models;

namespace SIKOPY.Database;

/// <summary>Kelola akun untuk halaman Pengguna (khusus admin).</summary>
public sealed class UserRepository
{
    public async Task<IReadOnlyList<UserAccount>> GetUsersAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            await PostgresConnectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id_pengguna, nama_pengguna, nama_lengkap, peran, is_aktif
            FROM pengguna
            ORDER BY is_aktif DESC, nama_lengkap, id_pengguna;
            """;

        var users = new List<UserAccount>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            users.Add(new UserAccount
            {
                Id = reader.GetInt32(0),
                Username = reader.GetString(1),
                FullName = reader.GetString(2),
                Role = reader.GetString(3),
                IsActive = reader.GetBoolean(4)
            });
        }

        return users;
    }

    public Task UpdateRoleAsync(int userId, string role, CancellationToken cancellationToken = default)
    {
        var normalized = UserAccount.Roles.FirstOrDefault(
            r => string.Equals(r, role?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Peran tidak dikenal.");

        return ChangeAccountAsync(
            userId, "UPDATE pengguna SET peran = $2 WHERE id_pengguna = $1;", normalized,
            removesAdmin: normalized != UserAccount.AdminRole, cancellationToken);
    }

    public Task SetActiveAsync(int userId, bool isActive, CancellationToken cancellationToken = default) =>
        ChangeAccountAsync(
            userId, "UPDATE pengguna SET is_aktif = $2 WHERE id_pengguna = $1;", isActive,
            removesAdmin: !isActive, cancellationToken);

    /// <summary>Ganti password dengan password acak baru; kembalikan teks aslinya sekali saja.</summary>
    public async Task<string> ResetPasswordAsync(int userId, CancellationToken cancellationToken = default)
    {
        // Tanpa huruf/angka yang mirip (0/O, 1/l/I) supaya mudah didiktekan.
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
        var password = "Sikopy-" + RandomNumberGenerator.GetString(alphabet, 12);

        await using var connection =
            await PostgresConnectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE pengguna SET kata_sandi = $2 WHERE id_pengguna = $1;";
        command.Parameters.AddWithValue(userId);
        command.Parameters.AddWithValue(PasswordHasher.Hash(password));

        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("Pengguna tidak ditemukan. Muat ulang daftar.");

        return password;
    }

    /// <summary>
    /// Jalankan satu perubahan akun. Bila perubahan itu mencabut status admin aktif,
    /// tolak jika target adalah admin aktif terakhir (supaya tidak terkunci).
    /// </summary>
    private static async Task ChangeAccountAsync(
        int userId,
        string sql,
        object value,
        bool removesAdmin,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await PostgresConnectionFactory.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            if (removesAdmin)
            {
                // Kunci semua admin aktif supaya dua admin tidak saling mencabut bersamaan.
                var activeAdmins = new List<int>();
                await using (var lockCommand = connection.CreateCommand())
                {
                    lockCommand.Transaction = transaction;
                    lockCommand.CommandText = """
                        SELECT id_pengguna FROM pengguna
                        WHERE LOWER(TRIM(peran)) = LOWER($1) AND is_aktif = TRUE
                        ORDER BY id_pengguna
                        FOR UPDATE;
                        """;
                    lockCommand.Parameters.AddWithValue(UserAccount.AdminRole);
                    await using var reader = await lockCommand.ExecuteReaderAsync(cancellationToken);
                    while (await reader.ReadAsync(cancellationToken))
                        activeAdmins.Add(reader.GetInt32(0));
                }

                if (activeAdmins.Count == 1 && activeAdmins[0] == userId)
                    throw new InvalidOperationException(
                        "Ini admin aktif terakhir. Jadikan akun lain admin dulu.");
            }

            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = sql;
                command.Parameters.AddWithValue(userId);
                command.Parameters.AddWithValue(value);
                if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
                    throw new InvalidOperationException("Pengguna tidak ditemukan. Muat ulang daftar.");
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
