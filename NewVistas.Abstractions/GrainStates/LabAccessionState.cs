// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Specimen status in the accessioning workflow.
/// </summary>
[GenerateSerializer]
public enum SpecimenStatus
{
    Received = 0,
    Accessioned = 1,
    InProcess = 2,
    Completed = 3,
    Rejected = 4,
}

/// <summary>
/// Reason for specimen rejection.
/// Maps to VistA LR specimen rejection codes.
/// </summary>
[GenerateSerializer]
public enum SpecimenRejectReason
{
    None = 0,
    Hemolyzed = 1,
    Clotted = 2,
    QuantityNotSufficient = 3,
    WrongTube = 4,
    Unlabeled = 5,
    MislabeledPatient = 6,
    Expired = 7,
    Contaminated = 8,
    ImproperTransport = 9,
    Other = 10,
}

/// <summary>
/// State for a lab specimen accession record.
/// Maps to VistA LRACE.m accessioning — assigns unique accession numbers,
/// tracks specimen receipt through result completion.
/// Grain key: "LAB-ACCESSION:{accessionNumber}"
/// </summary>
[GenerateSerializer]
public class LabAccessionState
{
    /// <summary>Accession number — grain key. Format: YYYYMMDD-LLLL-NNNN (date-lab-sequence).</summary>
    [Id(0)] public string AccessionNumber { get; set; } = string.Empty;

    /// <summary>Patient ID.</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Lab test IDs associated with this specimen.</summary>
    [Id(2)] public List<string> LabTestIds { get; set; } = new();

    /// <summary>Specimen type (Blood, Urine, CSF, Tissue, etc.).</summary>
    [Id(3)] public string SpecimenType { get; set; } = string.Empty;

    /// <summary>Collection tube type (LAVENDER, GRAY, MARBLED, RED, BLUE, GREEN).</summary>
    [Id(4)] public string? CollectionTube { get; set; }

    /// <summary>Date/time specimen was collected.</summary>
    [Id(5)] public DateTime CollectionDateTime { get; set; }

    /// <summary>Date/time specimen was received in the lab.</summary>
    [Id(6)] public DateTime ReceivedDateTime { get; set; }

    /// <summary>Date/time the accession number was assigned.</summary>
    [Id(7)] public DateTime? AccessionedDateTime { get; set; }

    /// <summary>Current specimen status.</summary>
    [Id(8)] public SpecimenStatus Status { get; set; }

    /// <summary>Lab section receiving the specimen (HEMATOLOGY, CHEMISTRY, MICROBIOLOGY, etc.).</summary>
    [Id(9)] public string? LabSection { get; set; }

    /// <summary>Technologist who accessioned the specimen.</summary>
    [Id(10)] public string? AccessionedByUserId { get; set; }

    /// <summary>Technologist name.</summary>
    [Id(11)] public string? AccessionedByUserName { get; set; }

    /// <summary>Whether specimen was rejected.</summary>
    [Id(12)] public bool IsRejected { get; set; }

    /// <summary>Rejection reason (if rejected).</summary>
    [Id(13)] public SpecimenRejectReason RejectReason { get; set; }

    /// <summary>Rejection notes.</summary>
    [Id(14)] public string? RejectNotes { get; set; }

    /// <summary>Date/time specimen was rejected.</summary>
    [Id(15)] public DateTime? RejectedDateTime { get; set; }

    /// <summary>User who rejected the specimen.</summary>
    [Id(16)] public string? RejectedByUserId { get; set; }

    /// <summary>Whether a recollect was ordered after rejection.</summary>
    [Id(17)] public bool RecollectOrdered { get; set; }

    /// <summary>Notes.</summary>
    [Id(18)] public string? Notes { get; set; }

    /// <summary>When created.</summary>
    [Id(19)] public DateTime CreatedDate { get; set; }

    /// <summary>When last modified.</summary>
    [Id(20)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>Lightweight accession index entry.</summary>
[GenerateSerializer]
public record LabAccessionIndexEntry
{
    [Id(0)] public string AccessionNumber { get; init; } = string.Empty;
    [Id(1)] public string PatientId { get; init; } = string.Empty;
    [Id(2)] public string SpecimenType { get; init; } = string.Empty;
    [Id(3)] public SpecimenStatus Status { get; init; }
    [Id(4)] public DateTime CollectionDateTime { get; init; }
    [Id(5)] public bool IsRejected { get; init; }
    [Id(6)] public int TestCount { get; init; }
}

/// <summary>
/// Per-patient accession index.
/// Grain key: "LAB-ACCESSION-IDX:{patientId}"
/// </summary>
[GenerateSerializer]
public class LabAccessionIndexState
{
    [Id(0)] public string PatientId { get; set; } = string.Empty;
    [Id(1)] public List<LabAccessionIndexEntry> Entries { get; set; } = new();
}
