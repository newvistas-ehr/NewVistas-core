// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.EventSourcing;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Represents the state of a Pharmacy grain based on VistA PRESCRIPTION file (#52).
///
/// Inherits the clinical event-sourcing outbox from <see cref="EventSourcedStateBase"/>
/// so that causal events for this prescription are persisted atomically with
/// the projection in a single <c>WriteStateAsync</c>.
/// </summary>
[GenerateSerializer]
public class PharmacyState : EventSourcedStateBase
{
    [Id(0)]
    public string PrescriptionId { get; set; } = string.Empty;

    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>
    /// Drug (.01) � pointer to DRUG file (#50)
    /// </summary>
    [Id(2)]
    public string DrugName { get; set; } = string.Empty;

    [Id(3)]
    public string? DrugId { get; set; }

    [Id(4)]
    public string? Dosage { get; set; }

    [Id(5)]
    public string? Route { get; set; }

    [Id(6)]
    public string? Schedule { get; set; }

    /// <summary>
    /// Status � ACTIVE, DISCONTINUED, EXPIRED, HOLD, SUSPENDED
    /// </summary>
    [Id(7)]
    public string Status { get; set; } = "ACTIVE";

    [Id(8)]
    public DateTime? IssueDate { get; set; }

    [Id(9)]
    public DateTime? FillDate { get; set; }

    [Id(10)]
    public DateTime? ExpirationDate { get; set; }

    [Id(11)]
    public DateTime? LastDispenseDate { get; set; }

    [Id(12)]
    public int? DaysSupply { get; set; }

    [Id(13)]
    public int? Quantity { get; set; }

    [Id(14)]
    public int? Refills { get; set; }

    [Id(15)]
    public int? RefillsRemaining { get; set; }

    [Id(16)]
    public string? ProviderId { get; set; }

    [Id(17)]
    public string? ProviderName { get; set; }

    [Id(18)]
    public string? PharmacyId { get; set; }

    [Id(19)]
    public string? PharmacyName { get; set; }

    /// <summary>
    /// SIG (Patient Instructions)
    /// </summary>
    [Id(20)]
    public string? Sig { get; set; }

    [Id(21)]
    public string? OrderId { get; set; }

    [Id(22)]
    public string? Comments { get; set; }

