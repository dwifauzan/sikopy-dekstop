using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SIKOPY.Database;
using SIKOPY.Helpers;
using SIKOPY.Models;

namespace SIKOPY.ViewModels;

public partial class RawMaterialsViewModel : ViewModelBase
{
    private const int PageSize = 10;
    private const int HistoryLimit = 10;
    private const decimal MaxStockKilograms = 99_999_999.99m;
    private const decimal MaxPricePerKilogram = 9_999_999_999.99m;

    private static readonly LookupItem AllTypes = new(0, "Semua jenis");
    private static readonly LookupItem AllProcesses = new(0, "Semua proses");
    private static readonly CultureInfo Indonesian = CultureInfo.GetCultureInfo("id-ID");

    private readonly RawMaterialRepository _repository = new();
    private readonly List<RawMaterial> _materials = new();
    private List<RawMaterial> _filteredMaterials = new();
    private int? _editingMaterialId;
    private bool _isRefreshingFilters;

    public RawMaterialsViewModel()
    {
        SelectedTypeFilter = AllTypes;
        SelectedProcessFilter = AllProcesses;
        _ = LoadAsync();
    }

    public ObservableCollection<RawMaterialRowViewModel> PageRows { get; } = new();
    public ObservableCollection<PageItem> Pages { get; } = new();
    public ObservableCollection<StockHistoryEntry> StockHistory { get; } = new();
    public ObservableCollection<LookupItem> Types { get; } = new();
    public ObservableCollection<LookupItem> ProcessingMethods { get; } = new();
    public ObservableCollection<LookupItem> TypeFilterOptions { get; } = new() { AllTypes };
    public ObservableCollection<LookupItem> ProcessFilterOptions { get; } = new() { AllProcesses };

    // ---------- List, search and filters ----------

