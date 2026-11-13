namespace SIKOPY.Models;

public sealed class UserAccount
{
    public int Id { get; init; }
    public string Username { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;

    public string Initial => string.IsNullOrWhiteSpace(FullName)
        ? "?"
        : FullName.Trim()[0].ToString().ToUpperInvariant();
}
