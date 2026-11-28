using System;

namespace SIKOPY.Models;

public sealed class UserAccount
{
    /// <summary>Role given to self-registered accounts. Every other role is staff.</summary>
    public const string CustomerRole = "Pelanggan";
    public const string StaffRole = "Staf";
    public const string AdminRole = "Admin";
    public static readonly string[] Roles = { CustomerRole, StaffRole, AdminRole };

    public int Id { get; init; }
    public string Username { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public bool IsActive { get; init; } = true;

    public bool IsAdmin => string.Equals(Role.Trim(), AdminRole, StringComparison.OrdinalIgnoreCase);
    public bool IsCustomer => string.Equals(Role.Trim(), CustomerRole, StringComparison.OrdinalIgnoreCase);

    public string StatusDisplay => IsActive ? "Aktif" : "Nonaktif";
    public string ToggleActiveText => IsActive ? "Nonaktifkan" : "Aktifkan";

    public string Initial => string.IsNullOrWhiteSpace(FullName)
        ? "?"
        : FullName.Trim()[0].ToString().ToUpperInvariant();
}