    [Id(23)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Id(24)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// RxNumber — human-readable label number assigned at fill (.01A)
    /// </summary>
    [Id(25)]
    public string? RxNumber { get; set; }

    /// <summary>
    /// Priority: ROUTINE / URGENT / STAT
    /// </summary>
    [Id(26)]
    public string Priority { get; set; } = "ROUTINE";

    /// <summary>
    /// True once pharmacist has performed RPh verification step
    /// </summary>
    [Id(27)]
    public bool IsVerified { get; set; }

    /// <summary>
    /// Pharmacist who verified the Rx
    /// </summary>
    [Id(28)]
    public string? VerifiedBy { get; set; }

    /// <summary>
    /// UTC timestamp of pharmacist verification
    /// </summary>
    [Id(29)]
    public DateTime? VerifiedDate { get; set; }

    /// <summary>
    /// Whether patient counseling is required by pharmacist
    /// </summary>
    [Id(30)]
    public bool CounselingRequired { get; set; }

    /// <summary>
    /// True once the dispensing label has been printed
    /// </summary>
    [Id(31)]
    public bool IsLabelPrinted { get; set; }

    /// <summary>
    /// UTC timestamp of last label print
    /// </summary>
    [Id(32)]
    public DateTime? LabelPrintedDate { get; set; }

    /// <summary>
    /// NDC of the specific lot dispensed (may differ from formulary NDC)
    /// </summary>
    [Id(33)]
    public string? NdcDispensed { get; set; }

    /// <summary>
    /// Lot number of the dispensed product
    /// </summary>
    [Id(34)]
    public string? LotNumber { get; set; }

    /// <summary>
    /// Reason stored when Status = DISCONTINUED (dedicated field, not Comments)
    /// </summary>
    [Id(35)]
    public string? DiscontinueReason { get; set; }

    /// <summary>
    /// Reason stored when Status = HOLD (dedicated field, not Comments)
    /// </summary>
    [Id(36)]
    public string? HoldReason { get; set; }

    /// <summary>
    /// UTC timestamp when Rx was placed on hold
    /// </summary>
    [Id(37)]
    public DateTime? HoldDate { get; set; }

    /// <summary>
    /// Full refill history — mirrors VistA File #52.1 sub-file
    /// </summary>
    [Id(38)]
    public List<RefillRecord> RefillHistory { get; set; } = new();

    // ── GAP 1: Medication Status Group ───────────────────────────────────────

    /// <summary>
    /// Medication status group — derived from Status per VistA rMeds.pas MedStatusGroup():
    /// 0 = MED_ACTIVE (ACTIVE, REFILL, HOLD, SUSPENDED, PROVIDER HOLD, ON CALL),
    /// 1 = MED_PENDING (NON-VERIFIED, DRUG INTERACTIONS, INCOMPLETE, PENDING),
    /// 2 = MED_NONACTIVE (DONE, EXPIRED, DISCONTINUED, DELETED, CANCELLED, LAPSED)
    /// </summary>
    [Id(39)]
    public int MedStatusGroup { get; set; }

    // ── GAP 3: DEA / Controlled Substance ────────────────────────────────────

    /// <summary>
    /// True when the dispensed drug is a controlled substance.
    /// Mirrors VistA rODMeds.pas DEACheckFailed logic.
    /// </summary>
    [Id(40)]
    public bool IsControlledSubstance { get; set; }

    /// <summary>
    /// DEA schedule (II, IIN, III, IIIIN, IV, V) — blank for non-controlled
    /// </summary>
    [Id(41)]
    public string? DeaSchedule { get; set; }

    /// <summary>
    /// Reason DEA check failed, if applicable (e.g., "NO DEA # ON FILE", "INVALID DEA #")
    /// </summary>
    [Id(42)]
    public string? DeaCheckFailedReason { get; set; }

    /// <summary>
    /// True when the DEA check has passed for this prescription
    /// </summary>
    [Id(43)]
    public bool DeaCheckPassed { get; set; }

    // ── GAP 4: Qty / Days / Refills Constraints ───────────────────────────────

    /// <summary>
    /// Maximum number of refills allowed — per VistA rODMeds.pas CalcMaxRefills()
    /// </summary>
    [Id(44)]
    public int? MaxRefills { get; set; }

    /// <summary>
    /// Maximum days supply allowed per formulary / drug class
    /// </summary>
    [Id(45)]
    public int? MaxDaysSupply { get; set; }

    /// <summary>
    /// Maximum quantity allowed per dispense event
    /// </summary>
    [Id(46)]
    public int? MaxQuantity { get; set; }

    /// <summary>
    /// True when this prescription is for a titration regimen.
    /// Mirrors VistA rODMeds.pas CalcMaxRefills Titration parameter.
    /// </summary>
    [Id(47)]
    public bool IsTitration { get; set; }

    /// <summary>
    /// True when this is a discharge prescription.
    /// Affects max refills calculation per VistA rODMeds.pas.
    /// </summary>
    [Id(48)]
    public bool IsDischarge { get; set; }

    /// <summary>
    /// True when the drug requires a dosing schedule (schedule required validation).
    /// Mirrors VistA rODMeds.pas ScheduleRequired().
    /// </summary>
    [Id(49)]
    public bool ScheduleRequired { get; set; }

    // ── Patient Counseling (PSOCP.m) ────────────────────────────────────────

    /// <summary>Whether patient counseling has been completed.</summary>
    [Id(50)]
    public bool CounselingCompleted { get; set; }

    /// <summary>UTC timestamp when counseling was completed.</summary>
    [Id(51)]
    public DateTime? CounselingDate { get; set; }

    /// <summary>Pharmacist who conducted the counseling session.</summary>
    [Id(52)]
    public string? CounseledBy { get; set; }

    /// <summary>Documentation of counseling topics discussed.</summary>
    [Id(53)]
    public string? CounselingNotes { get; set; }

    /// <summary>
    /// Deep copy. Used at event/state boundaries so that mutating the live
    /// prescription on the grain does not retroactively mutate the historical
    /// snapshot stored on a clinical event payload (which would break the
    /// hash chain). <see cref="RefillHistory"/> is deep-copied per record.
    /// </summary>
    public PharmacyState Clone() => new()
    {
        PrescriptionId = PrescriptionId,
        PatientId = PatientId,
        DrugName = DrugName,
        DrugId = DrugId,
        Dosage = Dosage,
        Route = Route,
        Schedule = Schedule,
        Status = Status,
        IssueDate = IssueDate,
        FillDate = FillDate,
        ExpirationDate = ExpirationDate,
        LastDispenseDate = LastDispenseDate,
        DaysSupply = DaysSupply,
        Quantity = Quantity,
        Refills = Refills,
        RefillsRemaining = RefillsRemaining,
        ProviderId = ProviderId,
        ProviderName = ProviderName,
        PharmacyId = PharmacyId,
        PharmacyName = PharmacyName,
        Sig = Sig,
        OrderId = OrderId,
        Comments = Comments,
        CreatedDate = CreatedDate,
        LastModifiedDate = LastModifiedDate,
        RxNumber = RxNumber,
        Priority = Priority,
        IsVerified = IsVerified,
        VerifiedBy = VerifiedBy,
        VerifiedDate = VerifiedDate,
        CounselingRequired = CounselingRequired,
        IsLabelPrinted = IsLabelPrinted,
        LabelPrintedDate = LabelPrintedDate,
        NdcDispensed = NdcDispensed,
        LotNumber = LotNumber,
        DiscontinueReason = DiscontinueReason,
        HoldReason = HoldReason,
        HoldDate = HoldDate,
        RefillHistory = RefillHistory.Select(r => new RefillRecord
        {
            FillNumber = r.FillNumber,
            FillDate = r.FillDate,
            Quantity = r.Quantity,
            DaysSupply = r.DaysSupply,
            RxNumber = r.RxNumber,
            PharmacistId = r.PharmacistId,
            NdcDispensed = r.NdcDispensed,
            RecordedDate = r.RecordedDate
        }).ToList(),
        MedStatusGroup = MedStatusGroup,
        IsControlledSubstance = IsControlledSubstance,
        DeaSchedule = DeaSchedule,
        DeaCheckFailedReason = DeaCheckFailedReason,
        DeaCheckPassed = DeaCheckPassed,
        MaxRefills = MaxRefills,
        MaxDaysSupply = MaxDaysSupply,
        MaxQuantity = MaxQuantity,
        IsTitration = IsTitration,
        IsDischarge = IsDischarge,
        ScheduleRequired = ScheduleRequired,
        CounselingCompleted = CounselingCompleted,
        CounselingDate = CounselingDate,
        CounseledBy = CounseledBy,
        CounselingNotes = CounselingNotes
    };
}

/// <summary>
/// One dispense event record (original fill or refill) — mirrors VistA File #52.1
/// </summary>
[GenerateSerializer]
public class RefillRecord
{
    /// <summary>
    /// 0 = original fill, 1+ = refills
    /// </summary>
    [Id(0)]
    public int FillNumber { get; set; }

