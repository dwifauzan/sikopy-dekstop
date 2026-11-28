using System.Collections.Generic;

namespace SIKOPY.Models;

/// <summary>Extra data shown in the batch detail dialog.</summary>
public sealed class RoastingBatchDetail
{
    public string CreatedBy { get; init; } = string.Empty;
    public RoastedBean? Product { get; init; }
    public IReadOnlyList<StockHistoryEntry> History { get; init; } = [];

    public string ProductStockDisplay => Product?.StockDisplay ?? "-";
    public string ProductPriceDisplay => Product?.PriceDisplay ?? "-";
}
