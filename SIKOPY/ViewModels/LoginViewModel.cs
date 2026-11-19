using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SIKOPY.Database;
using SIKOPY.Models;

namespace SIKOPY.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    private readonly AuthRepository _repository = new();
    private readonly Action<UserAccount> _onLoginSuccess;
    private readonly Action _onNavigateToRegister;
    private readonly Action _onNavigateToForgotPassword;

    public LoginViewModel()
        : this(_ => { }, () => { }, () => { })
    {
    }

    public LoginViewModel(
        Action<UserAccount> onLoginSuccess,
        Action onNavigateToRegister,
        Action onNavigateToForgotPassword)
    {
        _onLoginSuccess = onLoginSuccess;
        _onNavigateToRegister = onNavigateToRegister;
        _onNavigateToForgotPassword = onNavigateToForgotPassword;
    }

    [ObservableProperty] public partial string Username { get; set; } = string.Empty;
    [ObservableProperty] public partial string Password { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsPasswordVisible { get; set; }
    [ObservableProperty] public partial string ErrorMessage { get; set; } = string.Empty;
    [ObservableProperty] public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    public partial bool IsLoading { get; set; }

    public char PasswordChar => IsPasswordVisible ? '\0' : '•';
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);

    partial void OnUsernameChanged(string value)
    {
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;
    }

    partial void OnPasswordChanged(string value)
    {
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;
    }

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
    partial void OnStatusMessageChanged(string value) => OnPropertyChanged(nameof(HasStatus));

    partial void OnIsPasswordVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(PasswordChar));
    }

    [RelayCommand]
    private void TogglePasswordVisibility()
    {
        IsPasswordVisible = !IsPasswordVisible;
    }

    [RelayCommand]
    private void NavigateToRegister()
    {
        _onNavigateToRegister();
    }

    [RelayCommand]
    private void NavigateToForgotPassword()
    {
        _onNavigateToForgotPassword();
    }

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync(CancellationToken cancellationToken = default)
    {
        var username = Username.Trim();
        if (username.Length == 0)
        {
            ErrorMessage = "Username wajib diisi.";
            return;
        }

        if (username.Length > 50)
        {
            ErrorMessage = "Username maksimal 50 karakter.";
            return;
        }

        if (string.IsNullOrEmpty(Password))
        {
            ErrorMessage = "Password wajib diisi.";
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;
        try
        {
            var user = await _repository.AuthenticateAsync(username, Password, cancellationToken);
            if (user is null)
            {
                ErrorMessage = "Username atau password salah.";
                return;
            }

            Password = string.Empty;
            IsPasswordVisible = false;
            _onLoginSuccess(user);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            ErrorMessage = $"Gagal masuk: {DatabaseErrorMessage.Describe(exception)}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanLogin() => !IsLoading;

    public void Reset(string? prefillUsername = null, string? statusMessage = null)
    {
        Username = prefillUsername ?? string.Empty;
        Password = string.Empty;
        IsPasswordVisible = false;
        ErrorMessage = string.Empty;
        StatusMessage = statusMessage ?? string.Empty;
        IsLoading = false;
    }
}
