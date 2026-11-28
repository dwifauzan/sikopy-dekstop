using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using SIKOPY.Database;
using SIKOPY.Models;

namespace SIKOPY.ViewModels;

/// <summary>Snapshot sederhana untuk menyimpan state keranjang secara persisten.</summary>
public sealed record CartItemSnapshot(int ProductId, string Code, decimal SavedGram);

/// <summary>Wrapper command agar command level item tetap bekerja dengan atau tanpa CommandParameter dari AXAML.</summary>
internal sealed class BoundItemCommand<T> : ICommand
{
    private readonly ICommand _inner;
    private readonly T _fallbackItem;

    public BoundItemCommand(ICommand inner, T fallbackItem)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _fallbackItem = fallbackItem;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => _inner.CanExecuteChanged += value;
        remove => _inner.CanExecuteChanged -= value;
    }

    public bool CanExecute(object? parameter)
        => _inner.CanExecute(ResolveParameter(parameter));

    public void Execute(object? parameter)
        => _inner.Execute(ResolveParameter(parameter));

    private object? ResolveParameter(object? parameter)
        => parameter is T ? parameter : _fallbackItem;
}

/// <summary>Representasi produk etalase yang memetakan data RoastingBatch beserta origin dan harga per gram.</summary>
public sealed class ShopProductItem : ObservableObject
{
    private static readonly CultureInfo Indonesian = CultureInfo.GetCultureInfo("id-ID");

    private decimal _inCartGram;
    private ICommand? _buyCommand;

    public ShopProductItem(
        RoastingBatch batch,
        string origin = "-",
        decimal pricePerGram = 0m,
        ICommand? buyCommand = null)
    {
        Batch = batch ?? throw new ArgumentNullException(nameof(batch));
        Origin = string.IsNullOrWhiteSpace(origin) ? "-" : origin.Trim();
        PricePerGram = Math.Max(0m, pricePerGram);
        BuyCommand = buyCommand;
    }

    public RoastingBatch Batch { get; }
    public int Id => Batch.Id;
    public string Code => Batch.Code;
    public string MaterialName => Batch.MaterialName;
    public string? RoastLevel => Batch.RoastLevel;
    public DateTime BatchDate => Batch.BatchDate;
    public string? Notes => Batch.Notes;
    public decimal? RoastedGram => Batch.RoastedGram;

    public string Origin { get; }
    public decimal PricePerGram { get; }

    public ICommand? BuyCommand
    {
        get => _buyCommand;
        internal set
        {
            var wrapped = value is null or BoundItemCommand<ShopProductItem>
                ? value
                : new BoundItemCommand<ShopProductItem>(value, this);

            if (ReferenceEquals(_buyCommand, wrapped))
            {
                return;
            }

            _buyCommand = wrapped;
            OnPropertyChanged();
        }
    }

    /// <summary>Stok gram tersedia (0 g apabila RoastedGram bernilai null atau <= 0).</summary>
    public decimal AvailableStockGram => Math.Max(0m, RoastedGram ?? 0m);

    public decimal InCartGram
    {
        get => _inCartGram;
        private set
        {
            var normalized = Math.Max(0m, value);
            if (_inCartGram == normalized)
            {
                return;
            }

            _inCartGram = normalized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RemainingStockGram));
            OnPropertyChanged(nameof(RemainingStockDisplay));
            OnPropertyChanged(nameof(CanBuy));
            OnPropertyChanged(nameof(IsOutOfStock));
        }
    }

    public decimal RemainingStockGram => Math.Max(0m, AvailableStockGram - InCartGram);
    public bool HasStock => AvailableStockGram > 0m;
    public bool CanBuy => AvailableStockGram > 0m && InCartGram < AvailableStockGram;
    public bool IsOutOfStock => !CanBuy;

    public string LevelDisplay => string.IsNullOrWhiteSpace(RoastLevel) ? "-" : RoastLevel;
    public string DateDisplay => BatchDate.ToString("d MMM yyyy", Indonesian);
    public string NotesDisplay => string.IsNullOrWhiteSpace(Notes) ? "-" : Notes;
    public string StockDisplay => $"Stok {AvailableStockGram.ToString("N0", Indonesian)} g";
    public string RemainingStockDisplay => $"Sisa {RemainingStockGram.ToString("N0", Indonesian)} g";
    public string PricePerGramDisplay => $"Rp {PricePerGram.ToString("N0", Indonesian)}/g";

    internal void UpdateCartQuantity(decimal inCartGram)
    {
        InCartGram = inCartGram;
    }
}

