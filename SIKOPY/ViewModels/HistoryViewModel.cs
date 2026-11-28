using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SIKOPY.Database;
using SIKOPY.Models;

namespace SIKOPY.ViewModels;

/// <summary>Halaman Riwayat: semua mutasi stok dan daftar penjualan.</summary>
public partial class HistoryViewModel : ViewModelBase
{
    private const string AllDirections = "Semua arah";
    private static readonly CultureInfo Indonesian = CultureInfo.GetCultureInfo("id-ID");

    private readonly SalesRepository _sales = new();

    public ObservableCollection<StockHistoryEntry> StockHistory { get; } = new();
    public ObservableCollection<SaleSummary> Sales { get; } = new();

    public string[] DirectionOptions { get; } = { AllDirections, "MASUK", "KELUAR" };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStockTab))]
    public partial bool IsSalesTab { get; set; }

    public bool IsStockTab => !IsSalesTab;

    [ObservableProperty] public partial string SearchText { get; set; } = string.Empty;
    [ObservableProperty] public partial string SelectedDirection { get; set; } = AllDirections;
    [ObservableProperty] public partial DateTime? FromDate { get; set; }
    [ObservableProperty] public partial DateTime? ToDate { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    public partial string? StatusMessage { get; set; }

    [ObservableProperty] public partial string StockCountText { get; set; } = "0 transaksi";
    [ObservableProperty] public partial string SalesSummaryText { get; set; } = "0 penjualan";

    public bool HasStatus => !string.IsNullOrWhiteSpace(StatusMessage);
    public bool HasStockHistory => StockHistory.Count > 0;
    public bool HasSales => Sales.Count > 0;

    [RelayCommand]
    private async Task LoadAsync()
    {
        StatusMessage = null;
        try
        {
            var history = await _sales.GetStockHistoryAsync(
                FromDate, ToDate,
                SelectedDirection == AllDirections ? null : SelectedDirection,
                SearchText);
            StockHistory.Clear();
            foreach (var entry in history)
                StockHistory.Add(entry);
            StockCountText = history.Count >= 500 ? "500 transaksi terbaru" : $"{history.Count} transaksi";

            var sales = await _sales.GetSalesAsync();
            Sales.Clear();
            decimal total = 0m;
            foreach (var sale in sales)
            {
                Sales.Add(sale);
                total += sale.Total;
            }
            SalesSummaryText = $"{sales.Count} penjualan · Rp {total.ToString("N0", Indonesian)}";
        }
        catch (Exception exception)
        {
            StatusMessage = $"Gagal memuat riwayat: {DatabaseErrorMessage.Describe(exception)}";
        }

        OnPropertyChanged(nameof(HasStockHistory));
        OnPropertyChanged(nameof(HasSales));
    }

    [RelayCommand]
    private async Task ResetFiltersAsync()
    {
        SearchText = string.Empty;
        SelectedDirection = AllDirections;
        FromDate = null;
        ToDate = null;
        await LoadAsync();
    }

    [RelayCommand]
    private void ShowStockTab() => IsSalesTab = false;

    [RelayCommand]
    private void ShowSalesTab() => IsSalesTab = true;
}
