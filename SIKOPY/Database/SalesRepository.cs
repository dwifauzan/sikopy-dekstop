using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NpgsqlTypes;
using SIKOPY.Models;

namespace SIKOPY.Database;

public sealed class SalesRepository
{
    /// <summary>
    /// Roast bean yang bisa dijual: produk dan batch aktif, harga sudah diatur.
    /// <c>RoastedGram</c> di sini berisi stok roast bean saat ini.
    /// </summary>
    public async Task<IReadOnlyList<(RoastingBatch Batch, string Origin, decimal PricePerGram)>> GetShopProductsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            await PostgresConnectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT b.id_batch, b.kode_batch, b.id_biji_mentah, r.nama_produk,
                   b.jumlah_biji_dipakai_gram, r.stok_gram, b.level_roasting,
                   b.tanggal_batch, b.catatan, m.asal_daerah, r.harga_per_gram
            FROM biji_kopi_roasted r
            INNER JOIN batch_roasting b ON b.id_batch = r.id_batch
            INNER JOIN biji_kopi_mentah m ON m.id_biji_mentah = b.id_biji_mentah
            WHERE r.is_aktif = TRUE AND b.is_aktif = TRUE AND r.harga_per_gram > 0
            ORDER BY b.tanggal_batch DESC, b.id_batch DESC;
            """;

        var products = new List<(RoastingBatch, string, decimal)>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var batch = new RoastingBatch
            {
                Id = reader.GetInt32(0),
                Code = reader.GetString(1),
                RawMaterialId = reader.GetInt32(2),
                MaterialName = reader.GetString(3),
                UsedGram = reader.GetDecimal(4),
                RoastedGram = reader.GetDecimal(5),
                RoastLevel = reader.IsDBNull(6) ? null : reader.GetString(6),
                BatchDate = reader.GetDateTime(7),
                Notes = reader.IsDBNull(8) ? null : reader.GetString(8)
            };
            products.Add((batch, reader.IsDBNull(9) ? "-" : reader.GetString(9), reader.GetDecimal(10)));
        }

        return products;
    }

    /// <summary>
    /// Membuat satu penjualan dalam SATU transaksi: kunci produk, cek stok dan harga
    /// dari database (bukan dari layar), simpan penjualan + detail, kurangi stok,
    /// dan catat riwayat. Mengembalikan nomor invoice.
    /// </summary>
    public async Task<string> CheckoutAsync(
        int userId,
        string? customerName,
        IReadOnlyList<(int BatchId, decimal Gram)> items,
        CancellationToken cancellationToken = default)
    {
        var wanted = items
            .GroupBy(item => item.BatchId)
            .ToDictionary(group => group.Key, group => decimal.Round(group.Sum(item => item.Gram), 2));
        if (wanted.Count == 0 || wanted.Values.Any(gram => gram <= 0m))
            throw new InvalidOperationException("Keranjang kosong atau jumlah gram tidak valid.");

        var invoice = $"INV-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

        await using var connection =
            await PostgresConnectionFactory.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var lines = new List<(int RoastedId, string Name, decimal Gram, decimal Price, decimal Subtotal)>();
            await using (var lockCommand = connection.CreateCommand())
            {
                lockCommand.Transaction = transaction;
                // Urut id supaya dua checkout bersamaan tidak saling deadlock.
                lockCommand.CommandText = """
                    SELECT r.id_batch, r.id_roasted, r.nama_produk, r.stok_gram, r.harga_per_gram
                    FROM biji_kopi_roasted r
                    INNER JOIN batch_roasting b ON b.id_batch = r.id_batch
                    WHERE r.id_batch = ANY($1) AND r.is_aktif = TRUE AND b.is_aktif = TRUE
                    ORDER BY r.id_roasted
                    FOR UPDATE OF r;
                    """;
                lockCommand.Parameters.AddWithValue(wanted.Keys.ToArray());

                var found = new HashSet<int>();
                await using var reader = await lockCommand.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var batchId = reader.GetInt32(0);
                    var name = reader.GetString(2);
                    var stock = reader.GetDecimal(3);
                    var price = reader.GetDecimal(4);
                    var gram = wanted[batchId];

                    if (price <= 0m)
                        throw new InvalidOperationException($"Harga {name} belum diatur.");
                    if (gram > stock)
                        throw new InvalidOperationException(
                            $"Stok {name} tinggal {stock:0.##} g, kurang dari {gram:0.##} g yang dipesan.");

                    found.Add(batchId);
                    lines.Add((reader.GetInt32(1), name, gram, price, decimal.Round(gram * price, 2)));
                }

                if (found.Count != wanted.Count)
                    throw new InvalidOperationException(
                        "Sebagian produk sudah tidak dijual. Muat ulang etalase lalu coba lagi.");
            }

            int saleId;
            await using (var saleCommand = connection.CreateCommand())
            {
                saleCommand.Transaction = transaction;
                saleCommand.CommandText = """
                    INSERT INTO penjualan (id_pengguna, nomor_invoice, nama_pelanggan, total_penjualan)
                    VALUES ($1, $2, $3, $4)
                    RETURNING id_penjualan;
                    """;
                saleCommand.Parameters.AddWithValue(userId);
                saleCommand.Parameters.AddWithValue(invoice);
                saleCommand.Parameters.Add(RoastingRepository.Typed(NpgsqlDbType.Varchar, customerName));
                saleCommand.Parameters.AddWithValue(lines.Sum(line => line.Subtotal));
                saleId = (int)(await saleCommand.ExecuteScalarAsync(cancellationToken))!;
            }

            foreach (var line in lines)
            {
                await using var lineCommand = connection.CreateCommand();
                lineCommand.Transaction = transaction;
                lineCommand.CommandText = """
                    WITH line AS (
                        INSERT INTO detail_penjualan
                            (id_penjualan, id_roasted, jumlah_gram, harga_per_gram, subtotal)
                        VALUES ($1, $2, $3, $4, $5)
                    )
                    UPDATE biji_kopi_roasted SET stok_gram = stok_gram - $3 WHERE id_roasted = $2;
                    """;
                lineCommand.Parameters.AddWithValue(saleId);
                lineCommand.Parameters.AddWithValue(line.RoastedId);
                lineCommand.Parameters.AddWithValue(line.Gram);
                lineCommand.Parameters.AddWithValue(line.Price);
                lineCommand.Parameters.AddWithValue(line.Subtotal);
                await lineCommand.ExecuteNonQueryAsync(cancellationToken);

                await RoastingRepository.InsertHistoryAsync(
                    connection, transaction, rawMaterialId: null, roastedId: line.RoastedId,
                    category: "BIJI_ROASTED", direction: "KELUAR",
                    quantity: line.Gram, unit: "GRAM", reference: invoice, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return invoice;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<SaleSummary>> GetSalesAsync(
        int limit = 200,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            await PostgresConnectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT s.id_penjualan, s.nomor_invoice,
                   COALESCE(s.nama_pelanggan, u.nama_lengkap),
                   s.total_penjualan, s.tanggal_penjualan,
                   STRING_AGG(r.nama_produk || ' ' || TRIM_SCALE(d.jumlah_gram) || ' g', ', '
                              ORDER BY d.id_detail_penjualan)
            FROM penjualan s
            INNER JOIN pengguna u ON u.id_pengguna = s.id_pengguna
            LEFT JOIN detail_penjualan d ON d.id_penjualan = s.id_penjualan
            LEFT JOIN biji_kopi_roasted r ON r.id_roasted = d.id_roasted
            WHERE s.is_aktif = TRUE
            GROUP BY s.id_penjualan, u.nama_lengkap
            ORDER BY s.tanggal_penjualan DESC, s.id_penjualan DESC
            LIMIT $1;
            """;
        command.Parameters.AddWithValue(limit);

