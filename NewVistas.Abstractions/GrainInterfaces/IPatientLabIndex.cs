// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Lightweight metadata index that resolves queries like "last N results of type X"
/// into specific batch grain references. Uses pull-on-activate pattern — reconstructs
/// from batch grains when activated, stays current via Orleans Streams while hot.
///
/// Grain Key: PatientLabIndex/{patientIcn}
/// </summary>
public interface IPatientLabIndex : IGrainWithStringKey
{
    /// <summary>
    /// Get the last N results for a specific test type.
    /// Returns index entries with batch grain references.
    /// </summary>
    Task<IReadOnlyList<LabIndexEntry>> GetLastNByType(string loincCode, int count);

    /// <summary>
    /// Get all index entries within a date range.
    /// </summary>
    Task<IReadOnlyList<LabIndexEntry>> GetByDateRange(DateTimeOffset from, DateTimeOffset to);

    /// <summary>
    /// Get all distinct test types this patient has had.
    /// </summary>
    Task<IReadOnlyCollection<string>> GetDistinctTestTypes();

    /// <summary>
    /// Get all abnormal results within a date range.
    /// </summary>
    Task<IReadOnlyList<LabIndexEntry>> GetAbnormalByDateRange(DateTimeOffset from, DateTimeOffset to);

    /// <summary>
    /// Force a rebuild of the index from batch grains.
    /// Normally not needed — index auto-rebuilds on activation.
    /// </summary>
    Task RebuildIndex();
}
