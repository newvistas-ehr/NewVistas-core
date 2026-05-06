// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

// ── Enums ─────────────────────────────────────────────────────────────────────

/// <summary>DEA drug schedule classification per 21 CFR Part 1308.</summary>
[GenerateSerializer]
public enum DEADrugSchedule
{
    /// <summary>Schedule II — high abuse potential, accepted medical use (e.g., morphine, oxycodone).</summary>
    ScheduleII = 2,
    /// <summary>Schedule III — less abuse potential than CII (e.g., codeine combinations, buprenorphine).</summary>
    ScheduleIII = 3,
    /// <summary>Schedule IV — lower abuse potential (e.g., benzodiazepines, tramadol).</summary>
    ScheduleIV = 4,
    /// <summary>Schedule V — lowest abuse potential (e.g., cough preparations with &lt;200mg codeine/100ml).</summary>
    ScheduleV = 5,
}

/// <summary>Type of controlled substance vault inspection (VistA PSNINSP.m).</summary>
[GenerateSerializer]
public enum CSInspectionType
{
    /// <summary>Routine scheduled inspection (typically monthly or quarterly).</summary>
    Scheduled,
    /// <summary>Unscheduled spot-check inspection.</summary>
    Unscheduled,
    /// <summary>Follow-up inspection triggered by a prior discrepancy finding.</summary>
    DiscrepancyFollowUp,
    /// <summary>Reconciliation inspection to resolve an ongoing discrepancy.</summary>
    Reconciliation,
}

/// <summary>Outcome of a vault inspection.</summary>
[GenerateSerializer]
public enum CSInspectionResult
{
    /// <summary>All counts matched — no discrepancies found.</summary>
    Passed,
    /// <summary>Minor notes recorded but no count discrepancies.</summary>
    PassedWithNotes,
    /// <summary>One or more drugs failed to match system count.</summary>
    Failed,
    /// <summary>Discrepancy identified and requires investigation.</summary>
    DiscrepancyIdentified,
}

/// <summary>Nature of a controlled substance dispense event.</summary>
[GenerateSerializer]
public enum CSDispenseType
{
    /// <summary>Standard routine dispense.</summary>
    Routine,
    /// <summary>STAT (urgent) dispense.</summary>
    STAT,
    /// <summary>Emergency dispense outside normal workflow.</summary>
    Emergency,
    /// <summary>Wastage record (partial use, expired, or contaminated).</summary>
    Wastage,
}

// ── Supporting Value Types ─────────────────────────────────────────────────────

/// <summary>Single drug physical count entry within a vault inspection.</summary>
[GenerateSerializer]
public class CSInspectionCount
{
    /// <summary>Drug name (generic or trade).</summary>
    [Id(0)] public string DrugName { get; set; } = string.Empty;

    /// <summary>DEA schedule of the drug being counted.</summary>
    [Id(1)] public DEADrugSchedule DrugSchedule { get; set; }

    /// <summary>System (electronic) count at time of inspection.</summary>
    [Id(2)] public decimal SystemCount { get; set; }

    /// <summary>Physical (manual) count performed by inspector.</summary>
    [Id(3)] public decimal PhysicalCount { get; set; }

    /// <summary>
    /// Calculated discrepancy: PhysicalCount minus SystemCount.
    /// Negative = shortage; positive = overage; zero = no discrepancy.
    /// </summary>
    [Id(4)] public decimal Discrepancy { get; set; }

    /// <summary>Unit of measure for the count (e.g., "tablets", "mL").</summary>
    [Id(5)] public string CountUnit { get; set; } = string.Empty;

    /// <summary>Optional notes about this specific drug count.</summary>
    [Id(6)] public string? Notes { get; set; }
}

/// <summary>Lightweight summary of a CS dispense record for the location log index.</summary>
[GenerateSerializer]
public class CSDispenseSummaryEntry
{
    /// <summary>Unique record identifier.</summary>
    [Id(0)] public string RecordId { get; set; } = string.Empty;

    /// <summary>Location (vault/pharmacy) identifier.</summary>
    [Id(1)] public string LocationId { get; set; } = string.Empty;

    /// <summary>Patient name for display.</summary>
    [Id(2)] public string PatientName { get; set; } = string.Empty;

    /// <summary>Drug name dispensed.</summary>
    [Id(3)] public string DrugName { get; set; } = string.Empty;

    /// <summary>DEA drug schedule.</summary>
    [Id(4)] public DEADrugSchedule DrugSchedule { get; set; }

