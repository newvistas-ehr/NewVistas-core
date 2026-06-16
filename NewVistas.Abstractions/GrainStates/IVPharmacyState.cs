// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

// ── Enumerations ──────────────────────────────────────────────────────────────

/// <summary>
/// Lifecycle status of an IV admixture compounding order.
/// Maps to VistA File #53.4 status field.
/// </summary>
[GenerateSerializer]
public enum IVAdmixOrderStatus
{
    Pending = 0,
    Verified = 1,
    Compounding = 2,
    Ready = 3,
    Dispensed = 4,
    Administered = 5,
    Discontinued = 6,
    Expired = 7,
    Cancelled = 8
}

/// <summary>Clinical priority of the IV order.</summary>
[GenerateSerializer]
public enum IVAdmixPriority
{
    Routine = 0,
    ASAP = 1,
    STAT = 2,
    OnCall = 3
}

/// <summary>Vascular access route for IV administration.</summary>
[GenerateSerializer]
public enum IVAdmixRoute
{
    Peripheral = 0,
    Central = 1,
    PICC = 2,
    Midline = 3,
    Epidural = 4,
    Subcutaneous = 5,
    Other = 6
}

/// <summary>Physical container type for the IV admixture.</summary>
[GenerateSerializer]
public enum IVContainerType
{
    Bag = 0,
    Syringe = 1,
    Bottle = 2,
    Cassette = 3,
    Other = 4
}

/// <summary>Administration frequency of the IV admixture.</summary>
[GenerateSerializer]
public enum IVAdmixFrequency
{
    Once = 0,
    Continuous = 1,
    Q1H = 2,
    Q2H = 3,
    Q4H = 4,
    Q6H = 5,
    Q8H = 6,
    Q12H = 7,
    Q24H = 8,
    PRN = 9,
    Other = 10
}

// ── Supporting types ─────────────────────────────────────────────────────────

/// <summary>
/// An individual additive or base solution component in an IV admixture.
/// Maps to VistA File #50.8 (IV ADDITIVE).
/// </summary>
[GenerateSerializer]
public class IVAdmixAdditive
{
    /// <summary>Drug name. (.01)</summary>
    [Id(0)] public string DrugName { get; set; } = string.Empty;

    /// <summary>Drug identifier. (.02)</summary>
    [Id(1)] public string? DrugId { get; set; }

    /// <summary>Dose amount as a string (e.g., "20", "2.5"). (.03)</summary>
    [Id(2)] public string Dose { get; set; } = string.Empty;

    /// <summary>Dose unit (e.g., "mEq", "g", "mg", "units"). (.04)</summary>
    [Id(3)] public string DoseUnit { get; set; } = string.Empty;

    /// <summary>Whether this is the primary base solution (true) or an additive (false). (.05)</summary>
    [Id(4)] public bool IsBaseSolution { get; set; }
}

// ── State classes ─────────────────────────────────────────────────────────────

/// <summary>
/// State for an IV admixture compounding order.
/// Maps to VistA Files #53.4 (IV ORDERS) and #50.8 (IV ADDITIVE).
/// MUMPS routines: PSJIV.m, PSJVXU.m, PSJLBL.m
/// Grain key pattern: "IVAD-ORDER:{guid}"
/// </summary>
[GenerateSerializer]
public class IVAdmixOrderState
{
    /// <summary>Unique order identifier (grain key). (.01)</summary>
    [Id(0)] public string OrderId { get; set; } = string.Empty;

    /// <summary>Owning patient identifier. (.02)</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Linked inpatient order identifier, if applicable. (.03)</summary>
    [Id(2)] public string? LinkedInpatientOrderId { get; set; }

    /// <summary>Current lifecycle status. (.04)</summary>
    [Id(3)] public IVAdmixOrderStatus Status { get; set; } = IVAdmixOrderStatus.Pending;

    /// <summary>Clinical priority. (.05)</summary>
    [Id(4)] public IVAdmixPriority Priority { get; set; } = IVAdmixPriority.Routine;

    // ── Prescription / Formulation ───────────────────────────────────────────

    /// <summary>Base solution description (e.g., "Normal Saline", "D5W"). (.10)</summary>
    [Id(5)] public string BaseSolution { get; set; } = string.Empty;

    /// <summary>Base solution volume in mL. (.11)</summary>
    [Id(6)] public int BaseSolutionVolumeMl { get; set; }

    /// <summary>All additives and base solutions as ordered components. (.12)</summary>
    [Id(7)] public List<IVAdmixAdditive> Additives { get; set; } = new();

    /// <summary>Total volume of the admixture in mL (base + additives). (.13)</summary>
    [Id(8)] public int TotalVolumeMl { get; set; }

    // ── Administration parameters ────────────────────────────────────────────

    /// <summary>Vascular access route. (.20)</summary>
    [Id(9)] public IVAdmixRoute Route { get; set; } = IVAdmixRoute.Peripheral;

    /// <summary>Free-text route description. (.21)</summary>
    [Id(10)] public string? RouteDescription { get; set; }

    /// <summary>Infusion rate in mL/hr. Null for non-continuous or time-limited infusions. (.22)</summary>
    [Id(11)] public decimal? InfusionRateMlHr { get; set; }

    /// <summary>Human-readable infusion rate string (e.g., "125 mL/hr", "Over 30 min"). (.23)</summary>
    [Id(12)] public string? InfusionRateStr { get; set; }

    /// <summary>Administration frequency. (.24)</summary>
    [Id(13)] public IVAdmixFrequency Frequency { get; set; } = IVAdmixFrequency.Once;

