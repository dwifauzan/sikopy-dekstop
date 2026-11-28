using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SIKOPY.Models;

namespace SIKOPY.Database;

public sealed class DashboardRepository
{
    public async Task<DashboardStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection =
            await PostgresConnectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        // Minggu mulai Senin (date_trunc 'week'); susut = 1 - hasil / dipakai, berbobot per gram.
        command.CommandText = """
            WITH green AS (
                SELECT COALESCE(SUM(stok_kg), 0) AS kg, COUNT(*) AS n,
                       COUNT(*) FILTER (WHERE stok_kg < $1) AS low
                FROM biji_kopi_mentah WHERE is_aktif = TRUE
            ), roasted AS (
                SELECT COALESCE(SUM(r.stok_gram), 0) AS gram,
                       COUNT(*) FILTER (WHERE r.stok_gram > 0) AS in_stock,
                       COUNT(*) FILTER (WHERE r.harga_per_gram <= 0) AS unpriced
                FROM biji_kopi_roasted r
                INNER JOIN batch_roasting b ON b.id_batch = r.id_batch
                WHERE r.is_aktif = TRUE AND b.is_aktif = TRUE
            ), week AS (
                SELECT COUNT(*) AS n, COALESCE(SUM(jumlah_biji_dipakai_gram), 0) AS used,
                       1 - SUM(hasil_roasting_gram) FILTER (WHERE hasil_roasting_gram IS NOT NULL)
                         / NULLIF(SUM(jumlah_biji_dipakai_gram) FILTER (WHERE hasil_roasting_gram IS NOT NULL), 0) AS shrink
                FROM batch_roasting
                WHERE is_aktif = TRUE AND tanggal_batch >= DATE_TRUNC('week', LOCALTIMESTAMP)
            ), sales AS (
                SELECT COALESCE(SUM(total_penjualan) FILTER (WHERE tanggal_penjualan >= CURRENT_DATE), 0) AS today,
                       COUNT(*) FILTER (WHERE tanggal_penjualan >= CURRENT_DATE) AS today_n,
                       COALESCE(SUM(total_penjualan), 0) AS month,
                       COUNT(*) AS month_n
                FROM penjualan
                WHERE is_aktif = TRUE AND tanggal_penjualan >= DATE_TRUNC('month', LOCALTIMESTAMP)
            )
            SELECT green.kg, green.n, green.low, roasted.gram, roasted.in_stock, roasted.unpriced,
                   week.n, week.used, week.shrink, sales.today, sales.today_n, sales.month, sales.month_n
            FROM green, roasted, week, sales;
            """;
        command.Parameters.AddWithValue(RawMaterial.LowStockThresholdKilograms);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return new DashboardStats
        {
            GreenBeanKg = reader.GetDecimal(0),
            GreenBeanCount = (int)reader.GetInt64(1),
            LowStockCount = (int)reader.GetInt64(2),
            RoastedGram = reader.GetDecimal(3),
            RoastedInStockCount = (int)reader.GetInt64(4),
            UnpricedCount = (int)reader.GetInt64(5),
            WeekBatchCount = (int)reader.GetInt64(6),
            WeekUsedGram = reader.GetDecimal(7),
            WeekShrinkage = reader.IsDBNull(8) ? null : reader.GetDecimal(8),
            TodaySales = reader.GetDecimal(9),
            TodaySalesCount = (int)reader.GetInt64(10),
            MonthSales = reader.GetDecimal(11),
            MonthSalesCount = (int)reader.GetInt64(12)
        };
    }

    /// <summary>Green bean aktif di bawah ambang stok menipis, paling sedikit dulu.</summary>
    public async Task<IReadOnlyList<(string Name, decimal StockKg)>> GetLowStockAsync(
        int limit = 5,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            await PostgresConnectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT nama_biji, stok_kg FROM biji_kopi_mentah
            WHERE is_aktif = TRUE AND stok_kg < $1
            ORDER BY stok_kg, nama_biji
            LIMIT $2;
            """;
        command.Parameters.AddWithValue(RawMaterial.LowStockThresholdKilograms);
        command.Parameters.AddWithValue(limit);

        var items = new List<(string, decimal)>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            items.Add((reader.GetString(0), reader.GetDecimal(1)));
        return items;
    }
}
