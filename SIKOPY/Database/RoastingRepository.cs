using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;
using SIKOPY.Models;

namespace SIKOPY.Database;

public sealed class RoastingRepository
{
    public async Task<IReadOnlyList<RoastingBatch>> GetBatchesAsync(
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            await PostgresConnectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                b.id_batch,
                b.kode_batch,
                b.id_biji_mentah,
                m.nama_biji,
                b.jumlah_biji_dipakai_gram,
                b.hasil_roasting_gram,
                b.level_roasting,
                b.tanggal_batch,
                b.catatan
            FROM batch_roasting b
            INNER JOIN biji_kopi_mentah m
                ON m.id_biji_mentah = b.id_biji_mentah
            WHERE b.is_aktif = TRUE
            ORDER BY b.tanggal_batch DESC, b.id_batch DESC
            LIMIT $1;
            """;
        command.Parameters.AddWithValue(limit);

        var batches = new List<RoastingBatch>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            batches.Add(new RoastingBatch
            {
                Id = reader.GetInt32(0),
                Code = reader.GetString(1),
                RawMaterialId = reader.GetInt32(2),
                MaterialName = reader.GetString(3),
                UsedGram = reader.GetDecimal(4),
                RoastedGram = reader.IsDBNull(5) ? null : reader.GetDecimal(5),
                RoastLevel = reader.IsDBNull(6) ? null : reader.GetString(6),
                BatchDate = reader.GetDateTime(7),
                Notes = reader.IsDBNull(8) ? null : reader.GetString(8)
            });
        }

        return batches;
    }

    /// <summary>
    /// Menyimpan satu batch. Dalam SATU transaksi: kurangi stok green bean,
    /// simpan batch, buat produk roasted, dan catat dua baris riwayat stok.
    /// Mengembalikan kode batch.
    /// </summary>
    public async Task<string> CreateAsync(
        RoastingBatchDraft draft,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var usedKg = decimal.Round(draft.UsedKilograms, 2);
        var usedGram = usedKg * 1000m;
        var roastedGram = decimal.Round(draft.RoastedGram, 2);
        var code = $"BR-{draft.BatchDate:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        var batchTime = draft.BatchDate.Date.Add(DateTime.Now.TimeOfDay);

        await using var connection =
            await PostgresConnectionFactory.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            cancellationToken);

        try
        {
            // 1. Kurangi stok green bean (hanya kalau stok cukup)
            string materialName;
            await using (var stockCommand = connection.CreateCommand())
            {
                stockCommand.Transaction = transaction;
                stockCommand.CommandText = """
                    UPDATE biji_kopi_mentah
                    SET stok_kg = stok_kg - $1
                    WHERE id_biji_mentah = $2
                      AND is_aktif = TRUE
                      AND stok_kg >= $1
                    RETURNING nama_biji;
                    """;
                stockCommand.Parameters.AddWithValue(usedKg);
                stockCommand.Parameters.AddWithValue(draft.RawMaterialId);

                if (await stockCommand.ExecuteScalarAsync(cancellationToken) is not string name)
                    throw new InvalidOperationException(
                        "Stok green bean tidak cukup, atau green bean sudah tidak aktif.");
                materialName = name;
            }

            // 2. Simpan batch
            int batchId;
            await using (var batchCommand = connection.CreateCommand())
            {
                batchCommand.Transaction = transaction;
                batchCommand.CommandText = """
                    INSERT INTO batch_roasting
                        (id_biji_mentah, id_pengguna, kode_batch,
                         jumlah_biji_dipakai_gram, hasil_roasting_gram,
                         level_roasting, tanggal_batch, catatan)
                    VALUES
                        ($1, $2, $3, $4, $5, $6, $7, $8)
                    RETURNING id_batch;
                    """;
                batchCommand.Parameters.AddWithValue(draft.RawMaterialId);
                batchCommand.Parameters.AddWithValue(userId);
                batchCommand.Parameters.AddWithValue(code);
                batchCommand.Parameters.AddWithValue(usedGram);
                batchCommand.Parameters.AddWithValue(roastedGram);
                batchCommand.Parameters.AddWithValue(draft.RoastLevel);
                batchCommand.Parameters.Add(Typed(NpgsqlDbType.Timestamp, batchTime));
                batchCommand.Parameters.Add(Typed(NpgsqlDbType.Text, draft.Notes));
                batchId = (int)(await batchCommand.ExecuteScalarAsync(cancellationToken))!;
            }

            // 3. Buat produk roast bean (harga diatur admin nanti)
            int roastedId;
            await using (var roastedCommand = connection.CreateCommand())
            {
                roastedCommand.Transaction = transaction;
                roastedCommand.CommandText = """
                    INSERT INTO biji_kopi_roasted
                        (id_batch, nama_produk, stok_gram, catatan)
                    VALUES
                        ($1, $2, $3, $4)
                    RETURNING id_roasted;
                    """;
                roastedCommand.Parameters.AddWithValue(batchId);
                roastedCommand.Parameters.AddWithValue($"{materialName} ({draft.RoastLevel})");
                roastedCommand.Parameters.AddWithValue(roastedGram);
                roastedCommand.Parameters.Add(Typed(NpgsqlDbType.Text, draft.Notes));
                roastedId = (int)(await roastedCommand.ExecuteScalarAsync(cancellationToken))!;
            }

            // 4. Riwayat: green bean KELUAR
            await InsertHistoryAsync(connection, transaction,
                rawMaterialId: draft.RawMaterialId, roastedId: null,
                category: "BIJI_MENTAH", direction: "KELUAR",
                quantity: usedKg, unit: "KG", reference: code, cancellationToken);

            // 5. Riwayat: roast bean MASUK
            await InsertHistoryAsync(connection, transaction,
                rawMaterialId: null, roastedId: roastedId,
                category: "BIJI_ROASTED", direction: "MASUK",
                quantity: roastedGram, unit: "GRAM", reference: code, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return code;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Mengubah satu batch. Dalam SATU transaksi: sesuaikan stok green bean dan
    /// stok roast bean sebesar selisihnya, catat selisih di riwayat stok, lalu
    /// simpan perubahan batch. Green bean tidak boleh diganti.
    /// </summary>
    public async Task UpdateAsync(
        int batchId,
        RoastingBatchDraft draft,
        CancellationToken cancellationToken = default)
    {
        var usedKg = decimal.Round(draft.UsedKilograms, 2);
        var usedGram = usedKg * 1000m;
        var roastedGram = decimal.Round(draft.RoastedGram, 2);

        await using var connection =
            await PostgresConnectionFactory.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            cancellationToken);

        try
        {
            // 0. Ambil data lama dan kunci baris batch
            int rawMaterialId;
            decimal oldUsedGram;
            decimal oldRoastedGram;
            DateTime oldBatchTime;
            string code;
            string materialName;
            int? roastedId;
            await using (var currentCommand = connection.CreateCommand())
            {
                currentCommand.Transaction = transaction;
                currentCommand.CommandText = """
                    SELECT
                        b.id_biji_mentah,
                        b.jumlah_biji_dipakai_gram,
                        b.hasil_roasting_gram,
                        b.tanggal_batch,
                        b.kode_batch,
                        m.nama_biji,
                        r.id_roasted
                    FROM batch_roasting b
                    INNER JOIN biji_kopi_mentah m
                        ON m.id_biji_mentah = b.id_biji_mentah
                    LEFT JOIN biji_kopi_roasted r
                        ON r.id_batch = b.id_batch
                    WHERE b.id_batch = $1
                      AND b.is_aktif = TRUE
                    ORDER BY r.id_roasted
                    LIMIT 1
                    FOR UPDATE OF b;
                    """;
                currentCommand.Parameters.AddWithValue(batchId);

                await using var reader = await currentCommand.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                    throw new InvalidOperationException(
                        "Batch tidak ditemukan atau sudah tidak aktif.");

                rawMaterialId = reader.GetInt32(0);
                oldUsedGram = reader.GetDecimal(1);
                oldRoastedGram = reader.IsDBNull(2) ? 0m : reader.GetDecimal(2);
                oldBatchTime = reader.GetDateTime(3);
                code = reader.GetString(4);
                materialName = reader.GetString(5);
                roastedId = reader.IsDBNull(6) ? null : reader.GetInt32(6);
            }

            if (rawMaterialId != draft.RawMaterialId)
                throw new InvalidOperationException(
                    "Green bean pada batch tidak boleh diganti.");

            // 1. Sesuaikan stok green bean sebesar selisih pemakaian
            var usedDeltaKg = usedKg - oldUsedGram / 1000m;
            if (usedDeltaKg != 0m)
            {
                await using (var stockCommand = connection.CreateCommand())
                {
                    stockCommand.Transaction = transaction;
                    stockCommand.CommandText = """
                        UPDATE biji_kopi_mentah
                        SET stok_kg = stok_kg - $1
                        WHERE id_biji_mentah = $2
                          AND stok_kg - $1 >= 0;
                        """;
                    stockCommand.Parameters.AddWithValue(usedDeltaKg);
                    stockCommand.Parameters.AddWithValue(rawMaterialId);

                    if (await stockCommand.ExecuteNonQueryAsync(cancellationToken) == 0)
                        throw new InvalidOperationException(
                            "Stok green bean tidak cukup untuk menambah pemakaian.");
                }

                await InsertHistoryAsync(connection, transaction,
                    rawMaterialId: rawMaterialId, roastedId: null,
                    category: "BIJI_MENTAH",
                    direction: usedDeltaKg > 0m ? "KELUAR" : "MASUK",
                    quantity: Math.Abs(usedDeltaKg), unit: "KG", reference: code,
                    cancellationToken);
            }

            // 2. Sesuaikan produk roast bean (stok, nama, catatan)
            var roastedDelta = roastedGram - oldRoastedGram;
            if (roastedId is int existingRoastedId)
            {
                await using (var roastedCommand = connection.CreateCommand())
                {
                    roastedCommand.Transaction = transaction;
                    roastedCommand.CommandText = """
                        UPDATE biji_kopi_roasted
                        SET stok_gram = stok_gram + $1,
                            nama_produk = $2,
                            catatan = $3
                        WHERE id_roasted = $4
                          AND stok_gram + $1 >= 0;
                        """;
                    roastedCommand.Parameters.AddWithValue(roastedDelta);
                    roastedCommand.Parameters.AddWithValue($"{materialName} ({draft.RoastLevel})");
                    roastedCommand.Parameters.Add(Typed(NpgsqlDbType.Text, draft.Notes));
                    roastedCommand.Parameters.AddWithValue(existingRoastedId);

                    if (await roastedCommand.ExecuteNonQueryAsync(cancellationToken) == 0)
                        throw new InvalidOperationException(
                            "Hasil roast tidak bisa dikurangi sebanyak itu karena sebagian roast bean sudah terpakai atau terjual.");
                }

                if (roastedDelta != 0m)
                {
                    await InsertHistoryAsync(connection, transaction,
                        rawMaterialId: null, roastedId: existingRoastedId,
                        category: "BIJI_ROASTED",
                        direction: roastedDelta > 0m ? "MASUK" : "KELUAR",
                        quantity: Math.Abs(roastedDelta), unit: "GRAM", reference: code,
                        cancellationToken);
                }
            }

            // 3. Simpan perubahan batch (jam lama dipertahankan)
            await using (var batchCommand = connection.CreateCommand())
            {
                batchCommand.Transaction = transaction;
                batchCommand.CommandText = """
                    UPDATE batch_roasting
                    SET jumlah_biji_dipakai_gram = $1,
                        hasil_roasting_gram = $2,
                        level_roasting = $3,
                        tanggal_batch = $4,
                        catatan = $5
                    WHERE id_batch = $6;
                    """;
                batchCommand.Parameters.AddWithValue(usedGram);
                batchCommand.Parameters.AddWithValue(roastedGram);
                batchCommand.Parameters.AddWithValue(draft.RoastLevel);
                batchCommand.Parameters.Add(Typed(NpgsqlDbType.Timestamp,
                    draft.BatchDate.Date.Add(oldBatchTime.TimeOfDay)));
                batchCommand.Parameters.Add(Typed(NpgsqlDbType.Text, draft.Notes));
                batchCommand.Parameters.AddWithValue(batchId);
                await batchCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static async Task InsertHistoryAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int? rawMaterialId,
        int? roastedId,
        string category,
        string direction,
        decimal quantity,
        string unit,
        string reference,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO riwayat_stok
                (id_biji_mentah, id_roasted, kategori, arah, jumlah, satuan, referensi)
            VALUES
                ($1, $2, $3, $4, $5, $6, $7);
            """;
        command.Parameters.Add(Typed(NpgsqlDbType.Integer, rawMaterialId));
        command.Parameters.Add(Typed(NpgsqlDbType.Integer, roastedId));
        command.Parameters.AddWithValue(category);
        command.Parameters.AddWithValue(direction);
        command.Parameters.AddWithValue(quantity);
        command.Parameters.AddWithValue(unit);
        command.Parameters.AddWithValue(reference);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static NpgsqlParameter Typed(NpgsqlDbType type, object? value) =>
        new() { NpgsqlDbType = type, Value = value ?? DBNull.Value };
}