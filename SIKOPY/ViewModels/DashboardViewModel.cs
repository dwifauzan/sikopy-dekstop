using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SIKOPY.Database;
using SIKOPY.Models;

namespace SIKOPY.ViewModels;

public sealed record LowStockItem(string Name, string StockDisplay);

/// <summary>Ringkasan stok, produksi minggu ini, dan penjualan.</summary>
public partial class DashboardViewModel : ViewModelBase
{
    private static readonly CultureInfo Indonesian = CultureInfo.GetCultureInfo("id-ID");

    private readonly DashboardRepository _dashboard = new();
    private readonly SalesRepository _sales = new();

    [ObservableProperty] public partial DashboardStats Stats { get; set; } = new();
    [ObservableProperty] public partial string? StatusMessage { get; set; }

    public ObservableCollection<LowStockItem> LowStock { get; } = new();
    public ObservableCollection<SaleSummary> RecentSales { get; } = new();

    public bool HasLowStock => LowStock.Count > 0;
    public bool HasRecentSales => RecentSales.Count > 0;
    public string LowStockTitle => $"Stok Menipis (di bawah {RawMaterial.LowStockThresholdKilograms:0} kg)";

    [RelayCommand]
    private async Task LoadAsync()
    {
        StatusMessage = null;
        try
        {
            Stats = await _dashboard.GetStatsAsync();

            LowStock.Clear();
            foreach (var (name, kg) in await _dashboard.GetLowStockAsync())
                LowStock.Add(new LowStockItem(name, $"{kg.ToString("N2", Indonesian)} kg"));

            RecentSales.Clear();
            foreach (var sale in await _sales.GetSalesAsync(5))
                RecentSales.Add(sale);
        }
        catch (Exception exception)
        {
            StatusMessage = $"Gagal memuat dashboard: {DatabaseErrorMessage.Describe(exception)}";
        }

        OnPropertyChanged(nameof(HasLowStock));
        OnPropertyChanged(nameof(HasRecentSales));
    }
}