    [Id(1)]
    public DateTime FillDate { get; set; }

    [Id(2)]
    public int? Quantity { get; set; }

    [Id(3)]
    public int? DaysSupply { get; set; }

    /// <summary>
    /// Label Rx number assigned at this dispense
    /// </summary>
    [Id(4)]
    public string? RxNumber { get; set; }

    [Id(5)]
    public string? PharmacistId { get; set; }

    [Id(6)]
    public string? NdcDispensed { get; set; }

    [Id(7)]
    public DateTime RecordedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Result of a refill eligibility check for a prescription.
/// Returned by IPharmacyGrain.CheckRefillEligibilityAsync().
///
/// VistA reference: PSO refill date calculation, CalcMaxRefills(),
/// DEA schedule enforcement, early refill detection.
/// </summary>
[GenerateSerializer]
public class RefillEligibilityResult
{
    /// <summary>Whether the prescription is currently eligible for refill.</summary>
    [Id(0)]
    public bool IsEligible { get; set; }

    /// <summary>Human-readable reasons why the prescription is not eligible (empty if eligible).</summary>
    [Id(1)]
    public List<string> Reasons { get; set; } = new();

    /// <summary>Number of refills remaining.</summary>
    [Id(2)]
    public int RefillsRemaining { get; set; }