/// <summary>Item keranjang yang menyimpan referensi produk, batas stok RoastedGram, dan takaran gram tersimpan.</summary>
public sealed class CartItemViewModel : ObservableObject
{
    private static readonly CultureInfo Indonesian = CultureInfo.GetCultureInfo("id-ID");

    private decimal _savedGram;

    public CartItemViewModel(
        ShopProductItem product,
        decimal initialSavedGram,
        decimal gramStep = 50m,
        ICommand? increaseCommand = null,
        ICommand? decreaseCommand = null,
        ICommand? removeCommand = null)
    {
        Product = product ?? throw new ArgumentNullException(nameof(product));
        GramStep = gramStep > 0m ? gramStep : 50m;
        MaxStockGram = product.AvailableStockGram;
        _savedGram = Math.Clamp(initialSavedGram, 0m, MaxStockGram);

        IncreaseCommand = WrapCommand(increaseCommand);
        DecreaseCommand = WrapCommand(decreaseCommand);
        RemoveCommand = WrapCommand(removeCommand);
    }

    public ShopProductItem Product { get; private set; }
    public int ProductId => Product.Id;
    public string Code => Product.Code;
    public string MaterialName => Product.MaterialName;
    public string LevelDisplay => Product.LevelDisplay;
    public string Origin => Product.Origin;
    public decimal PricePerGram => Product.PricePerGram;
    public decimal MaxStockGram { get; private set; }
    public decimal GramStep { get; }

    public ICommand? IncreaseCommand { get; }
    public ICommand? DecreaseCommand { get; }
    public ICommand? RemoveCommand { get; }

    public decimal SavedGram
    {
        get => _savedGram;
        set
        {
            var clamped = Math.Clamp(value, 0m, MaxStockGram);
            if (_savedGram == clamped)
            {
                return;
            }

            _savedGram = clamped;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SavedGramDisplay));
            OnPropertyChanged(nameof(SubtotalPrice));
            OnPropertyChanged(nameof(SubtotalPriceDisplay));
            OnPropertyChanged(nameof(CanIncrease));
            OnPropertyChanged(nameof(CanDecrease));
            OnPropertyChanged(nameof(IsAtMaxStock));
        }
    }

    public bool CanIncrease => SavedGram < MaxStockGram;
    public bool CanDecrease => SavedGram > 0m;
    public bool IsAtMaxStock => SavedGram >= MaxStockGram;

    public string SavedGramDisplay => $"{SavedGram.ToString("N0", Indonesian)} g";
    public string MaxStockDisplay => $"Maks {MaxStockGram.ToString("N0", Indonesian)} g";
    public decimal SubtotalPrice => SavedGram * PricePerGram;
    public string SubtotalPriceDisplay => $"Rp {SubtotalPrice.ToString("N0", Indonesian)}";

    internal void UpdateProduct(ShopProductItem latestProduct)
    {
        ArgumentNullException.ThrowIfNull(latestProduct);

        Product = latestProduct;
        MaxStockGram = latestProduct.AvailableStockGram;

        var clamped = Math.Clamp(_savedGram, 0m, MaxStockGram);
        var savedGramChanged = _savedGram != clamped;
        _savedGram = clamped;

        OnPropertyChanged(nameof(Product));
        OnPropertyChanged(nameof(ProductId));
        OnPropertyChanged(nameof(Code));
        OnPropertyChanged(nameof(MaterialName));
        OnPropertyChanged(nameof(LevelDisplay));
        OnPropertyChanged(nameof(Origin));
        OnPropertyChanged(nameof(PricePerGram));
        OnPropertyChanged(nameof(MaxStockGram));
        OnPropertyChanged(nameof(MaxStockDisplay));

        if (savedGramChanged)
        {
            OnPropertyChanged(nameof(SavedGram));
            OnPropertyChanged(nameof(SavedGramDisplay));
        }

        OnPropertyChanged(nameof(SubtotalPrice));
        OnPropertyChanged(nameof(SubtotalPriceDisplay));
        OnPropertyChanged(nameof(CanIncrease));
        OnPropertyChanged(nameof(CanDecrease));
        OnPropertyChanged(nameof(IsAtMaxStock));
    }

    private ICommand? WrapCommand(ICommand? command)
        => command is null or BoundItemCommand<CartItemViewModel>
            ? command
            : new BoundItemCommand<CartItemViewModel>(command, this);
}