        var sales = new List<SaleSummary>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            sales.Add(new SaleSummary
            {
                Id = reader.GetInt32(0),
                Invoice = reader.GetString(1),
                CustomerName = reader.GetString(2),
                Total = reader.GetDecimal(3),
                SoldAt = reader.GetDateTime(4),
                ItemsSummary = reader.IsDBNull(5) ? "-" : reader.GetString(5)
            });
        }

        return sales;
    }

    /// <summary>Riwayat stok green bean dan roast bean, terbaru dulu. Filter null = semua.</summary>
    public async Task<IReadOnlyList<StockHistoryEntry>> GetStockHistoryAsync(
        DateTime? from,
        DateTime? to,
        string? direction,
        string? search,
        int limit = 500,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            await PostgresConnectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT r.id_riwayat_stok, COALESCE(m.nama_biji, p.nama_produk, '-'),
                   r.arah, r.jumlah, r.satuan, r.referensi, r.tanggal_riwayat, r.kategori
            FROM riwayat_stok r
            LEFT JOIN biji_kopi_mentah m ON m.id_biji_mentah = r.id_biji_mentah
            LEFT JOIN biji_kopi_roasted p ON p.id_roasted = r.id_roasted
            WHERE ($1::date IS NULL OR r.tanggal_riwayat >= $1)
              AND ($2::date IS NULL OR r.tanggal_riwayat < $2 + 1)
              AND ($3::text IS NULL OR r.arah = $3)
              AND ($4::text IS NULL
                   OR COALESCE(m.nama_biji, p.nama_produk) ILIKE '%' || $4 || '%'
                   OR r.referensi ILIKE '%' || $4 || '%')
            ORDER BY r.tanggal_riwayat DESC, r.id_riwayat_stok DESC
            LIMIT $5;
            """;
        command.Parameters.Add(RoastingRepository.Typed(NpgsqlDbType.Date, from?.Date));
        command.Parameters.Add(RoastingRepository.Typed(NpgsqlDbType.Date, to?.Date));
        command.Parameters.Add(RoastingRepository.Typed(NpgsqlDbType.Text, direction));
        command.Parameters.Add(RoastingRepository.Typed(
            NpgsqlDbType.Text, string.IsNullOrWhiteSpace(search) ? null : search.Trim()));
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
                OccurredAt = reader.GetDateTime(6),
                Category = reader.GetString(7)
            });
        }

        return history;
    }
}
