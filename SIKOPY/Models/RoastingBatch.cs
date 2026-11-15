using System;
using System.Globalization;

namespace SIKOPY.Models;

/// <summary>Data yang diisi di form Tambah Batch.</summary>
public sealed record RoastingBatchDraft(
    int RawMaterialId,
    decimal UsedKilograms,
    decimal RoastedGram,
    string RoastLevel,
    DateTime BatchDate,
    string? Notes);

/// <summary>Satu batch roasting yang ditampilkan sebagai kartu.</summary>
public sealed class RoastingBatch
{
    private static readonly CultureInfo Indonesian = CultureInfo.GetCultureInfo("id-ID");

    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string MaterialName { get; init; } = string.Empty;
    public decimal UsedGram { get; init; }
    public decimal? RoastedGram { get; init; }
    public string? RoastLevel { get; init; }
    public DateTime BatchDate { get; init; }
    public string? Notes { get; init; }

    public string LevelDisplay => string.IsNullOrWhiteSpace(RoastLevel) ? "-" : RoastLevel;
    public string DateDisplay => BatchDate.ToString("d MMM yyyy", Indonesian);
    public string NotesDisplay => string.IsNullOrWhiteSpace(Notes) ? "-" : Notes;

    public string UsedDisplay => $"{(UsedGram / 1000m).ToString("0.00", Indonesian)} kg";
    public string RoastedDisplay => RoastedGram is null
        ? "-"
        : $"{(RoastedGram.Value / 1000m).ToString("0.00", Indonesian)} kg";
    public string LossDisplay => RoastedGram is null || UsedGram <= 0
        ? "-"
        : $"{((UsedGram - RoastedGram.Value) / UsedGram * 100m).ToString("0.0", Indonesian)}%";
}