    /// <summary>Quantity dispensed.</summary>
    [Id(5)] public decimal QuantityDispensed { get; set; }

    /// <summary>Unit of measure (e.g., "mg", "tablets", "mL").</summary>
    [Id(6)] public string UnitOfMeasure { get; set; } = string.Empty;

    /// <summary>Name of dispensing staff member.</summary>
    [Id(7)] public string DispensedByName { get; set; } = string.Empty;

    /// <summary>Date and time of dispense.</summary>
    [Id(8)] public DateTime DispenseDateTime { get; set; }

    /// <summary>Running balance for this drug at this location after dispense.</summary>
    [Id(9)] public decimal RunningBalance { get; set; }

    /// <summary>Drug identifier (used for filtering by drug).</summary>
    [Id(10)] public string DrugId { get; set; } = string.Empty;
}

/// <summary>Lightweight summary of a vault inspection for the location log index.</summary>
[GenerateSerializer]
public class CSInspectionSummaryEntry
{
    /// <summary>Unique inspection identifier.</summary>
    [Id(0)] public string InspectionId { get; set; } = string.Empty;

    /// <summary>Location (vault) identifier.</summary>
    [Id(1)] public string LocationId { get; set; } = string.Empty;

    /// <summary>Type of inspection performed.</summary>
    [Id(2)] public CSInspectionType InspectionType { get; set; }

    /// <summary>Date and time inspection was conducted.</summary>
    [Id(3)] public DateTime InspectionDateTime { get; set; }

    /// <summary>Name of the inspector who conducted the count.</summary>
    [Id(4)] public string InspectorName { get; set; } = string.Empty;

    /// <summary>Overall result of the inspection.</summary>
    [Id(5)] public CSInspectionResult OverallResult { get; set; }

    /// <summary>Number of drugs with non-zero discrepancies.</summary>
    [Id(6)] public int TotalDiscrepancies { get; set; }

    /// <summary>Date the inspection record was created.</summary>
    [Id(7)] public DateTime CreatedDate { get; set; }
}

// ── State Classes ──────────────────────────────────────────────────────────────

/// <summary>
/// Full state for a controlled substance vault inspection record.
/// VistA File #58.82 — PSNINSP.m, PSNCS.m
/// </summary>
[GenerateSerializer]
public class CSInspectionState
{
    /// <summary>(.01) Unique inspection identifier (grain key).</summary>
    [Id(0)] public string InspectionId { get; set; } = string.Empty;

    /// <summary>(.02) Location/vault identifier.</summary>
    [Id(1)] public string LocationId { get; set; } = string.Empty;

    /// <summary>(.03) Location name for display.</summary>
    [Id(2)] public string LocationName { get; set; } = string.Empty;

    /// <summary>(.04) Type of inspection conducted.</summary>
    [Id(3)] public CSInspectionType InspectionType { get; set; }

    /// <summary>(.05) Date and time of the inspection.</summary>
    [Id(4)] public DateTime InspectionDateTime { get; set; }

    /// <summary>(.06) Inspector user ID.</summary>
    [Id(5)] public string InspectorId { get; set; } = string.Empty;

    /// <summary>(.07) Inspector full name.</summary>
    [Id(6)] public string InspectorName { get; set; } = string.Empty;

    /// <summary>(.08) Primary witness user ID (required for DEA CII inspections).</summary>
    [Id(7)] public string WitnessId { get; set; } = string.Empty;

    /// <summary>(.09) Primary witness full name.</summary>
    [Id(8)] public string WitnessName { get; set; } = string.Empty;

    /// <summary>(.10) Secondary witness user ID (optional).</summary>
    [Id(9)] public string? SecondWitnessId { get; set; }

    /// <summary>(.11) Secondary witness full name (optional).</summary>
    [Id(10)] public string? SecondWitnessName { get; set; }

    /// <summary>Drug counts recorded during this inspection.</summary>
    [Id(11)] public List<CSInspectionCount> DrugCounts { get; set; } = new();

    /// <summary>(.12) Overall result after finalization.</summary>
    [Id(12)] public CSInspectionResult OverallResult { get; set; }

    /// <summary>(.13) Total number of drugs with non-zero discrepancies.</summary>
    [Id(13)] public int TotalDiscrepancies { get; set; }

    /// <summary>(.14) Whether discrepancies were formally reported.</summary>
    [Id(14)] public bool DiscrepanciesReported { get; set; }

    /// <summary>(.15) ID of person discrepancies were reported to.</summary>
    [Id(15)] public string? ReportedToId { get; set; }

