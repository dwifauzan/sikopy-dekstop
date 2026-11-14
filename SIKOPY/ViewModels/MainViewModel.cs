using CommunityToolkit.Mvvm.Input;
using SIKOPY.Models;

namespace SIKOPY.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public LoginViewModel Login { get; }
    public RegisterViewModel Register { get; }
    public ForgotPasswordViewModel ForgotPassword { get; }
    public RawMaterialsViewModel RawMaterials { get; } = new();
    public RoastingViewModel Roasting { get; } = new();

    private UserAccount? _currentUser;
    public UserAccount? CurrentUser
    {
        get => _currentUser;
        private set
        {
            if (SetProperty(ref _currentUser, value))
            {
                OnPropertyChanged(nameof(IsAuthenticated));
                OnPropertyChanged(nameof(CurrentUserName));
                OnPropertyChanged(nameof(CurrentUserRole));
                OnPropertyChanged(nameof(CurrentUserInitial));
            }
        }
    }

    public bool IsAuthenticated => CurrentUser is not null;
    public string CurrentUserName => CurrentUser?.FullName ?? "Admin Roastery";
    public string CurrentUserRole => CurrentUser?.Role ?? "Admin";
    public string CurrentUserInitial => CurrentUser?.Initial ?? "A";

    private ViewModelBase _currentPage;
    public ViewModelBase CurrentPage
    {
        get => _currentPage;
        set => SetProperty(ref _currentPage, value);
    }

    public MainViewModel()
    {
        Login = new LoginViewModel(OnLoginSucceeded, ShowRegister, ShowForgotPassword);
        Register = new RegisterViewModel(
            username => ShowLogin(username, "Akun berhasil dibuat. Silakan masuk."),
            () => ShowLogin());
        ForgotPassword = new ForgotPasswordViewModel(
            username => ShowLogin(username, "Password berhasil diperbarui. Silakan masuk."),
            () => ShowLogin());
        _currentPage = Login;
    }

    public void ShowRawMaterials() => CurrentPage = RawMaterials;
    public void ShowRoasting() => CurrentPage = Roasting;

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

    private void ShowForgotPassword()
    {
        ForgotPassword.Reset();
        CurrentPage = ForgotPassword;
    }

    [RelayCommand]
    private void Logout()
    {
        CurrentUser = null;
        ShowLogin();
    }

    private void OnLoginSucceeded(UserAccount user)
    {
        CurrentUser = user;
        CurrentPage = RawMaterials;
    }
}
