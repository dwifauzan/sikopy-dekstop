using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;
using SIKOPY.Models;

namespace SIKOPY.Database;

public sealed class RawMaterialRepository
{
    public async Task<IReadOnlyList<RawMaterial>> GetActiveAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            await PostgresConnectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                b.id_biji_mentah,
                b.nama_biji,
                j.id_jenis_biji_kopi_mentah,
                j.nama,
                m.id_metode_proses,
                m.nama,
                b.asal_daerah,
                b.ketinggian_mdpl,
                b.tingkat_kualitas,
                b.stok_kg,
                b.harga_per_kg,
                b.tanggal_panen,
                b.is_aktif
            FROM biji_kopi_mentah b
            INNER JOIN jenis_biji_kopi_mentah j
                ON j.id_jenis_biji_kopi_mentah = b.id_jenis_biji_kopi_mentah
            INNER JOIN metode_proses m
                ON m.id_metode_proses = b.id_metode_proses
            WHERE b.is_aktif = TRUE
            ORDER BY b.nama_biji;
            """;

        var materials = new List<RawMaterial>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            materials.Add(new RawMaterial
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                TypeId = reader.GetInt32(2),
                TypeName = reader.GetString(3),
                ProcessingMethodId = reader.GetInt32(4),
                ProcessingMethodName = reader.GetString(5),
                Origin = reader.IsDBNull(6) ? null : reader.GetString(6),
                AltitudeMeters = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                Quality = reader.IsDBNull(8) ? null : reader.GetString(8),
                StockKilograms = reader.GetDecimal(9),
                PricePerKilogram = reader.GetDecimal(10),
                HarvestDate = reader.IsDBNull(11) ? null : reader.GetFieldValue<DateOnly>(11),
                IsActive = reader.GetBoolean(12)
            });
        }

        return materials;
    }

    public Task<IReadOnlyList<LookupItem>> GetTypesAsync(
        CancellationToken cancellationToken = default) =>
        GetLookupAsync(
            "SELECT id_jenis_biji_kopi_mentah, nama FROM jenis_biji_kopi_mentah ORDER BY nama;",
            cancellationToken);

    public Task<IReadOnlyList<LookupItem>> GetProcessingMethodsAsync(
        CancellationToken cancellationToken = default) =>
        GetLookupAsync(
            "SELECT id_metode_proses, nama FROM metode_proses ORDER BY nama;",
            cancellationToken);

    /// <summary>
    /// Creates a raw material. A new type or processing method name is added
    /// to its master table, and a positive initial stock is recorded in
    /// <c>riwayat_stok</c>, all in the same transaction.
    /// </summary>
    public async Task<int> CreateAsync(
        RawMaterialDraft draft,
        decimal initialStockKilograms,
        CancellationToken cancellationToken = default)
    {
        if (initialStockKilograms < 0)
            throw new ArgumentOutOfRangeException(
                nameof(initialStockKilograms),
                "Initial stock cannot be negative.");

        await using var connection =
            await PostgresConnectionFactory.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            cancellationToken);

        try
        {
            var (typeId, methodId) = await ResolveLookupsAsync(
                connection, transaction, draft, cancellationToken);

            await using var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = transaction;
            insertCommand.CommandText = """
                INSERT INTO biji_kopi_mentah
                    (id_jenis_biji_kopi_mentah, id_metode_proses, nama_biji,
                     asal_daerah, ketinggian_mdpl, tingkat_kualitas,
                     harga_per_kg, tanggal_panen, stok_kg)
                VALUES
                    ($1, $2, $3, $4, $5, $6, $7, $8, $9)
                RETURNING id_biji_mentah;
                """;
            AddDraftParameters(insertCommand, draft, typeId, methodId);
            insertCommand.Parameters.AddWithValue(initialStockKilograms);

            var id = (int)(await insertCommand.ExecuteScalarAsync(cancellationToken))!;

            if (initialStockKilograms > 0)
            {
                await InsertIncomingHistoryAsync(
                    connection, transaction, id, initialStockKilograms,
                    "Stok awal", cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return id;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Updates the descriptive fields of a raw material. Stock is not changed
    /// here; it only changes through <see cref="AddIncomingStockAsync"/> so
    /// every change has a history entry.
    /// </summary>
    public async Task UpdateAsync(
        int rawMaterialId,
        RawMaterialDraft draft,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            await PostgresConnectionFactory.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            cancellationToken);

        try
        {
            var (typeId, methodId) = await ResolveLookupsAsync(
                connection, transaction, draft, cancellationToken);

            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                UPDATE biji_kopi_mentah
                SET id_jenis_biji_kopi_mentah = $1,
                    id_metode_proses = $2,
                    nama_biji = $3,
                    asal_daerah = $4,
                    ketinggian_mdpl = $5,
                    tingkat_kualitas = $6,
                    harga_per_kg = $7,
                    tanggal_panen = $8
                WHERE id_biji_mentah = $9 AND is_aktif = TRUE;
                """;
            AddDraftParameters(command, draft, typeId, methodId);
            command.Parameters.AddWithValue(rawMaterialId);

