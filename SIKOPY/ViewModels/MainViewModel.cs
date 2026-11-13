using CommunityToolkit.Mvvm.Input;
using SIKOPY.Models;

namespace SIKOPY.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public LoginViewModel Login { get; }
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
        Login = new LoginViewModel(OnLoginSucceeded);
        _currentPage = Login;
    }

    public void ShowRawMaterials() => CurrentPage = RawMaterials;
    public void ShowRoasting() => CurrentPage = Roasting;

    [RelayCommand]
    private void Logout()
    {
        CurrentUser = null;
        Login.Reset();
        CurrentPage = Login;
    }

    private void OnLoginSucceeded(UserAccount user)
    {
        CurrentUser = user;
        CurrentPage = RawMaterials;
    }
}