/// <summary>ViewModel halaman Etalase Toko dan Modal Keranjang (single source of truth).</summary>
public class ShopWindowViewModel : ViewModelBase
{
    public const decimal DefaultBuyGramStep = 100m;
    public const decimal DefaultCartGramStep = 50m;

    private static readonly CultureInfo Indonesian = CultureInfo.GetCultureInfo("id-ID");

    private static readonly IReadOnlyList<string> DefaultSortOptions =
    [
        "Terbaru",
        "Terlama",
        "Stok Terbanyak",
        "Harga Terendah",
        "Harga Tertinggi",
        "Nama A–Z"
    ];

    private readonly Action<IReadOnlyList<CartItemSnapshot>>? _persistCartAction;
    private readonly Func<IReadOnlyList<CartItemSnapshot>>? _loadPersistedCartFunc;
    private readonly HashSet<CartItemViewModel> _subscribedCartItems = [];
    private readonly RelayCommand _openCartCommand;
    private readonly RelayCommand _closeCartCommand;
    private readonly RelayCommand<ShopProductItem> _buyCommand;
    private readonly RelayCommand<CartItemViewModel> _increaseCartItemCommand;
    private readonly RelayCommand<CartItemViewModel> _decreaseCartItemCommand;
    private readonly RelayCommand<CartItemViewModel> _removeCartItemCommand;
    private readonly RelayCommand _saveCartCommand;
    private readonly RelayCommand _clearStatusMessageCommand;
    private readonly CommunityToolkit.Mvvm.Input.AsyncRelayCommand _checkoutCommand;
    private readonly SalesRepository _sales = new();
    private bool _isLoading;

    private string _searchQuery = string.Empty;
    private string _selectedSortOption = "Terbaru";
    private bool _isCartPopupVisible;
    private string? _statusMessage;
    private IReadOnlyList<CartItemSnapshot> _savedCartSnapshot = Array.Empty<CartItemSnapshot>();

    public ShopWindowViewModel()
        : this(Array.Empty<ShopProductItem>(), null, null)
    {
    }

    public ShopWindowViewModel(
        IEnumerable<RoastingBatch> batches,
        Func<RoastingBatch, (string Origin, decimal PricePerGram)>? metadataSelector = null,
        Action<IReadOnlyList<CartItemSnapshot>>? persistCartAction = null,
        Func<IReadOnlyList<CartItemSnapshot>>? loadPersistedCartFunc = null)
        : this(
            MapBatches(batches, metadataSelector),
            persistCartAction,
            loadPersistedCartFunc)
    {
    }

    public ShopWindowViewModel(
        IEnumerable<ShopProductItem> products,
        Action<IReadOnlyList<CartItemSnapshot>>? persistCartAction = null,
        Func<IReadOnlyList<CartItemSnapshot>>? loadPersistedCartFunc = null)
    {
        _persistCartAction = persistCartAction;
        _loadPersistedCartFunc = loadPersistedCartFunc;

        _openCartCommand = new RelayCommand(OpenCart);
        _closeCartCommand = new RelayCommand(CloseCart);
        _buyCommand = new RelayCommand<ShopProductItem>(BuyProduct, CanBuyProduct);
        _increaseCartItemCommand = new RelayCommand<CartItemViewModel>(IncreaseCartItem, CanIncreaseCartItem);
        _decreaseCartItemCommand = new RelayCommand<CartItemViewModel>(DecreaseCartItem, CanDecreaseCartItem);
        _removeCartItemCommand = new RelayCommand<CartItemViewModel>(RemoveCartItem, item => item is not null);
        _saveCartCommand = new RelayCommand(SaveCart);
        _clearStatusMessageCommand = new RelayCommand(() => StatusMessage = null);
        _checkoutCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(CheckoutAsync, () => HasCartItems);

        AllProducts = new ObservableCollection<ShopProductItem>();
        FilteredProducts = new ObservableCollection<ShopProductItem>();
        CartItems = new ObservableCollection<CartItemViewModel>();

        AllProducts.CollectionChanged += OnAllProductsCollectionChanged;
        CartItems.CollectionChanged += OnCartItemsCollectionChanged;

        _savedCartSnapshot = LoadPersistedSnapshot();

        if (products is not null)
        {
            LoadProducts(products);
        }
    }

