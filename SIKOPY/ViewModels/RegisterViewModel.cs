using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SIKOPY.Database;
using SIKOPY.Models;

namespace SIKOPY.ViewModels;

public partial class RegisterViewModel : ViewModelBase
{
    private readonly AuthRepository _repository = new();
    private readonly Action<string> _onRegisterSuccess;
    private readonly Action _onNavigateToLogin;

    public RegisterViewModel()
        : this(_ => { }, () => { })
    {
    }

    public RegisterViewModel(Action<string> onRegisterSuccess, Action onNavigateToLogin)
    {
        _onRegisterSuccess = onRegisterSuccess;
        _onNavigateToLogin = onNavigateToLogin;
    }

    [ObservableProperty] public partial string FullName { get; set; } = string.Empty;
    [ObservableProperty] public partial string Username { get; set; } = string.Empty;
    [ObservableProperty] public partial string Password { get; set; } = string.Empty;
    [ObservableProperty] public partial string ConfirmPassword { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsPasswordVisible { get; set; }
    [ObservableProperty] public partial bool IsConfirmPasswordVisible { get; set; }
    [ObservableProperty] public partial string ErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    public partial bool IsLoading { get; set; }

    public char PasswordChar => IsPasswordVisible ? '\0' : '•';
    public char ConfirmPasswordChar => IsConfirmPasswordVisible ? '\0' : '•';
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    partial void OnFullNameChanged(string value) => ErrorMessage = string.Empty;
    partial void OnUsernameChanged(string value) => ErrorMessage = string.Empty;
    partial void OnPasswordChanged(string value) => ErrorMessage = string.Empty;
    partial void OnConfirmPasswordChanged(string value) => ErrorMessage = string.Empty;
    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));

    partial void OnIsPasswordVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(PasswordChar));
    }

    partial void OnIsConfirmPasswordVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(ConfirmPasswordChar));
    }

    [RelayCommand]
    private void TogglePasswordVisibility()
    {
        IsPasswordVisible = !IsPasswordVisible;
    }

    [RelayCommand]
    private void ToggleConfirmPasswordVisibility()
    {
        IsConfirmPasswordVisible = !IsConfirmPasswordVisible;
    }

    [RelayCommand]
    private void NavigateToLogin()
    {
        _onNavigateToLogin();
    }

    [RelayCommand(CanExecute = nameof(CanRegister))]
    private async Task RegisterAsync(CancellationToken cancellationToken = default)
    {
        var fullName = FullName.Trim();
        var username = Username.Trim();

        if (fullName.Length == 0)
        {
            ErrorMessage = "Nama lengkap wajib diisi.";
            return;
        }

        if (fullName.Length > 100)
        {
            ErrorMessage = "Nama lengkap maksimal 100 karakter.";
            return;
        }

        if (username.Length == 0)
        {
            ErrorMessage = "Username wajib diisi.";
            return;
        }

        if (username.Length < 3)
        {
            ErrorMessage = "Username minimal 3 karakter.";
            return;
        }

        if (username.Length > 50)
        {
            ErrorMessage = "Username maksimal 50 karakter.";
            return;
        }

        if (username.Any(char.IsWhiteSpace))
        {
            ErrorMessage = "Username tidak boleh mengandung spasi.";
            return;
        }

        if (string.IsNullOrEmpty(Password))
        {
            ErrorMessage = "Password wajib diisi.";
            return;
        }

        if (Password.Length < 6)
        {
            ErrorMessage = "Password minimal 6 karakter.";
            return;
        }

        if (!string.Equals(Password, ConfirmPassword, StringComparison.Ordinal))
        {
            ErrorMessage = "Konfirmasi password tidak cocok.";
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            if (await _repository.UsernameExistsAsync(username, cancellationToken))
            {
                ErrorMessage = "Username sudah digunakan.";
                return;
            }

            var createdUser = await _repository.CreateUserAsync(
                username,
                Password,
                fullName,
                UserAccount.CustomerRole,
                cancellationToken);

            Reset();
            _onRegisterSuccess(createdUser.Username);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            ErrorMessage = $"Gagal mendaftar: {DatabaseErrorMessage.Describe(exception)}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanRegister() => !IsLoading;

    public void Reset()
    {
        FullName = string.Empty;
        Username = string.Empty;
        Password = string.Empty;
        ConfirmPassword = string.Empty;
        IsPasswordVisible = false;
        IsConfirmPasswordVisible = false;
        ErrorMessage = string.Empty;
        IsLoading = false;
    }
}
