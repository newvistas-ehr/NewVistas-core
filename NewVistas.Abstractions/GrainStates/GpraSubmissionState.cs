// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Lifecycle status of a GPRA submission.
/// </summary>
[GenerateSerializer]
public enum GpraSubmissionStatus
{
    /// <summary>Submission record exists but no file has been packaged yet.</summary>
    Pending = 0,

    /// <summary>The submission file has been written; awaiting operator transmission to IHS.</summary>
    Packaged = 1,

    /// <summary>Operator recorded the file as transmitted to IHS (e.g., uploaded to the IHS portal).</summary>
    Submitted = 2,

    /// <summary>IHS validated the file and accepted it.</summary>
    Accepted = 3,

    /// <summary>IHS rejected the file (validation errors). Operator should re-package and re-submit.</summary>
    Rejected = 4,
}

/// <summary>
/// Tracks the packaging + transmission lifecycle of a single GPRA submission
/// to the IHS national office. One <see cref="GpraSubmissionState"/> per
/// <see cref="GpraReportState"/>; the submission grain is keyed
/// <c>"GPRA-SUB:{reportId}"</c>.
/// </summary>
[GenerateSerializer]
public class GpraSubmissionState
{
    /// <summary>Grain key (matches the source report's grain key suffix).</summary>
    [Id(0)] public string SubmissionId { get; set; } = string.Empty;

    /// <summary>The GPRA report this submission packages (<c>GpraReportState.ReportId</c>).</summary>
    [Id(1)] public string ReportId { get; set; } = string.Empty;

    /// <summary>Current lifecycle status.</summary>
    [Id(2)] public GpraSubmissionStatus Status { get; set; } = GpraSubmissionStatus.Pending;

    /// <summary>Absolute path to the packaged submission file. Null until packaging completes.</summary>
    [Id(3)] public string? FilePath { get; set; }

    /// <summary>Format version stamped by <see cref="Reporting.IGpraSubmissionFormatter.FormatVersion"/>.</summary>
    [Id(4)] public string? FormatVersion { get; set; }

    /// <summary>Size of the packaged file in bytes. Useful for IHS submission portals that require a checksum/size.</summary>
    [Id(5)] public long? FileSizeBytes { get; set; }

    /// <summary>SHA-256 hex digest of the file contents. Lets IHS validate integrity and lets operators detect re-packaging without content change.</summary>
    [Id(6)] public string? FileSha256 { get; set; }

    /// <summary>When the file was packaged.</summary>
    [Id(7)] public DateTime? PackagedDate { get; set; }

    /// <summary>User who triggered packaging.</summary>
    [Id(8)] public string? PackagedById { get; set; }

    /// <summary>Display name of the user who triggered packaging.</summary>
    [Id(9)] public string? PackagedByName { get; set; }

    /// <summary>When the operator recorded the file as transmitted to IHS.</summary>
    [Id(10)] public DateTime? TransmissionDate { get; set; }

    /// <summary>Optional tracking number returned by the IHS submission portal.</summary>
    [Id(11)] public string? TransmissionTrackingId { get; set; }

    /// <summary>When the IHS response (acceptance or rejection) was recorded.</summary>
    [Id(12)] public DateTime? IhsResponseDate { get; set; }

    /// <summary>True if IHS accepted the submission; false if rejected. Null until a response is recorded.</summary>
    [Id(13)] public bool? IhsAccepted { get; set; }

    /// <summary>Verbatim IHS response receipt or rejection reason. Free-text from the operator.</summary>
    [Id(14)] public string? IhsResponseReceipt { get; set; }

    /// <summary>How many times this submission has been packaged (re-packaging increments).</summary>
    [Id(15)] public int PackagingAttempts { get; set; }

    [Id(16)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    [Id(17)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