    // -------------------------------------------------------------------------
    // Core State (Mutable Properties)
    // -------------------------------------------------------------------------

    public ObservableCollection<ShopProductItem> AllProducts { get; }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            var normalized = value ?? string.Empty;
            if (string.Equals(_searchQuery, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _searchQuery = normalized;
            OnPropertyChanged();
            RefreshFilteredProducts();
        }
    }

    public IReadOnlyList<string> SortOptions => DefaultSortOptions;

    public string SelectedSortOption
    {
        get => _selectedSortOption;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? "Terbaru" : value;
            if (string.Equals(_selectedSortOption, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _selectedSortOption = normalized;
            OnPropertyChanged();
            RefreshFilteredProducts();
        }
    }

    public ObservableCollection<CartItemViewModel> CartItems { get; }

    public bool IsCartPopupVisible
    {
        get => _isCartPopupVisible;
        set
        {
            if (_isCartPopupVisible == value)
            {
                return;
            }

            _isCartPopupVisible = value;
            OnPropertyChanged();
        }
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            if (string.Equals(_statusMessage, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _statusMessage = normalized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasStatusMessage));
        }
    }

    public IReadOnlyList<CartItemSnapshot> SavedCartSnapshot => _savedCartSnapshot;

    // -------------------------------------------------------------------------
    // Derived / Computed Properties
    // -------------------------------------------------------------------------

    public ObservableCollection<ShopProductItem> FilteredProducts { get; }

    public int FilteredProductCount => FilteredProducts.Count;
    public string FilteredProductCountText => $"{FilteredProducts.Count} produk";
    public string CatalogSubheaderText => $"Roast bean · {FilteredProducts.Count} produk";
    public bool IsFilteredProductsEmpty => FilteredProducts.Count == 0;

    public int CartBadgeCount => CartItems.Count;
    public string CartHeaderSummaryText => $"{CartItems.Count} produk dalam keranjang";
    public decimal TotalSavedGram => CartItems.Sum(item => item.SavedGram);
    public string CartFooterSummaryText =>
        $"{CartItems.Count} produk · {TotalSavedGram.ToString("N0", Indonesian)} g tersimpan";

    public decimal TotalCartPrice => CartItems.Sum(item => item.SubtotalPrice);
    public string TotalCartPriceDisplay => $"Rp {TotalCartPrice.ToString("N0", Indonesian)}";

    public bool IsCartEmpty => CartItems.Count == 0;
    public bool HasCartItems => CartItems.Count > 0;
    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    // -------------------------------------------------------------------------
    // Commands
    // -------------------------------------------------------------------------

    public ICommand OpenCartCommand => _openCartCommand;
    public ICommand CloseCartCommand => _closeCartCommand;
    public ICommand BuyCommand => _buyCommand;
    public ICommand IncreaseCartItemCommand => _increaseCartItemCommand;
    public ICommand DecreaseCartItemCommand => _decreaseCartItemCommand;
    public ICommand RemoveCartItemCommand => _removeCartItemCommand;
    public ICommand SaveCartCommand => _saveCartCommand;
    public ICommand ClearStatusMessageCommand => _clearStatusMessageCommand;
    public ICommand CheckoutCommand => _checkoutCommand;

    /// <summary>Diisi MainViewModel saat login; pembeli yang dicatat di penjualan.</summary>
    public int? CurrentUserId { get; set; }
    public string? CustomerName { get; set; }

