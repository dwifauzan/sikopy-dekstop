using System;
using System.Diagnostics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using SIKOPY.Database;
using SIKOPY.Models;

namespace SIKOPY.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly RoastingRepository _roastingRepository = new();
    private bool _isLoadingShop;
    public LoginViewModel Login { get; }
    public RegisterViewModel Register { get; }
    public ForgotPasswordViewModel ForgotPassword { get; }
    public RawMaterialsViewModel RawMaterials { get; } = new();
    public RoastingViewModel Roasting { get; } = new();
    public ShopWindowViewModel Shop { get; } = new();

    private UserAccount? _currentUser;
    public UserAccount? CurrentUser
    {
        get => _currentUser;
        private set
        {
            if (SetProperty(ref _currentUser, value))
            {
                Roasting.CurrentUserId = value?.Id;
                OnPropertyChanged(nameof(IsAuthenticated));
                OnPropertyChanged(nameof(IsStaff));
                OnPropertyChanged(nameof(CurrentUserName));
                OnPropertyChanged(nameof(CurrentUserRole));
                OnPropertyChanged(nameof(CurrentUserInitial));
            }
        }
    }

    public bool IsAuthenticated => CurrentUser is not null;
    public bool IsStaff => CurrentUser is { IsCustomer: false };
    public string CurrentUserName => CurrentUser?.FullName ?? "Admin Roastery";
    public string CurrentUserRole => CurrentUser?.Role ?? "Admin";
    public string CurrentUserInitial => CurrentUser?.Initial ?? "A";

    private ViewModelBase _currentPage;
    public ViewModelBase CurrentPage
    {
        get => _currentPage;
        set
        {
            if (SetProperty(ref _currentPage, value))
            {
                OnPropertyChanged(nameof(IsRawMaterialsPage));
                OnPropertyChanged(nameof(IsRoastingPage));
                OnPropertyChanged(nameof(IsShopPage));
            }
        }
    }

    public bool IsRawMaterialsPage => CurrentPage == RawMaterials;
    public bool IsRoastingPage => CurrentPage == Roasting;
    public bool IsShopPage => CurrentPage == Shop;

    public MainViewModel()
    {
        Login = new LoginViewModel(OnLoginSucceeded, ShowRegister, ShowForgotPassword);
        Register = new RegisterViewModel(
            username => ShowLogin(username, "Akun berhasil dibuat. Silakan masuk."),
            () => ShowLogin());
        ForgotPassword = new ForgotPasswordViewModel(() => ShowLogin());
        _currentPage = Login;
    }

    // Reload on every visit so stock changed on another screen is never stale.
    public void ShowRawMaterials()
    {
        if (!IsStaff) return;
        CurrentPage = RawMaterials;
        _ = RawMaterials.LoadCommand.ExecuteAsync(null);
    }

    public void ShowRoasting()
    {
        if (!IsStaff) return;
        CurrentPage = Roasting;
        _ = Roasting.LoadCommand.ExecuteAsync(null);
    }

    private void ShowLogin(string? prefillUsername = null, string? statusMessage = null)
    {
        Login.Reset(prefillUsername, statusMessage);
        CurrentPage = Login;
    }

    private void ShowRegister()
    {
        Register.Reset();
        CurrentPage = Register;
    }

    private void ShowForgotPassword() => CurrentPage = ForgotPassword;

    [RelayCommand]
    private void Logout()
    {
        CurrentUser = null;
        ShowLogin();
    }

    private async Task LoadShopProductsAsync()
    {
        if (_isLoadingShop)
            return;

        _isLoadingShop = true;
        try
        {
            var batches = await _roastingRepository.GetBatchesAsync(500);
            Shop.LoadProducts(batches);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Gagal memuat produk etalase: {ex.Message}");
        }
        finally
        {
            _isLoadingShop = false;
        }
    }
    
    private void OnLoginSucceeded(UserAccount user)
    {
        CurrentUser = user;
        // Customers only get the Etalase.
        if (IsStaff) ShowRawMaterials();
        else ShowShop();
    }
    
    public void ShowShop()
    {
        CurrentPage = Shop;
        _ = LoadShopProductsAsync();
    }

    
}
