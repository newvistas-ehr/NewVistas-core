// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

// ─── Status codes — VistA File #350.21 IB ACTION STATUS ─────────────────────

/// <summary>Processing status of an Integrated Billing action (File #350.21).</summary>
[GenerateSerializer]
public enum IBillingActionStatus
{
    /// <summary>Work in progress — not yet submitted (INC).</summary>
    Incomplete = 1,

    /// <summary>Ready for AR processing (PEND).</summary>
    CompletePendingAR = 2,

    /// <summary>Sent to insurance/payer (BILL).</summary>
    Billed = 3,

    /// <summary>Modified after billing (UPD).</summary>
    Updated = 4,

    /// <summary>Awaiting insurance information (INS).</summary>
    OnHoldInsurance = 8,

    /// <summary>Processing error encountered (ERR).</summary>
    ErrorEncountered = 9,

    /// <summary>Action cancelled (CANC).</summary>
    Cancelled = 10,

    /// <summary>Copay exemption cancellation (EXEMPTED).</summary>
    CopayExemptionCancelled = 11,

    /// <summary>Awaiting rate resolution (RATE).</summary>
    OnHoldRate = 20,

    /// <summary>Under manual review (REVIEW).</summary>
    OnHoldReview = 21,

    /// <summary>Legacy data conversion record (CONV).</summary>
    ConvertedRecord = 99,
}

// ─── Action categories — derived from action type prefix patterns ─────────────

/// <summary>High-level category of an IB billing action (derived from File #350.1 action type codes).</summary>
[GenerateSerializer]
public enum IBActionCategory
{
    /// <summary>Outpatient/Inpatient pharmacy copay (PSO/PSJ prefix).</summary>
    Pharmacy = 0,

    /// <summary>Inpatient copay or per diem (DG INPT prefix).</summary>
    Inpatient = 1,

    /// <summary>Outpatient visit copay (DG OPT prefix).</summary>
    Outpatient = 2,

    /// <summary>Long-term care copay (LTC prefix).</summary>
    LongTermCare = 3,

    /// <summary>Administrative action (Medicare deductible, admission, CHAMPVA).</summary>
    Administrative = 4,
}

// ─── Charge remove reasons — VistA File #350.3 IB CHARGE REMOVE REASONS ──────

/// <summary>
/// Static constants for IB charge remove reason codes (File #350.3).
/// 50 defined reasons covering pharmacy, visit, eligibility, and administrative categories.
/// </summary>
public static class IBRemoveReasons
{
    // Pharmacy-Related (Category 1)
    public const string RxRefused             = "RX REFUSED";
    public const string RxNeverReceived       = "RX NEVER RECEIVED";
    public const string RxReturnedDamagedMail = "RX RETURNED/DAMAGED (MAIL)";
    public const string RxCancelled           = "RX CANCELLED";
    public const string RxDeleted             = "RX DELETED";
    public const string RxEdited              = "RX EDITED";
    public const string RxCopayIncomeExemption = "RX COPAY INCOME EXEMPTION";
    public const string RxForFormerPow        = "RX FOR FORMER POW";
    public const string RxForUnemployableVet  = "RX FOR UNEMPLOYABLE VETERAN";
    public const string BedsideMedications    = "BEDSIDE MEDICATIONS";
    public const string SupplyItem            = "SUPPLY ITEM";
    public const string PharmacyAutoCancelled = "PHARMACY AUTO CANCELLED";
    public const string BilledAtHigherTierRate = "BILLED AT HIGHER TIER RATE";
    public const string InvestigationalDrug   = "INVESTIGATIONAL DRUG";

    // Visit/Appointment (Category 2)
    public const string MtOpApptNoShow        = "MT OP APPT NO-SHOW";
    public const string MtOpApptCancelled     = "MT OP APPT CANCELLED";
    public const string MtChargeEdited        = "MT CHARGE EDITED";
    public const string MtStatusChangedFromYes = "MT STATUS CHANGED FROM YES";
    public const string CompAndPensionVisit   = "COMP & PENSION VISIT";
    public const string ServiceConnectedVisit = "SERVICE CONNECTED VISIT";
    public const string CheckOutDeleted       = "CHECK OUT DELETED";

    // Adjudication/Eligibility (Category 3)
    public const string EligibilityIncorrect  = "ELIGIBILITY INCORRECT";
    public const string ChangeInEligibility   = "CHANGE IN ELIGIBILITY";
    public const string PatientDeceased       = "PATIENT DECEASED";
    public const string AgentOrangeRelated    = "AGENT ORANGE RELATED";
    public const string IonizingRadRelated    = "IONIZING RAD RELATED";
    public const string SouthwestAsiaRelated  = "SOUTHWEST ASIA RELATED";
    public const string MilitarySexualTrauma  = "MILITARY SEXUAL TRAUMA";
    public const string CancerHeadNeck        = "CANCER OF HEAD/NECK";
    public const string CombatVeteran         = "COMBAT VETERAN";
    public const string CatastrophicallyDisabled = "CATASTROPHICALLY DISABLED";
    public const string HrfsFlagged           = "HRFS FLAGGED";
    public const string Project112Shad        = "PROJECT 112/SHAD";
    public const string PurpleHeartConfirmed  = "PURPLE HEART CONFIRMED";
    public const string HardshipGranted       = "HARDSHIP GRANTED";
    public const string CopayCapReached       = "COPAY CAP REACHED";