    // -------------------------------------------------------------------------
    // Public Operations
    // -------------------------------------------------------------------------

    public void LoadProducts(
        IEnumerable<RoastingBatch> batches,
        Func<RoastingBatch, (string Origin, decimal PricePerGram)>? metadataSelector = null)
    {
        LoadProducts(MapBatches(batches, metadataSelector));
    }

    public void LoadProducts(IEnumerable<ShopProductItem> products)
    {
        ArgumentNullException.ThrowIfNull(products);

        AllProducts.CollectionChanged -= OnAllProductsCollectionChanged;
        AllProducts.Clear();
        foreach (var product in products)
        {
            product.BuyCommand ??= _buyCommand;
            AllProducts.Add(product);
        }
        AllProducts.CollectionChanged += OnAllProductsCollectionChanged;

        if (CartItems.Count > 0)
        {
            ReconcileCartWithProducts();
        }
        else
        {
            RestoreCartFromSnapshotIfNeeded();
        }

        RefreshFilteredProducts();
        NotifyCartStateChanged();
    }

    /// <summary>Muat produk yang dijual (aktif dan sudah ada harga) dari database.</summary>
    public async Task LoadAsync()
    {
        if (_isLoading)
            return;

        _isLoading = true;
        try
        {
            var products = await _sales.GetShopProductsAsync();
            LoadProducts(products.Select(p => new ShopProductItem(p.Batch, p.Origin, p.PricePerGram)));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Gagal memuat produk etalase: {DatabaseErrorMessage.Describe(ex)}";
        }
        finally
        {
            _isLoading = false;
        }
    }

    /// <summary>Bayar isi keranjang: stok dan harga dicek ulang di database dalam satu transaksi.</summary>
    public async Task CheckoutAsync()
    {
        if (CurrentUserId is not int userId)
        {
            StatusMessage = "Sesi login berakhir. Silakan masuk lagi.";
            return;
        }

        var items = CartItems
            .Where(item => item.SavedGram > 0m)
            .Select(item => (item.ProductId, item.SavedGram))
            .ToList();
        if (items.Count == 0)
            return;

        var total = TotalCartPrice;
        try
        {
            var invoice = await _sales.CheckoutAsync(userId, CustomerName, items);
            CartItems.Clear();
            _savedCartSnapshot = Array.Empty<CartItemSnapshot>();
            PersistSnapshot(_savedCartSnapshot);
            IsCartPopupVisible = false;
            await LoadAsync();
            StatusMessage = $"Pesanan {invoice} berhasil · Rp {total.ToString("N0", Indonesian)}.";
        }
        catch (Exception ex)
        {
            // Keranjang dibiarkan utuh supaya pembeli bisa menyesuaikan lalu coba lagi.
            StatusMessage = $"Pembayaran gagal: {DatabaseErrorMessage.Describe(ex)}";
        }
    }

    public void OpenCart()
    {
        IsCartPopupVisible = true;
    }

    public void CloseCart()
    {
        IsCartPopupVisible = false;
    }

    public bool CanBuyProduct(ShopProductItem? product)
    {
        if (product is null || product.AvailableStockGram <= 0m)
        {
            return false;
        }

        var existingItem = FindCartItem(product);
        var currentInCart = existingItem?.SavedGram ?? 0m;
        return currentInCart < product.AvailableStockGram;
    }

    public void BuyProduct(ShopProductItem? product)
    {
        if (product is null)
        {
            return;
        }

        if (product.AvailableStockGram <= 0m)
        {
            StatusMessage = $"Stok {product.MaterialName} kosong (0 g).";
            return;
        }

        var existingItem = FindCartItem(product);
        var currentInCart = existingItem?.SavedGram ?? 0m;
        var remaining = product.AvailableStockGram - currentInCart;

        if (remaining <= 0m)
        {
            StatusMessage =
                $"Batas stok {product.MaterialName} ({product.AvailableStockGram.ToString("N0", Indonesian)} g) telah tercapai.";
            return;
        }

        var gramToAdd = Math.Min(DefaultBuyGramStep, remaining);

        if (existingItem is null)
        {
            var newItem = CreateCartItem(product, gramToAdd);
            CartItems.Add(newItem);
        }
        else
        {
            existingItem.SavedGram += gramToAdd;
        }

        StatusMessage = gramToAdd < DefaultBuyGramStep
            ? $"{product.MaterialName} ditambahkan {gramToAdd.ToString("N0", Indonesian)} g (menyesuaikan sisa stok maksimal)."
            : $"{product.MaterialName} (+{gramToAdd.ToString("N0", Indonesian)} g) ditambahkan ke keranjang.";
    }