    /// <summary>Total refills originally authorized.</summary>
    [Id(3)]
    public int TotalRefillsAuthorized { get; set; }

    /// <summary>Number of refills already dispensed.</summary>
    [Id(4)]
    public int RefillsDispensed { get; set; }

    /// <summary>Earliest date the next refill can be dispensed (based on days supply consumed rule).</summary>
    [Id(5)]
    public DateTime? EarliestRefillDate { get; set; }

    /// <summary>Prescription expiration date.</summary>
    [Id(6)]
    public DateTime? ExpirationDate { get; set; }

    /// <summary>Date of the last dispense event (fill or refill).</summary>
    [Id(7)]
    public DateTime? LastDispenseDate { get; set; }

    /// <summary>Days supply from the last dispense.</summary>
    [Id(8)]
    public int? DaysSupply { get; set; }

    /// <summary>Current prescription status.</summary>
    [Id(9)]
    public string Status { get; set; } = string.Empty;

    /// <summary>Whether the prescription is a DEA controlled substance.</summary>
    [Id(10)]
    public bool IsControlledSubstance { get; set; }

    /// <summary>DEA schedule if controlled substance.</summary>
    [Id(11)]
    public string? DeaSchedule { get; set; }

    /// <summary>Whether the refill is currently too early (less than 75% of supply consumed).</summary>
    [Id(12)]
    public bool IsTooEarly { get; set; }

    /// <summary>Percentage of current supply consumed (0-100+).</summary>
    [Id(13)]
    public int PercentConsumed { get; set; }

    /// <summary>Whether DUR is cleared (set by workflow grain, not PharmacyGrain).</summary>
    [Id(14)]
    public bool DurCleared { get; set; }

    /// <summary>Whether interaction screening is cleared (set by workflow grain).</summary>
    [Id(15)]
    public bool InteractionCleared { get; set; }

    /// <summary>Whether prior auth / coverage is cleared (set by workflow grain).</summary>
    [Id(16)]
    public bool PaCoverageCleared { get; set; }

    /// <summary>Prior auth / coverage reasons if not cleared.</summary>
    [Id(17)]
    public List<string> PaCoverageReasons { get; set; } = new();
}

/// <summary>
/// Result of a Prior Authorization / formulary coverage check.
/// Returned by IPatientWorkflowGrain.CheckPriorAuthStatusAsync().
/// </summary>
[GenerateSerializer]
public class PriorAuthCoverageResult
{
    /// <summary>Prescription this check was performed for.</summary>
    [Id(0)]
    public string PrescriptionId { get; set; } = string.Empty;

    /// <summary>Whether the prescription is cleared for fill (coverage OK, PA approved or not required).</summary>
    [Id(1)]
    public bool IsCleared { get; set; }

    /// <summary>Reasons why the prescription is not cleared.</summary>
    [Id(2)]
    public List<string> Reasons { get; set; } = new();

    /// <summary>Whether the patient has an active benefit plan.</summary>
    [Id(3)]
    public bool HasActivePlan { get; set; }