            if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new InvalidOperationException(
                    "The selected raw material does not exist or is inactive.");

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>Hides a raw material from the active list without deleting its history.</summary>
    public async Task DeactivateAsync(
        int rawMaterialId,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            await PostgresConnectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE biji_kopi_mentah
            SET is_aktif = FALSE
            WHERE id_biji_mentah = $1 AND is_aktif = TRUE;
            """;
        command.Parameters.AddWithValue(rawMaterialId);

        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException(
                "The selected raw material does not exist or is already inactive.");
    }

    /// <summary>
    /// Permanently deletes a raw material and its own stock history, in one
    /// transaction. Refused with <see cref="RawMaterialInUseException"/> when a
    /// roasting batch uses it, since deleting it would break that batch.
    /// </summary>
    public async Task DeleteAsync(
        int rawMaterialId,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            await PostgresConnectionFactory.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            cancellationToken);

        try
        {
            // Lock the row so no batch can start using it before it is deleted.
            await using var lockCommand = connection.CreateCommand();
            lockCommand.Transaction = transaction;
            lockCommand.CommandText = """
                SELECT 1 FROM biji_kopi_mentah
                WHERE id_biji_mentah = $1 AND is_aktif = TRUE
                FOR UPDATE;
                """;
            lockCommand.Parameters.AddWithValue(rawMaterialId);
            if (await lockCommand.ExecuteScalarAsync(cancellationToken) is null)
                throw new InvalidOperationException(
                    "The selected raw material does not exist or is inactive.");

            await using var usageCommand = connection.CreateCommand();
            usageCommand.Transaction = transaction;
            usageCommand.CommandText =
                "SELECT COUNT(*) FROM batch_roasting WHERE id_biji_mentah = $1;";
            usageCommand.Parameters.AddWithValue(rawMaterialId);
            var batchCount = (long)(await usageCommand.ExecuteScalarAsync(cancellationToken))!;
            if (batchCount > 0)
                throw new RawMaterialInUseException(batchCount);

            await using var historyCommand = connection.CreateCommand();
            historyCommand.Transaction = transaction;
            historyCommand.CommandText =
                "DELETE FROM riwayat_stok WHERE id_biji_mentah = $1;";
            historyCommand.Parameters.AddWithValue(rawMaterialId);
            await historyCommand.ExecuteNonQueryAsync(cancellationToken);

            await using var deleteCommand = connection.CreateCommand();
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText =
                "DELETE FROM biji_kopi_mentah WHERE id_biji_mentah = $1;";
            deleteCommand.Parameters.AddWithValue(rawMaterialId);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task AddIncomingStockAsync(
        int rawMaterialId,
        decimal quantityKilograms,
        string? reference,
        CancellationToken cancellationToken = default)
    {
        if (quantityKilograms <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(quantityKilograms),
                "Quantity must be greater than zero.");

        await using var connection =
            await PostgresConnectionFactory.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            cancellationToken);

        try
        {
            await using var updateCommand = connection.CreateCommand();
            updateCommand.Transaction = transaction;
            updateCommand.CommandText = """
                UPDATE biji_kopi_mentah
                SET stok_kg = stok_kg + $1
                WHERE id_biji_mentah = $2 AND is_aktif = TRUE;
                """;
            updateCommand.Parameters.AddWithValue(quantityKilograms);
            updateCommand.Parameters.AddWithValue(rawMaterialId);

            if (await updateCommand.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new InvalidOperationException(
                    "The selected raw material does not exist or is inactive.");

            await InsertIncomingHistoryAsync(
                connection, transaction, rawMaterialId, quantityKilograms,
                reference, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<StockHistoryEntry>> GetHistoryAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            await PostgresConnectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                r.id_riwayat_stok,
                b.nama_biji,
                r.arah,
                r.jumlah,
                r.satuan,
                r.referensi,
                r.tanggal_riwayat
            FROM riwayat_stok r
            INNER JOIN biji_kopi_mentah b
                ON b.id_biji_mentah = r.id_biji_mentah
            WHERE r.id_biji_mentah IS NOT NULL
            ORDER BY r.tanggal_riwayat DESC, r.id_riwayat_stok DESC
            LIMIT $1;
            """;
        command.Parameters.AddWithValue(limit);

        var history = new List<StockHistoryEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            history.Add(new StockHistoryEntry
            {
                Id = reader.GetInt32(0),
                MaterialName = reader.GetString(1),
                Direction = reader.GetString(2),
                Quantity = reader.GetDecimal(3),
                Unit = reader.GetString(4),
                Reference = reader.IsDBNull(5) ? null : reader.GetString(5),
                OccurredAt = reader.GetDateTime(6)
            });
        }

