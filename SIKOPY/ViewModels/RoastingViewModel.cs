using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SIKOPY.Database;
using SIKOPY.Helpers;
using SIKOPY.Models;

namespace SIKOPY.ViewModels;

public partial class RoastingViewModel : ViewModelBase
{
    private const string AllLevels = "Semua level";
    private static readonly CultureInfo Indonesian = CultureInfo.GetCultureInfo("id-ID");

    private readonly RawMaterialRepository _rawMaterials = new();
    private readonly RoastingRepository _roasting = new();
    private readonly List<RoastingBatch> _allBatches = new();
    private RoastingBatch? _editingBatch;
    private bool _isSaving;

    public string Title => "Roasting";

    /// <summary>Logged-in user, recorded as the batch creator. Set by MainViewModel.</summary>
    public int? CurrentUserId { get; set; }

    public ObservableCollection<RawMaterial> GreenBeans { get; } = new();
    public ObservableCollection<RoastingBatch> Batches { get; } = new();

    public string[] RoastLevels { get; } = { "Light", "Medium", "Medium Dark", "Dark" };
    public string[] LevelFilters { get; } = { AllLevels, "Light", "Medium", "Medium Dark", "Dark" };

    // ---------- Statistik bulan ini ----------
    [ObservableProperty] public partial string MonthBatchCount { get; set; } = "0";
    [ObservableProperty] public partial string MonthGreenBeanText { get; set; } = "0";
    [ObservableProperty] public partial string MonthRoastedText { get; set; } = "0";

    // ---------- Pencarian dan filter ----------
    [ObservableProperty] public partial string SearchText { get; set; } = string.Empty;
    [ObservableProperty] public partial string SelectedLevelFilter { get; set; } = AllLevels;

    // ---------- Halaman ----------
    [ObservableProperty] public partial bool HasNoResults { get; set; } = true;
    [ObservableProperty] public partial string EmptyMessage { get; set; } = string.Empty;
    [ObservableProperty] public partial string StatusMessage { get; set; } = string.Empty;

    // ---------- Form ----------
    [ObservableProperty] public partial bool IsFormOpen { get; set; }
    [ObservableProperty] public partial bool IsEditing { get; set; }
    [ObservableProperty] public partial RawMaterial? SelectedGreenBean { get; set; }
    [ObservableProperty] public partial string GreenBeanUsedKg { get; set; } = string.Empty;
    [ObservableProperty] public partial string RoastedGram { get; set; } = string.Empty;
    [ObservableProperty] public partial string RoastLevel { get; set; } = "Medium";
    [ObservableProperty] public partial DateTime? BatchDate { get; set; } = DateTime.Today;
    [ObservableProperty] public partial string Notes { get; set; } = string.Empty;
    [ObservableProperty] public partial string FormError { get; set; } = string.Empty;

    // ---------- Detail ----------
    [ObservableProperty] public partial RoastingBatch? DetailBatch { get; set; }
    [ObservableProperty] public partial RoastingBatchDetail? Detail { get; set; }
    [ObservableProperty] public partial string DetailError { get; set; } = string.Empty;
    public bool IsDetailOpen => DetailBatch is not null;

    // ---------- Hapus ----------
    [ObservableProperty] public partial RoastingBatch? DeletionTarget { get; set; }
    [ObservableProperty] public partial string DeleteError { get; set; } = string.Empty;
    public bool IsDeleteDialogOpen => DeletionTarget is not null;

    partial void OnDetailBatchChanged(RoastingBatch? value) => OnPropertyChanged(nameof(IsDetailOpen));
    partial void OnDeletionTargetChanged(RoastingBatch? value) => OnPropertyChanged(nameof(IsDeleteDialogOpen));

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSelectedLevelFilterChanged(string value) => ApplyFilter();
    partial void OnGreenBeanUsedKgChanged(string value) => FormError = string.Empty;
    partial void OnRoastedGramChanged(string value) => FormError = string.Empty;

    partial void OnIsEditingChanged(bool value)
    {
        OnPropertyChanged(nameof(FormTitle));
        OnPropertyChanged(nameof(FormInfo));
    }

    public string FormTitle => IsEditing ? "Edit Batch Roasting" : "Tambah Batch Roasting";