    // Administrative/System
    public const string EnteredInError        = "ENTERED IN ERROR";
    public const string InpatientPass         = "INPATIENT/PASS";
    public const string Employee              = "EMPLOYEE";
    public const string CnhThreeDay          = "CNH - 3 DAY";
    public const string InsuranceCoPaydInFull = "INSURANCE CO PAID IN FULL";
    public const string BilledLtcCharge       = "BILLED LTC CHARGE";
}

// ─── Main billing action state — VistA File #350 INTEGRATED BILLING ACTION ───

/// <summary>
/// State for a single Integrated Billing action record (VistA File #350).
/// Each action represents one charge event for a patient encounter — e.g.,
/// an outpatient copay, a pharmacy copay, an inpatient per diem entry.
/// Grain key: "IB-ACTION:{guid}"
/// </summary>
[GenerateSerializer]
public class IBillingActionState
{
    /// <summary>Unique identifier for this billing action (GUID string). (.01)</summary>
    [Id(0)] public string BillingActionId { get; set; } = string.Empty;

    /// <summary>Patient DFN/identifier this action is for. (2)</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Encounter or visit identifier this action is linked to. Optional.</summary>
    [Id(2)] public string? EncounterId { get; set; }

    /// <summary>IB Action Type code from File #350.1 (e.g., "PSO NSC RX COPAY NEW", "DG OPT COPAY NEW"). (.02)</summary>
    [Id(3)] public string ActionTypeCode { get; set; } = string.Empty;

    /// <summary>Human-readable description of the action type. (.03)</summary>
    [Id(4)] public string ActionTypeDescription { get; set; } = string.Empty;

    /// <summary>High-level billing category (Pharmacy, Inpatient, Outpatient, LTC, Administrative).</summary>
    [Id(5)] public IBActionCategory ActionCategory { get; set; } = IBActionCategory.Outpatient;

    /// <summary>Current processing status of this action (File #350.21). (.06)</summary>
    [Id(6)] public IBillingActionStatus Status { get; set; } = IBillingActionStatus.Incomplete;

    /// <summary>Dollar amount charged for this action. Null if not yet calculated. (.07)</summary>
    [Id(7)] public decimal? ChargeAmount { get; set; }

    /// <summary>Date and time this action was entered into the system. (.08)</summary>
    [Id(8)] public DateTime DateEntered { get; set; } = DateTime.UtcNow;

    /// <summary>User ID of the person who entered this action. (.09)</summary>
    [Id(9)] public string EnteredByUserId { get; set; } = string.Empty;

    /// <summary>Display name of the person who entered this action.</summary>
    [Id(10)] public string EnteredByUserName { get; set; } = string.Empty;

    /// <summary>Date of the clinical service that generated this charge. (.04)</summary>
    [Id(11)] public DateTime? ServiceDate { get; set; }

    /// <summary>ICD-10-CM diagnosis code associated with this action.</summary>
    [Id(12)] public string? DiagnosisCode { get; set; }

    /// <summary>CPT/HCPCS procedure code associated with this action.</summary>
    [Id(13)] public string? ProcedureCode { get; set; }

    /// <summary>Hospital location where the service occurred.</summary>
    [Id(14)] public string? LocationId { get; set; }

    /// <summary>Order number this billing action was generated from.</summary>
    [Id(15)] public string? OrderId { get; set; }

    /// <summary>Prescription number for pharmacy copay actions.</summary>
    [Id(16)] public string? PrescriptionId { get; set; }

    /// <summary>Remove reason code (from IBRemoveReasons constants) when action is cancelled. (.10)</summary>
    [Id(17)] public string? RemoveReasonCode { get; set; }

    /// <summary>Remove reason description when action is cancelled.</summary>
    [Id(18)] public string? RemoveReasonDescription { get; set; }

    /// <summary>Date and time this action was cancelled/removed.</summary>
    [Id(19)] public DateTime? RemoveDateTime { get; set; }

    /// <summary>User ID of the person who cancelled this action.</summary>
    [Id(20)] public string? RemovedByUserId { get; set; }

    /// <summary>Free-text notes or comments about this action.</summary>
    [Id(21)] public string? Notes { get; set; }

    /// <summary>Whether this charge has been marked as exempt from copay obligation.</summary>
    [Id(22)] public bool IsExempt { get; set; }

    /// <summary>Reason for copay exemption (e.g., "SERVICE CONNECTED", "INCOME EXEMPT"). (.11)</summary>
    [Id(23)] public string? ExemptionReason { get; set; }

    /// <summary>Date this grain record was first created.</summary>
    [Id(24)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Date this grain record was last modified.</summary>
    [Id(25)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
