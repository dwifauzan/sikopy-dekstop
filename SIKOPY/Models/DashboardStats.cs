using System.Globalization;

namespace SIKOPY.Models;

/// <summary>Angka ringkasan di Dashboard.</summary>
public sealed class DashboardStats
{
    private static readonly CultureInfo Indonesian = CultureInfo.GetCultureInfo("id-ID");

    public decimal GreenBeanKg { get; init; }
    public int GreenBeanCount { get; init; }
    public int LowStockCount { get; init; }
    public decimal RoastedGram { get; init; }
    public int RoastedInStockCount { get; init; }
    public int UnpricedCount { get; init; }
    public int WeekBatchCount { get; init; }
    public decimal WeekUsedGram { get; init; }
    /// <summary>Susut rata-rata minggu ini (0..1), null bila belum ada batch.</summary>
    public decimal? WeekShrinkage { get; init; }
    public decimal TodaySales { get; init; }
    public int TodaySalesCount { get; init; }
    public decimal MonthSales { get; init; }
    public int MonthSalesCount { get; init; }

    public string GreenBeanDisplay => $"{GreenBeanKg.ToString("N2", Indonesian)} kg";
    public string GreenBeanCaption => LowStockCount > 0
        ? $"{GreenBeanCount} jenis · {LowStockCount} stok menipis"
        : $"{GreenBeanCount} jenis aktif";
    public string RoastedDisplay => $"{(RoastedGram / 1000m).ToString("N2", Indonesian)} kg";
    public string RoastedCaption => UnpricedCount > 0
        ? $"{RoastedInStockCount} produk tersedia · {UnpricedCount} belum ada harga"
        : $"{RoastedInStockCount} produk tersedia";
    public string WeekBatchDisplay => WeekBatchCount.ToString(Indonesian);
    public string WeekBatchCaption => $"{(WeekUsedGram / 1000m).ToString("N2", Indonesian)} kg green bean dipakai";
    public string WeekShrinkageDisplay => WeekShrinkage is { } value
        ? $"{(value * 100m).ToString("0.0", Indonesian)}%"
        : "-";
    public string TodaySalesDisplay => $"Rp {TodaySales.ToString("N0", Indonesian)}";
    public string TodaySalesCaption => $"{TodaySalesCount} pesanan hari ini";
    public string MonthSalesDisplay => $"Rp {MonthSales.ToString("N0", Indonesian)}";
    public string MonthSalesCaption => $"{MonthSalesCount} pesanan bulan ini";
}