    public bool CanIncreaseCartItem(CartItemViewModel? item)
        => item is not null && item.CanIncrease;

    public void IncreaseCartItem(CartItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        if (item.SavedGram >= item.MaxStockGram)
        {
            StatusMessage =
                $"Batas stok {item.MaterialName} ({item.MaxStockGram.ToString("N0", Indonesian)} g) telah tercapai.";
            return;
        }

        var nextGram = Math.Min(item.SavedGram + item.GramStep, item.MaxStockGram);
        item.SavedGram = nextGram;

        if (item.SavedGram >= item.MaxStockGram)
        {
            StatusMessage =
                $"Batas maksimal stok {item.MaterialName} ({item.MaxStockGram.ToString("N0", Indonesian)} g) tercapai.";
        }
    }

    public bool CanDecreaseCartItem(CartItemViewModel? item)
        => item is not null && item.CanDecrease;

    public void DecreaseCartItem(CartItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        var nextGram = item.SavedGram - item.GramStep;
        if (nextGram <= 0m)
        {
            CartItems.Remove(item);
            StatusMessage = $"{item.MaterialName} dihapus dari keranjang.";
            return;
        }

        item.SavedGram = nextGram;
    }

    public void RemoveCartItem(CartItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        if (CartItems.Remove(item))
        {
            StatusMessage = $"{item.MaterialName} dihapus dari keranjang.";
        }
    }

    public void SaveCart()
    {
        var snapshot = CartItems
            .Select(item => new CartItemSnapshot(item.ProductId, item.Code, item.SavedGram))
            .ToList();

        if (!PersistSnapshot(snapshot))
        {
            StatusMessage = "Gagal menyimpan keranjang. Periksa akses penyimpanan lalu coba lagi.";
            return;
        }

        _savedCartSnapshot = snapshot;
        OnPropertyChanged(nameof(SavedCartSnapshot));
        NotifyCartStateChanged();

        StatusMessage = CartItems.Count > 0
            ? $"Keranjang berhasil disimpan ({CartItems.Count} produk · {TotalSavedGram.ToString("N0", Indonesian)} g)."
            : "Keranjang kosong berhasil disimpan.";

        IsCartPopupVisible = false;
    }

    // -------------------------------------------------------------------------
    // Internal Helpers
    // -------------------------------------------------------------------------

    private static IEnumerable<ShopProductItem> MapBatches(
        IEnumerable<RoastingBatch> batches,
        Func<RoastingBatch, (string Origin, decimal PricePerGram)>? metadataSelector)
    {
        ArgumentNullException.ThrowIfNull(batches);

        foreach (var batch in batches)
        {
            var (origin, pricePerGram) = metadataSelector?.Invoke(batch) ?? ("-", 0m);
            yield return new ShopProductItem(batch, origin, pricePerGram);
        }
    }

    private CartItemViewModel CreateCartItem(ShopProductItem product, decimal initialGram)
        => new(
            product,
            initialGram,
            DefaultCartGramStep,
            _increaseCartItemCommand,
            _decreaseCartItemCommand,
            _removeCartItemCommand);

    private CartItemViewModel? FindCartItem(ShopProductItem product)
    {
        return CartItems.FirstOrDefault(item =>
            ReferenceEquals(item.Product, product) ||
            (product.Id > 0 && item.ProductId == product.Id) ||
            (!string.IsNullOrWhiteSpace(product.Code) &&
             string.Equals(item.Code, product.Code, StringComparison.OrdinalIgnoreCase)));
    }

