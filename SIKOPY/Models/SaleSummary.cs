using System;
using System.Globalization;

namespace SIKOPY.Models;

/// <summary>Satu penjualan di daftar Riwayat.</summary>
public sealed class SaleSummary
{
    private static readonly CultureInfo Indonesian = CultureInfo.GetCultureInfo("id-ID");

    public int Id { get; init; }
    public string Invoice { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public decimal Total { get; init; }
    public DateTime SoldAt { get; init; }
    public string ItemsSummary { get; init; } = string.Empty;

    public string TotalDisplay => $"Rp {Total.ToString("N0", Indonesian)}";
    public string SoldAtDisplay => SoldAt.ToString("dd MMM yyyy, HH\\:mm", Indonesian);
}
