// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

// ─── Fee vendor index entry ────────────────────────────────────────────────────

/// <summary>Lightweight summary of a fee basis vendor for index lookups.</summary>
[GenerateSerializer]
public record FeeVendorIndexEntry
{
    /// <summary>Unique vendor identifier.</summary>
    [Id(0)] public string VendorId { get; init; } = string.Empty;

    /// <summary>Vendor display name.</summary>
    [Id(1)] public string VendorName { get; init; } = string.Empty;

    /// <summary>Vendor type (enum name string: Individual or Organization).</summary>
    [Id(2)] public string VendorType { get; init; } = string.Empty;

    /// <summary>Human-readable specialty name (optional).</summary>
    [Id(3)] public string? SpecialtyName { get; init; }

    /// <summary>Whether this vendor is currently active.</summary>
    [Id(4)] public bool IsActive { get; init; }
}

// ─── Fee vendor index state — singleton ───────────────────────────────────────

/// <summary>
/// Singleton index of all registered fee basis vendors.
/// Keyed as "FEE-VENDOR-IDX".
/// </summary>
[GenerateSerializer]
public class FeeVendorIndexState
{
    /// <summary>All fee basis vendor summaries.</summary>
    [Id(0)] public List<FeeVendorIndexEntry> Entries { get; set; } = new();
}
