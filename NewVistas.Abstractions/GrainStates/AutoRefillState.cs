// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for an automated prescription refill enrollment.
/// Extends VistA outpatient pharmacy (File #52) with automated refill scheduling
/// that VistA lacks — calculates next refill date from days supply and generates
/// refill requests before the patient runs out.
/// </summary>
[GenerateSerializer]
public class AutoRefillState
{
    /// <summary>Unique enrollment ID (grain key, e.g., "RX-AUTOREFILL:{guid}").</summary>
    [Id(0)]
    public string EnrollmentId { get; set; } = string.Empty;

    /// <summary>Patient enrolled in auto-refill.</summary>
    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient name for display.</summary>
    [Id(2)]
    public string PatientName { get; set; } = string.Empty;

    /// <summary>Prescription ID this auto-refill is linked to (VistA File #52 IEN).</summary>
    [Id(3)]
    public string PrescriptionId { get; set; } = string.Empty;

    /// <summary>Drug name for display.</summary>
    [Id(4)]
    public string DrugName { get; set; } = string.Empty;

    /// <summary>VA drug class (e.g., CV100 - Beta Blockers).</summary>
    [Id(5)]
    public string DrugClass { get; set; } = string.Empty;

    /// <summary>Days supply per fill.</summary>
    [Id(6)]
    public int DaysSupply { get; set; }

    /// <summary>Refills remaining on the prescription.</summary>
    [Id(7)]
    public int RefillsRemaining { get; set; }

    /// <summary>Date of the most recent fill.</summary>
    [Id(8)]
    public DateTime LastFillDate { get; set; }

    /// <summary>Calculated next refill date (LastFillDate + DaysSupply - LeadTimeDays).</summary>
    [Id(9)]
    public DateTime NextRefillDate { get; set; }

    /// <summary>Days before supply runs out to generate the refill request.</summary>
    [Id(10)]
    public int LeadTimeDays { get; set; } = 7;

    /// <summary>Pharmacy filling this prescription.</summary>
    [Id(11)]
    public string PharmacyId { get; set; } = string.Empty;

    /// <summary>Pharmacy name for display.</summary>
    [Id(12)]
    public string PharmacyName { get; set; } = string.Empty;

    /// <summary>Status: ACTIVE, SUSPENDED, EXPIRED, DISENROLLED, REFILL_PENDING, NO_REFILLS.</summary>
    [Id(13)]
    public string Status { get; set; } = "ACTIVE";

    /// <summary>Provider who enrolled the patient.</summary>
    [Id(14)]
    public string EnrolledByProviderId { get; set; } = string.Empty;

    /// <summary>Provider name who enrolled the patient.</summary>
    [Id(15)]
    public string EnrolledByProviderName { get; set; } = string.Empty;

    /// <summary>Suspension/disenrollment reason.</summary>
    [Id(16)]
    public string? SuspendReason { get; set; }

    /// <summary>Total number of auto-refills generated.</summary>
    [Id(17)]
    public int TotalRefillsGenerated { get; set; }

    /// <summary>History of auto-refill events.</summary>
    [Id(18)]
    public List<AutoRefillEvent> RefillHistory { get; set; } = new();

    [Id(19)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Id(20)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Record of an auto-refill event (request generated, dispensed, etc.).
/// </summary>
[GenerateSerializer]
public class AutoRefillEvent
{
    [Id(0)]
    public DateTime EventDate { get; set; }

    /// <summary>Event type: ENROLLED, FILL_RECORDED, REFILL_REQUESTED, REFILL_DISPENSED, SUSPENDED, RESUMED, DISENROLLED, EXPIRED.</summary>
    [Id(1)]
    public string EventType { get; set; } = string.Empty;

    [Id(2)]
    public string PerformedByName { get; set; } = string.Empty;

    [Id(3)]
    public string? Details { get; set; }
}

/// <summary>
/// Index entry for auto-refill enrollments.
/// </summary>
[GenerateSerializer]
public class AutoRefillIndexEntry
{
    [Id(0)]
    public string EnrollmentId { get; set; } = string.Empty;

    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    [Id(2)]
    public string PatientName { get; set; } = string.Empty;

    [Id(3)]
    public string PrescriptionId { get; set; } = string.Empty;

    [Id(4)]
    public string DrugName { get; set; } = string.Empty;

    [Id(5)]
    public string Status { get; set; } = string.Empty;

    [Id(6)]
    public DateTime NextRefillDate { get; set; }

    [Id(7)]
    public int RefillsRemaining { get; set; }

    [Id(8)]
    public string PharmacyId { get; set; } = string.Empty;

    [Id(9)]
    public string PharmacyName { get; set; } = string.Empty;

    [Id(10)]
    public int TotalRefillsGenerated { get; set; }
}

/// <summary>
/// Persistent state for the auto-refill index singleton.
/// </summary>
[GenerateSerializer]
public class AutoRefillIndexState
{
    [Id(0)]
    public Dictionary<string, AutoRefillIndexEntry> Entries { get; set; } = new();
}
