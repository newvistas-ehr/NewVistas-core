// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for an incomplete medical record / chart deficiency.
/// Maps to VistA DGPT (Incomplete Records Tracking) package.
/// Tracks unsigned notes, missing discharge summaries, incomplete H&amp;Ps,
/// and other documentation deficiencies required by Joint Commission.
/// Key: "DGPT:{deficiencyId}"
/// </summary>
[GenerateSerializer]
public class IncompleteRecordState
{
    /// <summary>Unique deficiency identifier.</summary>
    [Id(0)] public string DeficiencyId { get; set; } = string.Empty;

    /// <summary>Patient ID.</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient name.</summary>
    [Id(2)] public string PatientName { get; set; } = string.Empty;

    /// <summary>Responsible provider ID.</summary>
    [Id(3)] public string ProviderId { get; set; } = string.Empty;

    /// <summary>Responsible provider name.</summary>
    [Id(4)] public string ProviderName { get; set; } = string.Empty;

    /// <summary>Deficiency type: UNSIGNED_NOTE, MISSING_DISCHARGE_SUMMARY, MISSING_HP, MISSING_OP_NOTE, UNSIGNED_ORDER, MISSING_CONSENT, OTHER.</summary>
    [Id(5)] public string DeficiencyType { get; set; } = string.Empty;

    /// <summary>Description of what's missing/incomplete.</summary>
    [Id(6)] public string Description { get; set; } = string.Empty;

    /// <summary>Associated document ID (TIU, order, etc.) if applicable.</summary>
    [Id(7)] public string? DocumentId { get; set; }

    /// <summary>Associated admission/visit ID.</summary>
    [Id(8)] public string? AdmissionId { get; set; }

    /// <summary>Discharge date (for post-discharge deficiencies).</summary>
    [Id(9)] public DateTime? DischargeDate { get; set; }

    /// <summary>Status: OPEN, COMPLETED, WAIVED, DELINQUENT.</summary>
    [Id(10)] public string Status { get; set; } = "OPEN";

    /// <summary>Days since discharge (or creation) — used for delinquency tracking.</summary>
    [Id(11)] public int DaysOutstanding { get; set; }

    /// <summary>Delinquent after N days (Joint Commission: typically 30 days post-discharge).</summary>
    [Id(12)] public int DelinquentAfterDays { get; set; } = 30;

    /// <summary>Date deficiency was completed/resolved.</summary>
    [Id(13)] public DateTime? CompletedDate { get; set; }

    /// <summary>Completed by user ID.</summary>
    [Id(14)] public string? CompletedBy { get; set; }

    /// <summary>Waiver reason if waived.</summary>
    [Id(15)] public string? WaiverReason { get; set; }

    /// <summary>Created date.</summary>
    [Id(16)] public DateTime CreatedDate { get; set; }

    /// <summary>Last modified.</summary>
    [Id(17)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>
/// Lightweight deficiency entry for index display.
/// </summary>
[GenerateSerializer]
public class IncompleteRecordEntry
{
    /// <summary>Deficiency ID.</summary>
    [Id(0)] public string DeficiencyId { get; set; } = string.Empty;

    /// <summary>Patient name.</summary>
    [Id(1)] public string PatientName { get; set; } = string.Empty;

    /// <summary>Provider name.</summary>
    [Id(2)] public string ProviderName { get; set; } = string.Empty;

    /// <summary>Deficiency type.</summary>
    [Id(3)] public string DeficiencyType { get; set; } = string.Empty;

    /// <summary>Status.</summary>
    [Id(4)] public string Status { get; set; } = string.Empty;

    /// <summary>Days outstanding.</summary>
    [Id(5)] public int DaysOutstanding { get; set; }

    /// <summary>Is delinquent.</summary>
    [Id(6)] public bool IsDelinquent { get; set; }

    /// <summary>Discharge date.</summary>
    [Id(7)] public DateTime? DischargeDate { get; set; }
}

/// <summary>
/// State for provider's incomplete records index.
/// Key: "DGPT-INDEX:{providerId}"
/// </summary>
[GenerateSerializer]
public class IncompleteRecordIndexState
{
    /// <summary>All deficiencies assigned to this provider.</summary>
    [Id(0)] public List<IncompleteRecordEntry> Deficiencies { get; set; } = new();
}
