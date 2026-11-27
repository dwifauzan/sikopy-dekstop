using System;
using CommunityToolkit.Mvvm.Input;

namespace SIKOPY.ViewModels;

/// <summary>
/// Tells the user to ask an admin. Self-service reset was removed because it
/// let anyone change any account's password knowing only the username.
/// </summary>
public partial class ForgotPasswordViewModel : ViewModelBase
{
    private readonly Action _onNavigateToLogin;

    public ForgotPasswordViewModel()
        : this(() => { })
    {
    }

    public ForgotPasswordViewModel(Action onNavigateToLogin)
    {
        _onNavigateToLogin = onNavigateToLogin;
    }

    [RelayCommand]
    private void NavigateToLogin() => _onNavigateToLogin();
}
