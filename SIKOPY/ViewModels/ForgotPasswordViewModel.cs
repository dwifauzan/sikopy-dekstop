using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SIKOPY.Database;

namespace SIKOPY.ViewModels;

public partial class ForgotPasswordViewModel : ViewModelBase
{
    private readonly AuthRepository _repository = new();
    private readonly Action<string> _onResetSuccess;
    private readonly Action _onNavigateToLogin;

    public ForgotPasswordViewModel()
        : this(_ => { }, () => { })
    {
    }

    public ForgotPasswordViewModel(Action<string> onResetSuccess, Action onNavigateToLogin)
    {
        _onResetSuccess = onResetSuccess;
        _onNavigateToLogin = onNavigateToLogin;
    }

    [ObservableProperty] public partial string Username { get; set; } = string.Empty;
    [ObservableProperty] public partial string NewPassword { get; set; } = string.Empty;
    [ObservableProperty] public partial string ConfirmPassword { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsNewPasswordVisible { get; set; }
    [ObservableProperty] public partial bool IsConfirmPasswordVisible { get; set; }
    [ObservableProperty] public partial string ErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ResetPasswordCommand))]
    public partial bool IsLoading { get; set; }

    public char NewPasswordChar => IsNewPasswordVisible ? '\0' : '•';
    public char ConfirmPasswordChar => IsConfirmPasswordVisible ? '\0' : '•';
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    partial void OnUsernameChanged(string value) => ErrorMessage = string.Empty;
    partial void OnNewPasswordChanged(string value) => ErrorMessage = string.Empty;
    partial void OnConfirmPasswordChanged(string value) => ErrorMessage = string.Empty;
    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));

    partial void OnIsNewPasswordVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(NewPasswordChar));
    }

    partial void OnIsConfirmPasswordVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(ConfirmPasswordChar));
    }

    [RelayCommand]
    private void ToggleNewPasswordVisibility()
    {
        IsNewPasswordVisible = !IsNewPasswordVisible;
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

    [RelayCommand(CanExecute = nameof(CanResetPassword))]
    private async Task ResetPasswordAsync(CancellationToken cancellationToken = default)
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

        if (string.IsNullOrEmpty(NewPassword))
        {
            ErrorMessage = "Password baru wajib diisi.";
            return;
        }

        if (NewPassword.Length < 6)
        {
            ErrorMessage = "Password baru minimal 6 karakter.";
            return;
        }

        if (!string.Equals(NewPassword, ConfirmPassword, StringComparison.Ordinal))
        {
            ErrorMessage = "Konfirmasi password tidak cocok.";
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var updated = await _repository.ResetPasswordAsync(username, NewPassword, cancellationToken);
            if (!updated)
            {
                ErrorMessage = "Username tidak ditemukan.";
                return;
            }

            Reset();
            _onResetSuccess(username);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            ErrorMessage = $"Gagal mengubah password: {DatabaseErrorMessage.Describe(exception)}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanResetPassword() => !IsLoading;

    public void Reset()
    {
        Username = string.Empty;
        NewPassword = string.Empty;
        ConfirmPassword = string.Empty;
        IsNewPasswordVisible = false;
        IsConfirmPasswordVisible = false;
        ErrorMessage = string.Empty;
        IsLoading = false;
    }
}
