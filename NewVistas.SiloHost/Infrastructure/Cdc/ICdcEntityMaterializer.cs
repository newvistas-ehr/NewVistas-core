// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.Data.SqlClient;
using Orleans;

namespace NewVistas.SiloHost.Infrastructure.Cdc;

/// <summary>
/// Strategy interface for materializing a specific grain type into the reporting star schema.
/// Each clinical domain (Patient, Order, LabTest, etc.) has its own implementation.
/// </summary>
public interface ICdcEntityMaterializer
{
    /// <summary>Name used in CDCWatermark table, logging, and metrics.</summary>
    string EntityName { get; }

    /// <summary>
    /// The GrainTypeString pattern to match in OrleansStorage (SQL LIKE clause).
    /// Example: "%PatientGrain,%" to match the full type name.
    /// </summary>
    string GrainTypePattern { get; }

    /// <summary>Processing order — lower values run first. Dimensions before facts.</summary>
    int Priority { get; }

    /// <summary>
    /// Materializes a batch of changed grains into the star schema.
    /// Returns the number of rows upserted/inserted.
    /// </summary>
    Task<int> MaterializeAsync(
        IReadOnlyList<ChangedGrainInfo> changedGrains,
        SqlConnection reportingConnection,
        IGrainFactory grainFactory,
        CancellationToken ct);
}

/// <summary>
/// Lightweight record returned by the watermark query — just enough to identify the grain.
/// </summary>
public record ChangedGrainInfo(string GrainKey, DateTime ModifiedOn);
