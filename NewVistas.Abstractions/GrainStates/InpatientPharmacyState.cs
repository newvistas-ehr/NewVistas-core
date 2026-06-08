// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// One drug component of an IV order (additive or base solution label).
/// Mirrors VistA IV ADDITIVES sub-file of File #55.
/// </summary>
[GenerateSerializer]
public class IvAdditive
{
    [Id(0)]
    public string DrugName { get; set; } = string.Empty;

    [Id(1)]
    public string? DrugId { get; set; }

    /// <summary>
    /// Dose amount (e.g. "1", "20", "500")
    /// </summary>
    [Id(2)]
    public string Dose { get; set; } = string.Empty;

    /// <summary>
    /// Dose unit (e.g. "GM", "mEq", "mg", "units")
    /// </summary>
    [Id(3)]
    public string DoseUnit { get; set; } = string.Empty;

    /// <summary>
    /// True = primary drug additive; false = secondary additive
    /// </summary>
    [Id(4)]
    public bool IsBase { get; set; }
}

/// <summary>
/// Persistent state for an inpatient pharmacy order (VistA File #55 inpatient sub-file).
/// Covers Unit Dose (UD), IV admixture, and Large Volume Parenteral (LVP) order types.
/// </summary>
[GenerateSerializer]
public class InpatientOrderState
{
    /// <summary>Grain key — assigned from GetPrimaryKeyString() on first activation</summary>
    [Id(0)]
    public string OrderId { get; set; } = string.Empty;

    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>Pointer to ADT admission grain (optional)</summary>
    [Id(2)]
    public string? AdmissionId { get; set; }

    [Id(3)]
    public string WardId { get; set; } = string.Empty;

    [Id(4)]
    public string WardName { get; set; } = string.Empty;

    /// <summary>Room and bed, e.g. "4B-12"</summary>
    [Id(5)]
    public string? RoomBed { get; set; }

    /// <summary>Order type: UNIT_DOSE | IV | LVP</summary>
    [Id(6)]
    public string OrderType { get; set; } = "UNIT_DOSE";

    /// <summary>Drug name (.01) — primary drug or solution description for LVP</summary>
    [Id(7)]
    public string DrugName { get; set; } = string.Empty;

    /// <summary>Pointer to DRUG file (#50)</summary>
    [Id(8)]
    public string? DrugId { get; set; }

    /// <summary>Dosage amount string (e.g. "25MG", "1GM")</summary>
    [Id(9)]
    public string? Dosage { get; set; }

    /// <summary>Unit of measure for the dose (e.g. MG, MCG, GM)</summary>
    [Id(10)]
    public string? DoseUnit { get; set; }

    [Id(11)]
    public string? Route { get; set; }

    /// <summary>Administration schedule code: BID, Q4H, PRN, QD, QHS, etc.</summary>
    [Id(12)]
    public string? Schedule { get; set; }

    /// <summary>Order status: PENDING | ACTIVE | DISCONTINUED | EXPIRED | HOLD | ON_CALL</summary>
    [Id(13)]
    public string Status { get; set; } = "PENDING";

    /// <summary>Priority: ROUTINE | STAT | NOW</summary>
    [Id(14)]
    public string Priority { get; set; } = "ROUTINE";

    [Id(15)]
    public DateTime? StartDate { get; set; }

    [Id(16)]
    public DateTime? StopDate { get; set; }

    /// <summary>Therapy duration in days (used to calculate StopDate)</summary>
    [Id(17)]
    public int? DurationDays { get; set; }

    /// <summary>Doses per dispense event (unit dose orders)</summary>
    [Id(18)]
    public int? QuantityPerDose { get; set; }

    /// <summary>True once a pharmacist has verified the order</summary>
    [Id(19)]
    public bool IsVerified { get; set; }

    [Id(20)]
    public string? VerifiedBy { get; set; }

    [Id(21)]
    public DateTime? VerifiedDate { get; set; }

    [Id(22)]
    public string? ProviderId { get; set; }