    /// <summary>Plan name if active.</summary>
    [Id(4)]
    public string? PlanName { get; set; }

    /// <summary>Formulary coverage status (COVERED, NOT_COVERED, REQUIRES_PA, NON_FORMULARY).</summary>
    [Id(5)]
    public string? CoverageStatus { get; set; }

    /// <summary>Formulary tier (1=preferred generic, 2=preferred brand, 3=non-preferred).</summary>
    [Id(6)]
    public int? Tier { get; set; }

    /// <summary>Whether the drug requires Prior Authorization.</summary>
    [Id(7)]
    public bool RequiresPriorAuth { get; set; }

    /// <summary>Whether an approved PA exists for this drug.</summary>
    [Id(8)]
    public bool HasApprovedPa { get; set; }

    /// <summary>The approved PA ID, if one exists.</summary>
    [Id(9)]
    public string? PaId { get; set; }
}

/// <summary>
/// Prescription label content generated by GenerateLabelContentAsync().
/// Contains all fields needed to render a pharmacy dispensing label.
/// VistA reference: PSJLBL.m label generation.
/// </summary>
[GenerateSerializer]
public class PrescriptionLabelContent
{
    /// <summary>Rx number displayed prominently on label.</summary>
    [Id(0)]
    public string RxNumber { get; set; } = string.Empty;

    /// <summary>Patient name.</summary>
    [Id(1)]
    public string PatientName { get; set; } = string.Empty;

    /// <summary>Patient ID.</summary>
    [Id(2)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>Drug name and strength.</summary>
    [Id(3)]
    public string DrugName { get; set; } = string.Empty;

    /// <summary>Dosage.</summary>
    [Id(4)]
    public string? Dosage { get; set; }

    /// <summary>Route of administration.</summary>
    [Id(5)]
    public string? Route { get; set; }

    /// <summary>Schedule/frequency.</summary>
    [Id(6)]
    public string? Schedule { get; set; }

    /// <summary>Patient instructions (SIG).</summary>
    [Id(7)]
    public string? Sig { get; set; }

    /// <summary>Quantity dispensed.</summary>
    [Id(8)]
    public int? Quantity { get; set; }

    /// <summary>Days supply.</summary>
    [Id(9)]
    public int? DaysSupply { get; set; }

    /// <summary>Number of refills remaining.</summary>
    [Id(10)]
    public int? RefillsRemaining { get; set; }

    /// <summary>Prescribing provider name.</summary>
    [Id(11)]
    public string? ProviderName { get; set; }

    /// <summary>Pharmacy name.</summary>
    [Id(12)]
    public string? PharmacyName { get; set; }

    /// <summary>Issue date.</summary>
    [Id(13)]
    public DateTime? IssueDate { get; set; }

    /// <summary>Fill/dispense date.</summary>
    [Id(14)]
    public DateTime? FillDate { get; set; }

    /// <summary>Expiration date.</summary>
    [Id(15)]
    public DateTime? ExpirationDate { get; set; }

    /// <summary>NDC of dispensed product.</summary>
    [Id(16)]
    public string? NdcDispensed { get; set; }

    /// <summary>Lot number of dispensed product.</summary>
    [Id(17)]
    public string? LotNumber { get; set; }

    /// <summary>Whether this is a controlled substance.</summary>
    [Id(18)]
    public bool IsControlledSubstance { get; set; }

    /// <summary>DEA schedule if controlled.</summary>
    [Id(19)]
    public string? DeaSchedule { get; set; }

    /// <summary>Whether patient counseling is required.</summary>
    [Id(20)]
    public bool CounselingRequired { get; set; }

    /// <summary>Barcode data string (RxNumber for scanning).</summary>
    [Id(21)]
    public string BarcodeData { get; set; } = string.Empty;

    /// <summary>UTC timestamp when label was generated.</summary>
    [Id(22)]
    public DateTime GeneratedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Fill number (0 = original, 1+ = refill).</summary>
    [Id(23)]
    public int FillNumber { get; set; }
}
