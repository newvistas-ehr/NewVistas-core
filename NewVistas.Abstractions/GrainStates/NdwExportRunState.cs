// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Lifecycle status of an NDW export run. Same shape as
/// <see cref="GpraSubmissionStatus"/>, kept distinct for audit clarity.
/// </summary>
[GenerateSerializer]
public enum NdwExportRunStatus
{
    /// <summary>Run record exists; no files packaged yet.</summary>
    Pending = 0,

    /// <summary>Files written; awaiting operator transmission to IHS NDW.</summary>
    Packaged = 1,

    /// <summary>Operator recorded the files as transmitted to IHS.</summary>
    Submitted = 2,

    /// <summary>IHS NDW validated and accepted the submission.</summary>
    Accepted = 3,

    /// <summary>IHS NDW rejected the submission. Operator should re-package and re-submit.</summary>
    Rejected = 4,
}

/// <summary>
/// Tracks one NDW export run: cohort selection, packaging, transmission,
/// IHS NDW response. Grain key: <c>"NDW-EXPORT:{runId}"</c>.
/// </summary>
[GenerateSerializer]
public class NdwExportRunState
{
    /// <summary>Grain key.</summary>
    [Id(0)] public string RunId { get; set; } = string.Empty;

    /// <summary>Current lifecycle status.</summary>
    [Id(1)] public NdwExportRunStatus Status { get; set; } = NdwExportRunStatus.Pending;

    /// <summary>Submitting facility identifier (the cluster id when single-facility).</summary>
    [Id(2)] public string FacilityId { get; set; } = string.Empty;

    /// <summary>Reporting period start (inclusive).</summary>
    [Id(3)] public DateTime PeriodStart { get; set; }

    /// <summary>Reporting period end (inclusive).</summary>
    [Id(4)] public DateTime PeriodEnd { get; set; }

    /// <summary>Absolute path to the directory holding the per-domain export files.</summary>
    [Id(5)] public string? OutputDirectory { get; set; }

    /// <summary>Stable format-version identifier from the formatter.</summary>
    [Id(6)] public string? FormatVersion { get; set; }

    /// <summary>How many patients were included in the export.</summary>
    [Id(7)] public int PatientCount { get; set; }

    /// <summary>Per-domain output files (relative paths within OutputDirectory).</summary>
    [Id(8)] public List<NdwExportFile> Files { get; set; } = new();

    /// <summary>How many times this run has been packaged (re-packaging increments).</summary>
    [Id(9)] public int PackagingAttempts { get; set; }

    /// <summary>When the files were packaged.</summary>
    [Id(10)] public DateTime? PackagedDate { get; set; }

    /// <summary>User who triggered packaging.</summary>
    [Id(11)] public string? PackagedById { get; set; }

    /// <summary>Display name of the user who triggered packaging.</summary>
    [Id(12)] public string? PackagedByName { get; set; }

    /// <summary>When the operator recorded the files as transmitted to IHS.</summary>
    [Id(13)] public DateTime? TransmissionDate { get; set; }

    /// <summary>Optional tracking number returned by the IHS NDW submission portal.</summary>
    [Id(14)] public string? TransmissionTrackingId { get; set; }

    /// <summary>When the IHS NDW response (acceptance or rejection) was recorded.</summary>
    [Id(15)] public DateTime? IhsResponseDate { get; set; }

    /// <summary>True if accepted; false if rejected; null until a response is recorded.</summary>
    [Id(16)] public bool? IhsAccepted { get; set; }

    /// <summary>Verbatim IHS NDW response receipt or rejection reason.</summary>
    [Id(17)] public string? IhsResponseReceipt { get; set; }

    [Id(18)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    [Id(19)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>One per-domain file written by the formatter (e.g., patients.csv).</summary>
[GenerateSerializer]
public class NdwExportFile
{
    /// <summary>Filename relative to <see cref="NdwExportRunState.OutputDirectory"/>.</summary>
    [Id(0)] public string FileName { get; set; } = string.Empty;

    /// <summary>Size of the file in bytes.</summary>
    [Id(1)] public long FileSizeBytes { get; set; }

    /// <summary>SHA-256 hex digest of the file contents (for IHS NDW integrity verification).</summary>
    [Id(2)] public string Sha256 { get; set; } = string.Empty;
}