    [Id(23)]
    public string? ProviderName { get; set; }

    [Id(24)]
    public string? PharmacistId { get; set; }

    [Id(25)]
    public string? PharmacistName { get; set; }

    [Id(26)]
    public string? Comments { get; set; }

    /// <summary>Reason stored on discontinue (dedicated field, not Comments)</summary>
    [Id(27)]
    public string? DiscontinueReason { get; set; }

    /// <summary>Reason stored on hold (dedicated field, not Comments)</summary>
    [Id(28)]
    public string? HoldReason { get; set; }

    /// <summary>IV solution/diluent label, e.g. "NS" / "D5W" / "LR"</summary>
    [Id(29)]
    public string? IvSolution { get; set; }

    /// <summary>IV bag volume in mL (e.g. 50, 100, 250, 500, 1000)</summary>
    [Id(30)]
    public int? IvVolumeMl { get; set; }

    /// <summary>Infusion rate string, e.g. "125 mL/hr" or "over 60 min"</summary>
    [Id(31)]
    public string? InfusionRateStr { get; set; }

    /// <summary>IV drug additives (for IV and LVP order types)</summary>
    [Id(32)]
    public List<IvAdditive> IvAdditives { get; set; } = new();

    /// <summary>Scheduled administration date/times for this order</summary>
    [Id(33)]
    public List<DateTime> ScheduledAdminTimes { get; set; } = new();

    /// <summary>BCMA administration event IDs linked to this order</summary>
    [Id(34)]
    public List<string> BcmaAdministrationIds { get; set; } = new();

    [Id(35)]
    public DateTime? LastAdminDate { get; set; }

    [Id(36)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Id(37)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    // ── Route / dose-form validation (RxNorm-derived, warn-only) ─────────────

    /// <summary>
    /// Advisory set when the order's Route is not a typical route for the drug's
    /// dose form. Warn-only — the order is still created. Null when the route is
    /// valid or could not be evaluated (unknown/unmapped dose form).
    /// </summary>
    [Id(38)]
    public string? RouteValidationWarning { get; set; }

    /// <summary>
    /// Suggested valid routes for the drug's dose form, shown alongside
    /// <see cref="RouteValidationWarning"/>. Empty when there is no warning.
    /// </summary>
    [Id(39)]
    public List<string> RouteSuggestions { get; set; } = new();
}

/// <summary>
/// Lightweight projection stored in the per-patient inpatient medication profile index.
/// </summary>
[GenerateSerializer]
public class InpatientOrderIndexEntry
{
    [Id(0)]
    public string OrderId { get; set; } = string.Empty;

    [Id(1)]
    public string DrugName { get; set; } = string.Empty;

    [Id(2)]
    public string? DrugId { get; set; }

    /// <summary>UNIT_DOSE | IV | LVP</summary>
    [Id(3)]
    public string OrderType { get; set; } = string.Empty;

    [Id(4)]
    public string Status { get; set; } = string.Empty;

    [Id(5)]
    public string Priority { get; set; } = "ROUTINE";

    [Id(6)]
    public string? Schedule { get; set; }

    [Id(7)]
    public string? Dosage { get; set; }

    [Id(8)]
    public DateTime? StartDate { get; set; }

    [Id(9)]
    public DateTime? StopDate { get; set; }

    [Id(10)]
    public bool IsVerified { get; set; }

    [Id(11)]
    public string? ProviderName { get; set; }

    [Id(12)]
    public string? WardName { get; set; }

    [Id(13)]
    public string? RoomBed { get; set; }

    [Id(14)]
    public DateTime? LastAdminDate { get; set; }
}

/// <summary>
/// Persistent state for the per-patient inpatient medication profile index grain.
/// Keyed by "PSJ-PROFILE:{patientId}".
/// </summary>
[GenerateSerializer]
public class PatientInpatientProfileState
{
    [Id(0)]
    public List<InpatientOrderIndexEntry> Orders { get; set; } = new();

    [Id(1)]
    public int TotalOrders { get; set; }
}
