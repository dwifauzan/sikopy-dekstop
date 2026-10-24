using System;

namespace SIKOPY.Database;

/// <summary>
/// Thrown when a raw material cannot be deleted because a roasting batch uses it.
/// </summary>
public sealed class RawMaterialInUseException : InvalidOperationException
{
    public RawMaterialInUseException(long batchCount)
        : base($"The raw material is used by {batchCount} roasting batch(es) and cannot be deleted.")
    {
        BatchCount = batchCount;
    }

    public long BatchCount { get; }
}