    /// <summary>(.16) Name of person discrepancies were reported to.</summary>
    [Id(16)] public string? ReportedToName { get; set; }

    /// <summary>(.17) Date/time discrepancies were formally reported.</summary>
    [Id(17)] public DateTime? ReportedDateTime { get; set; }

    /// <summary>(.18) Notes from discrepancy investigation.</summary>
    [Id(18)] public string? InvestigationNotes { get; set; }

    /// <summary>(.19) General notes about this inspection.</summary>
    [Id(19)] public string? Notes { get; set; }

    /// <summary>Date this inspection record was created.</summary>
    [Id(20)] public DateTime CreatedDate { get; set; }

    /// <summary>Date this record was last modified.</summary>
    [Id(21)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>
/// Full state for a DEA-required controlled substance patient dispense record.
/// VistA File #58.80 — PSNLOG.m, PSNCS.m
/// </summary>
[GenerateSerializer]
public class CSDispenseRecordState
{
    /// <summary>(.01) Unique dispense record identifier (grain key).</summary>
    [Id(0)] public string RecordId { get; set; } = string.Empty;

    /// <summary>(.02) Location/vault identifier where dispensed.</summary>
    [Id(1)] public string LocationId { get; set; } = string.Empty;

    /// <summary>(.03) Location name for display.</summary>
    [Id(2)] public string LocationName { get; set; } = string.Empty;

    /// <summary>(.04) Patient identifier.</summary>
    [Id(3)] public string PatientId { get; set; } = string.Empty;

    /// <summary>(.05) Patient full name.</summary>
    [Id(4)] public string PatientName { get; set; } = string.Empty;

    /// <summary>(.06) Patient date of birth (required for DEA logs).</summary>
    [Id(5)] public DateTime? PatientDateOfBirth { get; set; }

    /// <summary>(.07) Drug identifier.</summary>
    [Id(6)] public string DrugId { get; set; } = string.Empty;

    /// <summary>(.08) Drug name (generic).</summary>
    [Id(7)] public string DrugName { get; set; } = string.Empty;

    /// <summary>(.09) DEA schedule of the dispensed drug.</summary>
    [Id(8)] public DEADrugSchedule DEASchedule { get; set; }

    /// <summary>(.10) NDC number of the dispensed drug (optional).</summary>
    [Id(9)] public string? NdcNumber { get; set; }

    /// <summary>(.11) Quantity dispensed to patient.</summary>
    [Id(10)] public decimal QuantityDispensed { get; set; }

    /// <summary>(.12) Unit of measure (e.g., "mg", "tablets", "mL").</summary>
    [Id(11)] public string UnitOfMeasure { get; set; } = string.Empty;

    /// <summary>(.13) Running balance of this drug at this location after dispense.</summary>
    [Id(12)] public decimal RunningBalance { get; set; }

    /// <summary>(.14) Nature of this dispense event.</summary>
    [Id(13)] public CSDispenseType DispenseType { get; set; }

    /// <summary>(.15) Prescriber/ordering provider identifier.</summary>
    [Id(14)] public string PrescriberId { get; set; } = string.Empty;

    /// <summary>(.16) Prescriber full name.</summary>
    [Id(15)] public string PrescriberName { get; set; } = string.Empty;

    /// <summary>(.17) Prescriber DEA registration number.</summary>
    [Id(16)] public string? PrescriberDEANumber { get; set; }

    /// <summary>(.18) ID of staff member who dispensed the drug.</summary>
    [Id(17)] public string DispensedById { get; set; } = string.Empty;

    /// <summary>(.19) Name of staff member who dispensed the drug.</summary>
    [Id(18)] public string DispensedByName { get; set; } = string.Empty;

    /// <summary>(.20) Witness user ID (required for CII dispenses).</summary>
    [Id(19)] public string? WitnessId { get; set; }

    /// <summary>(.21) Witness full name.</summary>
    [Id(20)] public string? WitnessName { get; set; }

    /// <summary>(.22) Date and time of dispense.</summary>
    [Id(21)] public DateTime DispenseDateTime { get; set; }

    /// <summary>(.23) Prescription or order number reference.</summary>
    [Id(22)] public string? PrescriptionNumber { get; set; }

    /// <summary>(.24) Linked inpatient or outpatient order ID.</summary>
    [Id(23)] public string? OrderId { get; set; }

    /// <summary>(.25) General notes about this dispense.</summary>
    [Id(24)] public string? Notes { get; set; }

    /// <summary>Date this record was created.</summary>
    [Id(25)] public DateTime CreatedDate { get; set; }
}