    [ObservableProperty] public partial bool IsLoading { get; set; }
    [ObservableProperty] public partial string SearchText { get; set; } = string.Empty;
    [ObservableProperty] public partial LookupItem? SelectedTypeFilter { get; set; }
    [ObservableProperty] public partial LookupItem? SelectedProcessFilter { get; set; }
    [ObservableProperty] public partial bool ShowLowStockOnly { get; set; }
    [ObservableProperty] public partial string TotalCountText { get; set; } = "0 jenis biji";
    [ObservableProperty] public partial string ResultSummary { get; set; } = string.Empty;
    [ObservableProperty] public partial bool HasMaterials { get; set; }
    [ObservableProperty] public partial bool HasNoResults { get; set; }
    [ObservableProperty] public partial string EmptyMessage { get; set; } = string.Empty;
    [ObservableProperty] public partial bool HasHistory { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand), nameof(NextPageCommand))]
    public partial int CurrentPage { get; set; } = 1;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand), nameof(NextPageCommand))]
    public partial int PageCount { get; set; } = 1;

    // ---------- Status banner ----------

    [ObservableProperty] public partial string StatusMessage { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsStatusError { get; set; }
    public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);

    // ---------- Add / edit green bean form ----------

    [ObservableProperty] public partial bool IsFormOpen { get; set; }
    [ObservableProperty] public partial bool IsEditing { get; set; }
    [ObservableProperty] public partial string FormTitle { get; set; } = string.Empty;
    [ObservableProperty] public partial string FormName { get; set; } = string.Empty;
    [ObservableProperty] public partial string FormTypeName { get; set; } = string.Empty;
    [ObservableProperty] public partial string FormProcessingMethodName { get; set; } = string.Empty;
    [ObservableProperty] public partial LookupItem? SelectedFormType { get; set; }
    [ObservableProperty] public partial bool HasTypes { get; set; }
    [ObservableProperty] public partial bool HasProcessingMethods { get; set; }
    [ObservableProperty] public partial LookupItem? SelectedFormProcessingMethod { get; set; }
    [ObservableProperty] public partial string FormOrigin { get; set; } = string.Empty;
    [ObservableProperty] public partial string FormAltitude { get; set; } = string.Empty;
    [ObservableProperty] public partial string FormGrade { get; set; } = string.Empty;
    [ObservableProperty] public partial string FormPrice { get; set; } = string.Empty;
    [ObservableProperty] public partial DateTime? FormHarvestDate { get; set; }
    [ObservableProperty] public partial string FormInitialStock { get; set; } = string.Empty;
    [ObservableProperty] public partial string FormError { get; set; } = string.Empty;

    // ---------- Incoming stock dialog ----------

    [ObservableProperty] public partial RawMaterial? StockTarget { get; set; }
    [ObservableProperty] public partial string StockQuantity { get; set; } = string.Empty;
    [ObservableProperty] public partial string StockReference { get; set; } = string.Empty;
    [ObservableProperty] public partial string StockError { get; set; } = string.Empty;
    public bool IsStockDialogOpen => StockTarget is not null;

    // ---------- Deactivate confirmation ----------

    [ObservableProperty] public partial RawMaterial? DeactivationTarget { get; set; }
    public bool IsDeactivateDialogOpen => DeactivationTarget is not null;

    partial void OnStatusMessageChanged(string value) => OnPropertyChanged(nameof(HasStatus));
    partial void OnStockTargetChanged(RawMaterial? value) => OnPropertyChanged(nameof(IsStockDialogOpen));
    partial void OnDeactivationTargetChanged(RawMaterial? value) => OnPropertyChanged(nameof(IsDeactivateDialogOpen));

    partial void OnStockQuantityChanged(string value) => StockError = string.Empty;
    partial void OnFormNameChanged(string value) => FormError = string.Empty;
    partial void OnFormPriceChanged(string value) => FormError = string.Empty;
    partial void OnSelectedFormTypeChanged(LookupItem? value)
    {
        if (value is not null)
            FormTypeName = value.Name;
    }

    partial void OnSelectedFormProcessingMethodChanged(LookupItem? value)
    {
        if (value is not null)
            FormProcessingMethodName = value.Name;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnShowLowStockOnlyChanged(bool value) => ApplyFilter();
    partial void OnSelectedTypeFilterChanged(LookupItem? value) => ApplyFilter();
    partial void OnSelectedProcessFilterChanged(LookupItem? value) => ApplyFilter();

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        try
        {
            var types = await _repository.GetTypesAsync(cancellationToken);
            var methods = await _repository.GetProcessingMethodsAsync(cancellationToken);
            var materials = await _repository.GetActiveAsync(cancellationToken);
            var history = await _repository.GetHistoryAsync(HistoryLimit, cancellationToken);

            ReplaceLookups(types, methods);
            SyncFormLookupSelections();

            _materials.Clear();
            _materials.AddRange(materials);
            TotalCountText = $"{_materials.Count} jenis biji";
            HasMaterials = _materials.Count > 0;
            ApplyFilter(resetPage: false);

            StockHistory.Clear();
            foreach (var entry in history)
                StockHistory.Add(entry);
            HasHistory = StockHistory.Count > 0;
        }
        catch (Exception exception)
        {
            ShowError($"Gagal memuat bahan baku: {DatabaseErrorMessage.Describe(exception)}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ClearFilters()
    {
        _isRefreshingFilters = true;
        SearchText = string.Empty;
        SelectedTypeFilter = AllTypes;
        SelectedProcessFilter = AllProcesses;
        ShowLowStockOnly = false;
        _isRefreshingFilters = false;
        ApplyFilter();
    }

    [RelayCommand(CanExecute = nameof(CanGoToPreviousPage))]
    private void PreviousPage() => GoToPage(CurrentPage - 1);

    private bool CanGoToPreviousPage() => CurrentPage > 1;

    [RelayCommand(CanExecute = nameof(CanGoToNextPage))]
    private void NextPage() => GoToPage(CurrentPage + 1);

    private bool CanGoToNextPage() => CurrentPage < PageCount;

    [RelayCommand]
    private void GoToPage(int page)
    {
        CurrentPage = Math.Clamp(page, 1, PageCount);
        RefreshPage();
    }

    [RelayCommand]
    private void DismissStatus() => StatusMessage = string.Empty;

    // ---------- Add / edit ----------

    [RelayCommand]
    private async Task OpenCreateFormAsync(CancellationToken cancellationToken = default)
    {
        if (!await RefreshLookupsAsync(cancellationToken))
            return;

        _editingMaterialId = null;
        IsEditing = false;
        FormTitle = "Tambah Green Bean";
        FormName = string.Empty;
        FormTypeName = string.Empty;
        FormProcessingMethodName = string.Empty;
        SelectedFormType = null;
        SelectedFormProcessingMethod = null;
        FormOrigin = string.Empty;
        FormAltitude = string.Empty;
        FormGrade = string.Empty;
        FormPrice = string.Empty;
        FormHarvestDate = null;
        FormInitialStock = string.Empty;
        FormError = string.Empty;
        IsFormOpen = true;
    }

    private async Task OpenEditFormAsync(RawMaterial material)
    {
        if (!await RefreshLookupsAsync())
            return;

        _editingMaterialId = material.Id;
        IsEditing = true;
        FormTitle = "Ubah Green Bean";
        FormName = material.Name;
        FormTypeName = material.TypeName;
        FormProcessingMethodName = material.ProcessingMethodName;
        SelectedFormType = Types.FirstOrDefault(item =>
            string.Equals(item.Name, material.TypeName, StringComparison.OrdinalIgnoreCase));
        SelectedFormProcessingMethod = ProcessingMethods.FirstOrDefault(item =>
            string.Equals(item.Name, material.ProcessingMethodName, StringComparison.OrdinalIgnoreCase));
        FormOrigin = material.Origin ?? string.Empty;
        FormAltitude = material.AltitudeMeters?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        FormGrade = material.Quality ?? string.Empty;
        FormPrice = material.PricePerKilogram.ToString("N0", Indonesian);
        FormHarvestDate = material.HarvestDate?.ToDateTime(TimeOnly.MinValue);
        FormInitialStock = string.Empty;
        FormError = string.Empty;
        IsFormOpen = true;
    }

    [RelayCommand]
    private void CancelForm() => IsFormOpen = false;

    [RelayCommand]
    private async Task SaveFormAsync(CancellationToken cancellationToken = default)
    {
        if (!TryBuildDraft(out var draft, out var initialStock, out var error))
        {
            FormError = error;
            return;
        }

        try
        {
            if (_editingMaterialId is { } id)
            {
                await _repository.UpdateAsync(id, draft, cancellationToken);
                ShowSuccess($"{draft.Name} berhasil diperbarui.");
            }
            else
            {
                await _repository.CreateAsync(draft, initialStock, cancellationToken);
                ShowSuccess($"{draft.Name} berhasil ditambahkan.");
            }

            FormError = string.Empty;
            IsFormOpen = false;
            await LoadAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            FormError = $"Gagal menyimpan: {DatabaseErrorMessage.Describe(exception)}";
        }
    }

    private bool TryBuildDraft(out RawMaterialDraft draft, out decimal initialStock, out string error)
    {
        draft = null!;
        initialStock = 0;

        var name = FormName.Trim();
        var origin = FormOrigin.Trim();
        var grade = FormGrade.Trim();
        var typeName = FormTypeName.Trim();
        var methodName = FormProcessingMethodName.Trim();

        if (name.Length == 0)
            return Fail("Nama green bean wajib diisi.", out error);
        if (name.Length > 100)
            return Fail("Nama green bean maksimal 100 karakter.", out error);
        if (typeName.Length == 0)
            return Fail("Pilih atau ketik jenis biji.", out error);
        if (typeName.Length > 100)
            return Fail("Jenis biji maksimal 100 karakter.", out error);
        if (methodName.Length == 0)
            return Fail("Pilih atau ketik metode proses.", out error);
        if (methodName.Length > 100)
            return Fail("Metode proses maksimal 100 karakter.", out error);
        if (origin.Length > 100)
            return Fail("Asal daerah maksimal 100 karakter.", out error);
        if (grade.Length > 50)
            return Fail("Grade maksimal 50 karakter.", out error);

        int? altitude = null;
        if (!string.IsNullOrWhiteSpace(FormAltitude))
        {
            if (!NumberInput.TryParseInteger(FormAltitude, out var parsedAltitude) || parsedAltitude > 9000)
                return Fail("Ketinggian harus berupa angka 0–9000 mdpl.", out error);
            altitude = parsedAltitude;
        }

        if (!NumberInput.TryParseRupiah(FormPrice, out var price) || price > MaxPricePerKilogram)
            return Fail("Harga per kg wajib diisi dengan angka, contoh: 200.000.", out error);

        if (!IsEditing && !string.IsNullOrWhiteSpace(FormInitialStock))
        {
            if (!NumberInput.TryParseDecimal(FormInitialStock, out initialStock) || initialStock > MaxStockKilograms)
                return Fail("Stok awal harus berupa angka, contoh: 25,5.", out error);
        }

        draft = new RawMaterialDraft(
            name,
            typeName,
            methodName,
            origin.Length == 0 ? null : origin,
            altitude,
            grade.Length == 0 ? null : grade,
            decimal.Round(price, 2),
            FormHarvestDate is { } harvest ? DateOnly.FromDateTime(harvest) : null);
        initialStock = decimal.Round(initialStock, 2);
        error = string.Empty;
        return true;
    }

    private static bool Fail(string message, out string error)
    {
        error = message;
        return false;
    }

    // ---------- Incoming stock ----------

    private void OpenStockDialog(RawMaterial material)
    {
        StockQuantity = string.Empty;
        StockReference = string.Empty;
        StockError = string.Empty;
        StockTarget = material;
    }

    [RelayCommand]
    private void CancelStock() => StockTarget = null;

    [RelayCommand]
    private async Task SaveStockAsync(CancellationToken cancellationToken = default)
    {
        if (StockTarget is not { } material)
            return;

        if (!NumberInput.TryParseDecimal(StockQuantity, out var quantity) ||
            quantity <= 0 ||
            material.StockKilograms + quantity > MaxStockKilograms)
        {
            StockError = "Masukkan jumlah lebih dari nol, contoh: 25,5.";
            return;
        }

        var reference = StockReference.Trim();
        if (reference.Length > 100)
        {
            StockError = "Referensi maksimal 100 karakter.";
            return;
        }

        quantity = decimal.Round(quantity, 2);
        try
        {
            await _repository.AddIncomingStockAsync(
                material.Id,
                quantity,
                reference.Length == 0 ? null : reference,
                cancellationToken);

            StockTarget = null;
            ShowSuccess($"Stok {material.Name} bertambah {quantity.ToString("0.##", Indonesian)} kg.");
            await LoadAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            StockError = $"Gagal menambah stok: {DatabaseErrorMessage.Describe(exception)}";
        }
    }

    // ---------- Deactivate ----------

    private void RequestDeactivation(RawMaterial material) => DeactivationTarget = material;

    [RelayCommand]
    private void CancelDeactivate() => DeactivationTarget = null;

    [RelayCommand]
    private async Task ConfirmDeactivateAsync(CancellationToken cancellationToken = default)
    {
        if (DeactivationTarget is not { } material)
            return;

        try
        {
            await _repository.DeactivateAsync(material.Id, cancellationToken);
            DeactivationTarget = null;
            ShowSuccess($"{material.Name} dinonaktifkan.");
            await LoadAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            DeactivationTarget = null;
            ShowError($"Gagal menonaktifkan: {DatabaseErrorMessage.Describe(exception)}");
        }
    }

    // ---------- Helpers ----------

    private void ReplaceLookups(IReadOnlyList<LookupItem> types, IReadOnlyList<LookupItem> methods)
    {
        var selectedTypeId = SelectedTypeFilter?.Id ?? 0;
        var selectedProcessId = SelectedProcessFilter?.Id ?? 0;

        _isRefreshingFilters = true;
        Types.Clear();
        ProcessingMethods.Clear();
        TypeFilterOptions.Clear();
        ProcessFilterOptions.Clear();
        TypeFilterOptions.Add(AllTypes);
        ProcessFilterOptions.Add(AllProcesses);
        foreach (var type in types)
        {
            Types.Add(type);
            TypeFilterOptions.Add(type);
        }
        foreach (var method in methods)
        {
            ProcessingMethods.Add(method);
            ProcessFilterOptions.Add(method);
        }

        SelectedTypeFilter = TypeFilterOptions.FirstOrDefault(item => item.Id == selectedTypeId) ?? AllTypes;
        SelectedProcessFilter = ProcessFilterOptions.FirstOrDefault(item => item.Id == selectedProcessId) ?? AllProcesses;
        _isRefreshingFilters = false;

        HasTypes = Types.Count > 0;
        HasProcessingMethods = ProcessingMethods.Count > 0;
    }

    /// <summary>
    /// Reloads bean types and processing methods from the database so the form
    /// dropdowns show rows added since the screen was loaded.
    /// </summary>
    private async Task<bool> RefreshLookupsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var types = await _repository.GetTypesAsync(cancellationToken);
            var methods = await _repository.GetProcessingMethodsAsync(cancellationToken);
            ReplaceLookups(types, methods);
            return true;
        }
        catch (Exception exception)
        {
            ShowError($"Gagal memuat jenis biji dan metode proses: {DatabaseErrorMessage.Describe(exception)}");
            return false;
        }
    }

    private void SyncFormLookupSelections()
    {
        if (IsFormOpen)
        {
            SelectedFormType = Types.FirstOrDefault(item =>
                string.Equals(item.Name, FormTypeName, StringComparison.OrdinalIgnoreCase));
            SelectedFormProcessingMethod = ProcessingMethods.FirstOrDefault(item =>
                string.Equals(item.Name, FormProcessingMethodName, StringComparison.OrdinalIgnoreCase));
        }
    }

    private void ApplyFilter() => ApplyFilter(resetPage: true);

    private void ApplyFilter(bool resetPage)
    {
        if (_isRefreshingFilters)
            return;

        var search = SearchText.Trim();
        var typeId = SelectedTypeFilter?.Id ?? 0;
        var processId = SelectedProcessFilter?.Id ?? 0;

        _filteredMaterials = _materials
            .Where(material =>
                search.Length == 0 ||
                material.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (material.Origin?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false))
            .Where(material => typeId == 0 || material.TypeId == typeId)
            .Where(material => processId == 0 || material.ProcessingMethodId == processId)
            .Where(material => !ShowLowStockOnly || material.IsLowStock)
            .ToList();

        if (resetPage)
            CurrentPage = 1;
        RefreshPage();
    }

    private void RefreshPage()
    {
        var total = _filteredMaterials.Count;
        PageCount = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        CurrentPage = Math.Clamp(CurrentPage, 1, PageCount);

        PageRows.Clear();
        foreach (var material in _filteredMaterials.Skip((CurrentPage - 1) * PageSize).Take(PageSize))
            PageRows.Add(new RawMaterialRowViewModel(material, OpenEditFormAsync, OpenStockDialog, RequestDeactivation));

        Pages.Clear();
        for (var page = 1; page <= PageCount; page++)
            Pages.Add(new PageItem(page, page == CurrentPage));

        HasNoResults = total == 0;
        EmptyMessage = _materials.Count == 0
            ? "Belum ada green bean. Klik \"Tambah Green Bean\" untuk menambahkan."
            : "Tidak ada green bean yang cocok dengan pencarian atau filter.";

        var first = (CurrentPage - 1) * PageSize + 1;
        var last = first + PageRows.Count - 1;
        ResultSummary = total == 0
            ? "Menampilkan 0 dari 0 green bean"
            : $"Menampilkan {first}–{last} dari {total} green bean";
    }

    private void ShowSuccess(string message)
    {
        IsStatusError = false;
        StatusMessage = message;
    }

    private void ShowError(string message)
    {
        IsStatusError = true;
        StatusMessage = message;
    }
}
