using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SIKOPY.Database;
using SIKOPY.Models;

namespace SIKOPY.ViewModels;

/// <summary>Halaman Pengguna (khusus admin): ubah peran, reset password, nonaktifkan akun.</summary>
public partial class UsersViewModel : ViewModelBase
{
    private enum UserAction { Role, Reset, Deactivate, Activate }

    private readonly UserRepository _users = new();
    private UserAction _pendingAction;
    private bool _isSaving;

    /// <summary>Admin yang sedang login; tidak boleh mengubah peran / menonaktifkan dirinya sendiri.</summary>
    public int? CurrentUserId { get; set; }

    public ObservableCollection<UserAccount> Users { get; } = new();
    public string[] Roles => UserAccount.Roles;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    public partial string? StatusMessage { get; set; }

    [ObservableProperty] public partial bool IsStatusError { get; set; }
    [ObservableProperty] public partial string CountText { get; set; } = "0 akun";

    [ObservableProperty] public partial UserAccount? Target { get; set; }
    [ObservableProperty] public partial bool IsDialogOpen { get; set; }
    [ObservableProperty] public partial string DialogTitle { get; set; } = string.Empty;
    [ObservableProperty] public partial string DialogMessage { get; set; } = string.Empty;
    [ObservableProperty] public partial string DialogConfirmText { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsRoleDialog { get; set; }
    [ObservableProperty] public partial bool IsDangerAction { get; set; }
    [ObservableProperty] public partial string SelectedRole { get; set; } = UserAccount.CustomerRole;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDialogError))]
    public partial string? DialogError { get; set; }

    /// <summary>Password baru hasil reset, ditampilkan sekali di dialog.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNewPassword))]
    [NotifyPropertyChangedFor(nameof(IsConfirmStep))]
    public partial string? NewPassword { get; set; }

    public bool HasStatus => !string.IsNullOrWhiteSpace(StatusMessage);
    public bool HasDialogError => !string.IsNullOrWhiteSpace(DialogError);
    public bool HasNewPassword => !string.IsNullOrEmpty(NewPassword);
    public bool IsConfirmStep => !HasNewPassword;

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            var users = await _users.GetUsersAsync();
            Users.Clear();
            foreach (var user in users)
                Users.Add(user);
            CountText = $"{users.Count} akun";
        }
        catch (Exception exception)
        {
            SetStatus($"Gagal memuat pengguna: {DatabaseErrorMessage.Describe(exception)}", isError: true);
        }
    }

    [RelayCommand]
    private void OpenRole(UserAccount? user)
    {
        if (user is null || IsSelf(user, "mengubah peran")) return;
        SelectedRole = Array.Find(UserAccount.Roles,
            r => string.Equals(r, user.Role.Trim(), StringComparison.OrdinalIgnoreCase)) ?? UserAccount.CustomerRole;
        Open(user, UserAction.Role, "Ubah Peran",
            $"Pilih peran baru untuk {user.FullName} (@{user.Username}). Berlaku saat ia masuk lagi.",
            "Simpan peran", danger: false);
    }

    [RelayCommand]
    private void OpenReset(UserAccount? user)
    {
        if (user is null) return;
        Open(user, UserAction.Reset, "Reset Password",
            $"Buat password acak baru untuk {user.FullName} (@{user.Username})? Password lama langsung tidak berlaku.",
            "Reset password", danger: true);
    }

    [RelayCommand]
    private void OpenToggleActive(UserAccount? user)
    {
        if (user is null || IsSelf(user, "menonaktifkan")) return;
        if (user.IsActive)
            Open(user, UserAction.Deactivate, "Nonaktifkan Akun",
                $"{user.FullName} (@{user.Username}) tidak bisa masuk lagi sampai diaktifkan kembali. Data penjualan dan batch tetap tersimpan.",
                "Nonaktifkan", danger: true);
        else
            Open(user, UserAction.Activate, "Aktifkan Akun",
                $"Izinkan {user.FullName} (@{user.Username}) masuk lagi?", "Aktifkan", danger: false);
    }

    [RelayCommand]
    private void CloseDialog()
    {
        IsDialogOpen = false;
        NewPassword = null;
        DialogError = null;
        Target = null;
    }

    [RelayCommand]
    private async Task ConfirmAsync()
    {
        if (Target is not { } user || _isSaving) return;

        _isSaving = true;
        DialogError = null;
        try
        {
            switch (_pendingAction)
            {
                case UserAction.Role:
                    await _users.UpdateRoleAsync(user.Id, SelectedRole);
                    SetStatus($"Peran {user.FullName} sekarang {SelectedRole}.");
                    break;
                case UserAction.Deactivate:
                case UserAction.Activate:
                    var activate = _pendingAction == UserAction.Activate;
                    await _users.SetActiveAsync(user.Id, activate);
                    SetStatus($"Akun {user.FullName} {(activate ? "diaktifkan" : "dinonaktifkan")}.");
                    break;
                case UserAction.Reset:
                    // Dialog tetap terbuka supaya admin bisa menyalin password barunya.
                    NewPassword = await _users.ResetPasswordAsync(user.Id);
                    DialogTitle = "Password Baru";
                    DialogMessage = $"Berikan password ini ke {user.FullName}. Password tidak akan ditampilkan lagi.";
                    SetStatus($"Password {user.FullName} direset.");
                    return;
            }

            CloseDialog();
        }
        catch (Exception exception)
        {
            DialogError = DatabaseErrorMessage.Describe(exception);
        }
        finally
        {
            _isSaving = false;
            await LoadAsync();
        }
    }

    [RelayCommand]
    private void DismissStatus() => StatusMessage = null;

    private bool IsSelf(UserAccount user, string what)
    {
        if (user.Id != CurrentUserId) return false;
        SetStatus($"Tidak bisa {what} akun sendiri. Minta admin lain.", isError: true);
        return true;
    }

    private void Open(UserAccount user, UserAction action, string title, string message, string confirm, bool danger)
    {
        Target = user;
        _pendingAction = action;
        DialogTitle = title;
        DialogMessage = message;
        DialogConfirmText = confirm;
        IsRoleDialog = action == UserAction.Role;
        IsDangerAction = danger;
        DialogError = null;
        NewPassword = null;
        IsDialogOpen = true;
    }

    private void SetStatus(string message, bool isError = false)
    {
        IsStatusError = isError;
        StatusMessage = message;
    }
}
