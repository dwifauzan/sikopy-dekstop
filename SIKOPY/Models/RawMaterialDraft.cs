using System;

namespace SIKOPY.Models;

/// <summary>
/// The editable fields of a raw material, used when creating or updating one.
/// The type and processing method are given by name; names that do not exist
/// yet are created when the draft is saved.
/// </summary>
public sealed record RawMaterialDraft(
    string Name,
    string TypeName,
    string ProcessingMethodName,
    string? Origin,
    int? AltitudeMeters,
    string? Quality,
    decimal PricePerKilogram,
    DateOnly? HarvestDate);