    private ShopProductItem? FindMatchingProduct(int productId, string? code)
    {
        return AllProducts.FirstOrDefault(p =>
            (productId > 0 && p.Id == productId) ||
            (!string.IsNullOrWhiteSpace(code) &&
             string.Equals(p.Code, code, StringComparison.OrdinalIgnoreCase)));
    }

    private void OnAllProductsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (ShopProductItem item in e.NewItems)
            {
                item.BuyCommand ??= _buyCommand;
            }
        }

        RefreshFilteredProducts();
        SyncProductsWithCart();
    }

    private void OnCartItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var tracked in _subscribedCartItems)
            {
                tracked.PropertyChanged -= OnCartItemPropertyChanged;
            }

            _subscribedCartItems.Clear();

            foreach (var item in CartItems)
            {
                if (_subscribedCartItems.Add(item))
                {
                    item.PropertyChanged += OnCartItemPropertyChanged;
                }
            }
        }
        else
        {
            if (e.OldItems is not null)
            {
                foreach (CartItemViewModel item in e.OldItems)
                {
                    if (_subscribedCartItems.Remove(item))
                    {
                        item.PropertyChanged -= OnCartItemPropertyChanged;
                    }
                }
            }

            if (e.NewItems is not null)
            {
                foreach (CartItemViewModel item in e.NewItems)
                {
                    if (_subscribedCartItems.Add(item))
                    {
                        item.PropertyChanged += OnCartItemPropertyChanged;
                    }
                }
            }
        }

        NotifyCartStateChanged();
    }

    private void OnCartItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CartItemViewModel.SavedGram))
        {
            NotifyCartStateChanged();
        }
    }

    private void RefreshFilteredProducts()
    {
        var query = SearchQuery.Trim();

        IEnumerable<ShopProductItem> filtered = string.IsNullOrEmpty(query)
            ? AllProducts
            : AllProducts.Where(item => MatchesSearch(item, query));

        filtered = ApplySort(filtered, SelectedSortOption);

        FilteredProducts.Clear();
        foreach (var item in filtered)
        {
            FilteredProducts.Add(item);
        }

        OnPropertyChanged(nameof(FilteredProductCount));
        OnPropertyChanged(nameof(FilteredProductCountText));
        OnPropertyChanged(nameof(CatalogSubheaderText));
        OnPropertyChanged(nameof(IsFilteredProductsEmpty));
    }

    private static bool MatchesSearch(ShopProductItem item, string query)
    {
        return ContainsIgnoreCase(item.MaterialName, query)
            || ContainsIgnoreCase(item.Code, query)
            || ContainsIgnoreCase(item.RoastLevel, query)
            || ContainsIgnoreCase(item.Origin, query)
            || ContainsIgnoreCase(item.Notes, query);
    }

    private static bool ContainsIgnoreCase(string? source, string query)
        => !string.IsNullOrEmpty(source) &&
           source.Contains(query, StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<ShopProductItem> ApplySort(
        IEnumerable<ShopProductItem> items,
        string sortOption)
    {
        return sortOption switch
        {
            "Terlama" => items
                .OrderBy(p => p.BatchDate)
                .ThenBy(p => p.Id),
            "Stok Terbanyak" => items
                .OrderByDescending(p => p.AvailableStockGram)
                .ThenByDescending(p => p.BatchDate)
                .ThenByDescending(p => p.Id),
            "Harga Terendah" => items
                .OrderBy(p => p.PricePerGram)
                .ThenBy(p => p.MaterialName, StringComparer.OrdinalIgnoreCase),
            "Harga Tertinggi" => items
                .OrderByDescending(p => p.PricePerGram)
                .ThenBy(p => p.MaterialName, StringComparer.OrdinalIgnoreCase),
            "Nama A–Z" or "Nama A-Z" => items
                .OrderBy(p => p.MaterialName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(p => p.Code, StringComparer.OrdinalIgnoreCase),
            _ => items
                .OrderByDescending(p => p.BatchDate)
                .ThenByDescending(p => p.Id)
        };
    }

    private void SyncProductsWithCart()
    {
        foreach (var product in AllProducts)
        {
            var cartItem = FindCartItem(product);
            product.UpdateCartQuantity(cartItem?.SavedGram ?? 0m);
        }
    }

    private void NotifyCartStateChanged()
    {
        SyncProductsWithCart();

        OnPropertyChanged(nameof(CartBadgeCount));
        OnPropertyChanged(nameof(CartHeaderSummaryText));
        OnPropertyChanged(nameof(TotalSavedGram));
        OnPropertyChanged(nameof(CartFooterSummaryText));
        OnPropertyChanged(nameof(TotalCartPrice));
        OnPropertyChanged(nameof(TotalCartPriceDisplay));
        OnPropertyChanged(nameof(IsCartEmpty));
        OnPropertyChanged(nameof(HasCartItems));

        _buyCommand.RaiseCanExecuteChanged();
        _increaseCartItemCommand.RaiseCanExecuteChanged();
        _decreaseCartItemCommand.RaiseCanExecuteChanged();
        _removeCartItemCommand.RaiseCanExecuteChanged();
        _saveCartCommand.RaiseCanExecuteChanged();
        _checkoutCommand.NotifyCanExecuteChanged();
    }

    private void ReconcileCartWithProducts()
    {
        for (var i = CartItems.Count - 1; i >= 0; i--)
        {
            var cartItem = CartItems[i];
            var matchedProduct = FindMatchingProduct(cartItem.ProductId, cartItem.Code);

            if (matchedProduct is null || matchedProduct.AvailableStockGram <= 0m)
            {
                CartItems.RemoveAt(i);
                continue;
            }

            cartItem.UpdateProduct(matchedProduct);

            if (cartItem.SavedGram <= 0m)
            {
                CartItems.RemoveAt(i);
            }
        }
    }

    private void RestoreCartFromSnapshotIfNeeded()
    {
        if (CartItems.Count > 0 || _savedCartSnapshot.Count == 0 || AllProducts.Count == 0)
        {
            return;
        }

        foreach (var entry in _savedCartSnapshot)
        {
            if (entry.SavedGram <= 0m)
            {
                continue;
            }

            var product = FindMatchingProduct(entry.ProductId, entry.Code);
            if (product is null || product.AvailableStockGram <= 0m)
            {
                continue;
            }

            var clampedGram = Math.Min(entry.SavedGram, product.AvailableStockGram);
            if (clampedGram > 0m)
            {
                CartItems.Add(CreateCartItem(product, clampedGram));
            }
        }
    }

    private bool PersistSnapshot(IReadOnlyList<CartItemSnapshot> snapshot)
    {
        if (_persistCartAction is not null)
        {
            try
            {
                _persistCartAction(snapshot);
                return true;
            }
            catch
            {
                return false;
            }
        }

        try
        {
            var path = GetDefaultCartFilePath();
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(snapshot);
            File.WriteAllText(path, json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private IReadOnlyList<CartItemSnapshot> LoadPersistedSnapshot()
    {
        if (_loadPersistedCartFunc is not null)
        {
            return _loadPersistedCartFunc() ?? Array.Empty<CartItemSnapshot>();
        }

        try
        {
            var path = GetDefaultCartFilePath();
            if (!File.Exists(path))
            {
                return Array.Empty<CartItemSnapshot>();
            }

            var json = File.ReadAllText(path);
            var items = JsonSerializer.Deserialize<List<CartItemSnapshot>>(json);
            return items ?? (IReadOnlyList<CartItemSnapshot>)Array.Empty<CartItemSnapshot>();
        }
        catch
        {
            return Array.Empty<CartItemSnapshot>();
        }
    }

    private static string GetDefaultCartFilePath()
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(baseDir, "SIKOPY", "shop_cart.json");
    }

    private sealed class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

        public void Execute(object? parameter)
        {
            if (CanExecute(parameter))
            {
                _execute();
            }
        }

        public void RaiseCanExecuteChanged()
            => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    private sealed class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        private readonly Func<T?, bool>? _canExecute;

        public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter)
        {
            var typed = parameter is T value ? value : default;
            return _canExecute?.Invoke(typed) ?? true;
        }

        public void Execute(object? parameter)
        {
            var typed = parameter is T value ? value : default;
            if (_canExecute is null || _canExecute(typed))
            {
                _execute(typed);
            }
        }

        public void RaiseCanExecuteChanged()
            => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
