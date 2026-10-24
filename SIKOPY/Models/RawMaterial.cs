using System;
using System.Globalization;

namespace SIKOPY.Models;

public sealed class RawMaterial
{
    /// <summary>Stock below this amount is highlighted as running low.</summary>
    public const decimal LowStockThresholdKilograms = 50m;

    private static readonly CultureInfo Indonesian = CultureInfo.GetCultureInfo("id-ID");

    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int TypeId { get; init; }
    public string TypeName { get; init; } = string.Empty;
    public int ProcessingMethodId { get; init; }
    public string ProcessingMethodName { get; init; } = string.Empty;
    public string? Origin { get; init; }
    public int? AltitudeMeters { get; init; }
    public string? Quality { get; init; }
    public decimal StockKilograms { get; init; }
    public decimal PricePerKilogram { get; init; }
    public DateOnly? HarvestDate { get; init; }
    public bool IsActive { get; init; }

    public bool IsLowStock => StockKilograms < LowStockThresholdKilograms;
    public bool HasGrade => !string.IsNullOrWhiteSpace(Quality);

    public string OriginDisplay => string.IsNullOrWhiteSpace(Origin) ? "-" : Origin;
    public string StockDisplay => $"{StockKilograms.ToString("0.##", Indonesian)} kg";
    public string AltitudeDisplay => AltitudeMeters is null ? "-" : $"{AltitudeMeters} mdpl";
    public string GradeDisplay => string.IsNullOrWhiteSpace(Quality) ? "-" : Quality;
    public string PriceDisplay => $"Rp {PricePerKilogram.ToString("N0", Indonesian)}";
    public string HarvestDisplay => HarvestDate?.ToString("MMM yyyy", Indonesian) ?? "-";
}
