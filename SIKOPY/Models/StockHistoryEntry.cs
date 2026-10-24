using System;
using System.Globalization;

namespace SIKOPY.Models;

public sealed class StockHistoryEntry
{
    private static readonly CultureInfo Indonesian = CultureInfo.GetCultureInfo("id-ID");

    public int Id { get; init; }
    public string MaterialName { get; init; } = string.Empty;
    public string Direction { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public string Unit { get; init; } = string.Empty;
    public string? Reference { get; init; }
    public DateTime OccurredAt { get; init; }

    public bool IsIncoming => Direction == "MASUK";
    public string QuantityDisplay =>
        $"{(IsIncoming ? "+" : "-")}{Quantity.ToString("0.##", Indonesian)} {Unit.ToLowerInvariant()}";
    public string ReferenceDisplay => string.IsNullOrWhiteSpace(Reference) ? "-" : Reference;
    public string OccurredAtDisplay => OccurredAt.ToString("dd MMM yyyy, HH\\:mm", Indonesian);
}
