using System;

namespace SIKOPY.Models;

public sealed class UserAccount
{
    /// <summary>Role given to self-registered accounts. Every other role is staff.</summary>
    public const string CustomerRole = "Pelanggan";

    public int Id { get; init; }
    public string Username { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;

    public bool IsCustomer => string.Equals(Role.Trim(), CustomerRole, StringComparison.OrdinalIgnoreCase);

    public string Initial => string.IsNullOrWhiteSpace(FullName)
        ? "?"
        : FullName.Trim()[0].ToString().ToUpperInvariant();
}