        return history;
    }

    private static async Task<IReadOnlyList<LookupItem>> GetLookupAsync(
        string sql,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await PostgresConnectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        var items = new List<LookupItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            items.Add(new LookupItem(reader.GetInt32(0), reader.GetString(1)));

        return items;
    }

    /// <summary>Adds the draft fields as parameters $1–$8.</summary>
    private static void AddDraftParameters(
        NpgsqlCommand command,
        RawMaterialDraft draft,
        int typeId,
        int processingMethodId)
    {
        command.Parameters.AddWithValue(typeId);
        command.Parameters.AddWithValue(processingMethodId);
        command.Parameters.AddWithValue(draft.Name);
        command.Parameters.Add(Nullable(NpgsqlDbType.Varchar, draft.Origin));
        command.Parameters.Add(Nullable(NpgsqlDbType.Integer, draft.AltitudeMeters));
        command.Parameters.Add(Nullable(NpgsqlDbType.Varchar, draft.Quality));
        command.Parameters.AddWithValue(draft.PricePerKilogram);
        command.Parameters.Add(Nullable(NpgsqlDbType.Date, draft.HarvestDate));
    }

    private static async Task<(int TypeId, int ProcessingMethodId)> ResolveLookupsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        RawMaterialDraft draft,
        CancellationToken cancellationToken)
    {
        var typeId = await GetOrCreateLookupAsync(
            connection, transaction,
            """
            SELECT id_jenis_biji_kopi_mentah FROM jenis_biji_kopi_mentah
            WHERE lower(nama) = lower($1)
            ORDER BY id_jenis_biji_kopi_mentah
            LIMIT 1;
            """,
            """
            INSERT INTO jenis_biji_kopi_mentah (nama) VALUES ($1)
            RETURNING id_jenis_biji_kopi_mentah;
            """,
            draft.TypeName, cancellationToken);

        var methodId = await GetOrCreateLookupAsync(
            connection, transaction,
            """
            SELECT id_metode_proses FROM metode_proses
            WHERE lower(nama) = lower($1)
            ORDER BY id_metode_proses
            LIMIT 1;
            """,
            """
            INSERT INTO metode_proses (nama) VALUES ($1)
            RETURNING id_metode_proses;
            """,
            draft.ProcessingMethodName, cancellationToken);

        return (typeId, methodId);
    }

    /// <summary>Returns the id of the entry with this name (ignoring case), creating it if needed.</summary>
    private static async Task<int> GetOrCreateLookupAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string selectSql,
        string insertSql,
        string name,
        CancellationToken cancellationToken)
    {
        await using var selectCommand = connection.CreateCommand();
        selectCommand.Transaction = transaction;
        selectCommand.CommandText = selectSql;
        selectCommand.Parameters.AddWithValue(name);
        if (await selectCommand.ExecuteScalarAsync(cancellationToken) is int existingId)
            return existingId;

        await using var insertCommand = connection.CreateCommand();
        insertCommand.Transaction = transaction;
        insertCommand.CommandText = insertSql;
        insertCommand.Parameters.AddWithValue(name);
        return (int)(await insertCommand.ExecuteScalarAsync(cancellationToken))!;
    }

    private static NpgsqlParameter Nullable(NpgsqlDbType type, object? value) =>
        new() { NpgsqlDbType = type, Value = value ?? DBNull.Value };

    private static async Task InsertIncomingHistoryAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int rawMaterialId,
        decimal quantityKilograms,
        string? reference,
        CancellationToken cancellationToken)
    {
        await using var historyCommand = connection.CreateCommand();
        historyCommand.Transaction = transaction;
        historyCommand.CommandText = """
            INSERT INTO riwayat_stok
                (id_biji_mentah, kategori, arah, jumlah, satuan, referensi)
            VALUES
                ($1, 'BIJI_MENTAH', 'MASUK', $2, 'KG', $3);
            """;
        historyCommand.Parameters.AddWithValue(rawMaterialId);
        historyCommand.Parameters.AddWithValue(quantityKilograms);
        historyCommand.Parameters.Add(Nullable(NpgsqlDbType.Varchar, reference));
        await historyCommand.ExecuteNonQueryAsync(cancellationToken);
    }
}