    /// <summary>Free-text frequency description for non-standard schedules. (.25)</summary>
    [Id(14)] public string? FrequencyDescription { get; set; }

    /// <summary>Infusion duration in hours (for time-limited infusions). (.26)</summary>
    [Id(15)] public decimal? InfusionDurationHours { get; set; }

    // ── Container and quantity ───────────────────────────────────────────────

    /// <summary>Physical container type. (.30)</summary>
    [Id(16)] public IVContainerType ContainerType { get; set; } = IVContainerType.Bag;

    /// <summary>Number of containers to compound (bags, syringes, etc.). (.31)</summary>
    [Id(17)] public int ContainerCount { get; set; } = 1;

    // ── Order dates ──────────────────────────────────────────────────────────

    /// <summary>Scheduled start date/time for infusion. (.40)</summary>
    [Id(18)] public DateTime? StartDateTime { get; set; }

    /// <summary>Scheduled stop date/time. (.41)</summary>
    [Id(19)] public DateTime? StopDateTime { get; set; }

    // ── Personnel ────────────────────────────────────────────────────────────

    /// <summary>Ordering provider identifier. (.50)</summary>
    [Id(20)] public string? ProviderId { get; set; }

    /// <summary>Ordering provider name. (.51)</summary>
    [Id(21)] public string? ProviderName { get; set; }

    /// <summary>Verifying pharmacist identifier. (.52)</summary>
    [Id(22)] public string? PharmacistId { get; set; }

    /// <summary>Verifying pharmacist name. (.53)</summary>
    [Id(23)] public string? PharmacistName { get; set; }

    /// <summary>Compounding technician/pharmacist identifier. (.54)</summary>
    [Id(24)] public string? CompoundedById { get; set; }

    /// <summary>Compounding technician/pharmacist name. (.55)</summary>
    [Id(25)] public string? CompoundedByName { get; set; }

    // ── Verification ────────────────────────────────────────────────────────

    /// <summary>Date/time the order was pharmacist-verified. (.60)</summary>
    [Id(26)] public DateTime? VerifiedDate { get; set; }

    // ── Compounding and batch ────────────────────────────────────────────────

    /// <summary>Date/time compounding started. (.70)</summary>
    [Id(27)] public DateTime? CompoundingStartDate { get; set; }

    /// <summary>Date/time compounding was completed. (.71)</summary>
    [Id(28)] public DateTime? CompoundingCompleteDate { get; set; }

    /// <summary>Lot or batch number assigned during compounding. (.72)</summary>
    [Id(29)] public string? LotNumber { get; set; }

    /// <summary>Expiration date/time of the compounded product. (.73)</summary>
    [Id(30)] public DateTime? ExpirationDate { get; set; }

    // ── Label tracking ───────────────────────────────────────────────────────

    /// <summary>Whether the IV label has been printed. (.80)</summary>
    [Id(31)] public bool LabelPrinted { get; set; }

    /// <summary>Date/time the label was printed. (.81)</summary>
    [Id(32)] public DateTime? LabelPrintedDate { get; set; }

    /// <summary>Name of person who printed the label. (.82)</summary>
    [Id(33)] public string? LabelPrintedBy { get; set; }

    // ── Dispensing and administration ────────────────────────────────────────

    /// <summary>Date/time the order was dispensed to the ward. (.90)</summary>
    [Id(34)] public DateTime? DispensingDateTime { get; set; }

    /// <summary>Date/time the admixture was administered. (.91)</summary>
    [Id(35)] public DateTime? AdministrationDateTime { get; set; }

    // ── Discontinuation / Cancellation ──────────────────────────────────────

    /// <summary>Reason the order was discontinued, if applicable. (.92)</summary>
    [Id(36)] public string? DiscontinuationReason { get; set; }

    /// <summary>Reason the order was cancelled, if applicable. (.93)</summary>
    [Id(37)] public string? CancellationReason { get; set; }

    // ── Notes ────────────────────────────────────────────────────────────────

    /// <summary>Special instructions or pharmacy notes. (.95)</summary>
    [Id(38)] public string? Notes { get; set; }

    // ── Audit ────────────────────────────────────────────────────────────────

    /// <summary>Date this record was created. (.99)</summary>
    [Id(39)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Date this record was last modified. (.100)</summary>
    [Id(40)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>Summary entry stored in the per-patient IV admixture order index.</summary>
[GenerateSerializer]
public class IVAdmixOrderIndexEntry
{
    [Id(0)] public string OrderId { get; set; } = string.Empty;
    [Id(1)] public IVAdmixOrderStatus Status { get; set; }
    [Id(2)] public IVAdmixPriority Priority { get; set; }
    [Id(3)] public string BaseSolution { get; set; } = string.Empty;
    [Id(4)] public int TotalVolumeMl { get; set; }
    [Id(5)] public IVAdmixRoute Route { get; set; }
    [Id(6)] public string? InfusionRateStr { get; set; }
    [Id(7)] public IVAdmixFrequency Frequency { get; set; }
    [Id(8)] public DateTime? StartDateTime { get; set; }
    [Id(9)] public DateTime? StopDateTime { get; set; }
    [Id(10)] public string? LotNumber { get; set; }
    [Id(11)] public DateTime? ExpirationDate { get; set; }
    [Id(12)] public string? ProviderName { get; set; }
    [Id(13)] public DateTime CreatedDate { get; set; }
    [Id(14)] public int AdditiveCount { get; set; }
}
