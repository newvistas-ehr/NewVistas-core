// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Entry point for lab result ingestion. Receives a new lab result
/// (from HL7 feed, VistA extract, etc.) and orchestrates the minimal write path:
///   1. Write to time-partitioned batch grain
///   2. Push-update the summary grain
///   3. Publish to Orleans Stream (index grain picks up IF hot)
///
/// Grain Key: LabIngestion/{patientIcn}
/// </summary>
public interface ILabResultIngestion : IGrainWithStringKey
{
    /// <summary>
    /// Ingest a single lab result. Orchestrates writes to batch + summary grains.
    /// </summary>
    Task IngestResult(LabResultDetail result);
}
