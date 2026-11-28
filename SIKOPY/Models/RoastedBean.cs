using System.Globalization;

namespace SIKOPY.Models;

/// <summary>A roast bean product made by one roasting batch.</summary>
public sealed class RoastedBean
{
    private static readonly CultureInfo Indonesian = CultureInfo.GetCultureInfo("id-ID");

    public int Id { get; init; }
    public string BatchCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public decimal StockGram { get; init; }
    public decimal PricePerGram { get; init; }

    public bool HasPrice => PricePerGram > 0;
    public decimal PricePerKilogram => PricePerGram * 1000m;

    public string StockDisplay => $"{(StockGram / 1000m).ToString("0.00", Indonesian)} kg";
    public string PriceDisplay => HasPrice
        ? $"Rp {PricePerKilogram.ToString("N0", Indonesian)} / kg"
        : "Belum diatur";
}