    public string FormInfo => IsEditing
        ? "Catatan: Green bean tidak bisa diganti. Perubahan jumlah akan menyesuaikan stok green bean dan roast bean sebesar selisihnya."
        : "Catatan: Saat batch disimpan, stok green bean berkurang otomatis dan roast bean baru ditambahkan di Bahan Baku → Roast Bean.";

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            var batches = await _roasting.GetBatchesAsync(500);
            _allBatches.Clear();
            _allBatches.AddRange(batches);
            UpdateStats();
            ApplyFilter();
        }
        catch (Exception exception)
        {
            StatusMessage = $"Gagal memuat batch: {DatabaseErrorMessage.Describe(exception)}";
        }
    }

    private void UpdateStats()
    {
        var now = DateTime.Now;
        var thisMonth = _allBatches
            .Where(b => b.BatchDate.Year == now.Year && b.BatchDate.Month == now.Month)
            .ToList();

        MonthBatchCount = thisMonth.Count.ToString(Indonesian);
        MonthGreenBeanText = (thisMonth.Sum(b => b.UsedGram) / 1000m).ToString("0.##", Indonesian);
        MonthRoastedText = (thisMonth.Sum(b => b.RoastedGram ?? 0m) / 1000m).ToString("0.##", Indonesian);
    }

    private void ApplyFilter()
    {
        var search = SearchText.Trim();

        var filtered = _allBatches.Where(batch =>
            (search.Length == 0 ||
             batch.Code.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             batch.MaterialName.Contains(search, StringComparison.OrdinalIgnoreCase)) &&
            (SelectedLevelFilter == AllLevels ||
             string.Equals(batch.RoastLevel, SelectedLevelFilter, StringComparison.OrdinalIgnoreCase)));

        Batches.Clear();
        foreach (var batch in filtered)
            Batches.Add(batch);

        HasNoResults = Batches.Count == 0;
        EmptyMessage = _allBatches.Count == 0
            ? "Belum ada batch roasting. Klik \"Tambah Batch\" untuk memulai."
            : "Tidak ada batch yang cocok dengan pencarian atau filter.";
    }

    private async Task<bool> LoadGreenBeansAsync()
    {
        try
        {
            var materials = await _rawMaterials.GetActiveAsync();
            GreenBeans.Clear();
            foreach (var material in materials)
                GreenBeans.Add(material);
            return true;
        }
        catch (Exception exception)
        {
            StatusMessage = $"Gagal memuat green bean: {DatabaseErrorMessage.Describe(exception)}";
            return false;
        }
    }

    [RelayCommand]
    private async Task OpenFormAsync()
    {
        if (!await LoadGreenBeansAsync())
            return;

        _editingBatch = null;
        IsEditing = false;
        StatusMessage = string.Empty;
        SelectedGreenBean = GreenBeans.FirstOrDefault();
        GreenBeanUsedKg = string.Empty;
        RoastedGram = string.Empty;
        RoastLevel = "Medium";
        BatchDate = DateTime.Today;
        Notes = string.Empty;
        FormError = string.Empty;
        IsFormOpen = true;
    }

    [RelayCommand]
    private async Task EditBatchAsync(RoastingBatch? batch)
    {
        if (batch is null || !await LoadGreenBeansAsync())
            return;

        // Green bean yang sudah nonaktif tidak ada di daftar; tampilkan saja namanya
        var material = GreenBeans.FirstOrDefault(m => m.Id == batch.RawMaterialId);
        if (material is null)
        {
            material = new RawMaterial { Id = batch.RawMaterialId, Name = batch.MaterialName };
            GreenBeans.Add(material);
        }

        _editingBatch = batch;
        IsEditing = true;
        StatusMessage = string.Empty;
        SelectedGreenBean = material;
        GreenBeanUsedKg = (batch.UsedGram / 1000m).ToString("0.##", Indonesian);
        RoastedGram = (batch.RoastedGram ?? 0m).ToString("0.##", Indonesian);
        RoastLevel = RoastLevels.Contains(batch.RoastLevel) ? batch.RoastLevel! : "Medium";
        BatchDate = batch.BatchDate.Date;
        Notes = batch.Notes ?? string.Empty;
        FormError = string.Empty;
        IsFormOpen = true;
    }

    [RelayCommand]
    private void CancelForm()
    {
        IsFormOpen = false;
        _editingBatch = null;
        IsEditing = false;
    }

    [RelayCommand]
    private async Task SaveFormAsync()
    {
        if (_isSaving)
            return;

        if (SelectedGreenBean is not { } material)
        {
            FormError = "Pilih green bean dulu.";
            return;
        }

        if (!NumberInput.TryParseDecimal(GreenBeanUsedKg, out var usedKg) || usedKg <= 0)
        {
            FormError = "Green bean dipakai harus berupa angka lebih dari nol, contoh: 4,5.";
            return;
        }

        // Saat edit, green bean yang sudah dipakai batch ini ikut dihitung tersedia
        var editing = _editingBatch;
        var previousKg = editing is null ? 0m : editing.UsedGram / 1000m;
        var availableKg = material.StockKilograms + previousKg;
        if (usedKg > availableKg)
        {
            FormError = $"Stok tidak cukup. Tersedia {availableKg.ToString("0.##", Indonesian)} kg.";
            return;
        }

        if (!NumberInput.TryParseDecimal(RoastedGram, out var roastedGram) || roastedGram <= 0)
        {
            FormError = "Hasil roast harus berupa angka lebih dari nol, contoh: 3360.";
            return;
        }

        if (roastedGram > usedKg * 1000m)
        {
            FormError = "Hasil roast tidak boleh lebih besar dari green bean yang dipakai.";
            return;
        }

        if (usedKg * 1000m > 99_999_999m)
        {
            FormError = "Jumlah green bean terlalu besar.";
            return;
        }

        _isSaving = true;
        try
        {
            var draft = new RoastingBatchDraft(
                material.Id,
                usedKg,
                roastedGram,
                RoastLevel,
                BatchDate ?? DateTime.Today,
                string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim());

            if (editing is null)
            {
                if (CurrentUserId is not { } userId)
                {
                    FormError = "Sesi login berakhir. Silakan masuk lagi.";
                    return;
                }

                var code = await _roasting.CreateAsync(draft, userId);
                StatusMessage = $"Batch {code} berhasil disimpan.";
            }
            else
            {
                await _roasting.UpdateAsync(editing.Id, draft);
                StatusMessage = $"Batch {editing.Code} berhasil diperbarui.";
            }

            IsFormOpen = false;
            _editingBatch = null;
            IsEditing = false;
            await LoadAsync();
        }
        catch (Exception exception)
        {
            FormError = $"Gagal menyimpan: {DatabaseErrorMessage.Describe(exception)}";
        }
        finally
        {
            _isSaving = false;
        }
    }

    [RelayCommand]
    private async Task OpenDetailAsync(RoastingBatch? batch)
    {
        if (batch is null)
            return;

        Detail = null;
        DetailError = string.Empty;
        DetailBatch = batch;
        try
        {
            Detail = await _roasting.GetDetailAsync(batch.Id);
        }
        catch (Exception exception)
        {
            DetailError = $"Gagal memuat detail: {DatabaseErrorMessage.Describe(exception)}";
        }
    }

    [RelayCommand]
    private void CloseDetail() => DetailBatch = null;

    [RelayCommand]
    private void RequestDelete(RoastingBatch? batch)
    {
        DeleteError = string.Empty;
        DeletionTarget = batch;
    }

    [RelayCommand]
    private void CancelDelete() => DeletionTarget = null;

    [RelayCommand]
    private async Task ConfirmDeleteAsync()
    {
        if (DeletionTarget is not { } batch || _isSaving)
            return;

        _isSaving = true;
        try
        {
            await _roasting.DeactivateAsync(batch.Id);
            DeletionTarget = null;
            StatusMessage = $"Batch {batch.Code} dihapus. Stok green bean dikembalikan.";
            await LoadAsync();
        }
        catch (Exception exception)
        {
            // Dialog tetap terbuka supaya alasannya terbaca
            DeleteError = $"Gagal menghapus: {DatabaseErrorMessage.Describe(exception)}";
        }
        finally
        {
            _isSaving = false;
        }
    }
}
