// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for the system-level site parameters grain.
/// Based on VistA PARAMETER file (#8989.5) and PARAMETER DEFINITION (#8989.51).
/// Holds cross-cutting display and behavior settings.
/// </summary>
[GenerateSerializer]
public class SiteParametersState
{
    /// <summary>
    /// Site/facility identifier
    /// </summary>
    [Id(0)]
    public string SiteId { get; set; } = string.Empty;

    /// <summary>
    /// Number of recent vitals to cache on the patient grain.
    /// Maps to VistA ORWCV VITALS parameter.
    /// Default 10 — shows latest reading per vital type on the cover sheet.
    /// </summary>
    [Id(1)]
    public int VitalsDisplayCount { get; set; } = 10;

    /// <summary>
    /// Number of recent orders to cache on the patient grain.
    /// Maps to VistA ORWCV ORDERS parameter.
    /// Default 5 — shows latest orders on the cover sheet.
    /// </summary>
    [Id(5)]
    public int OrdersDisplayCount { get; set; } = 5;

    /// <summary>
    /// Number of recent notes to cache on the patient grain.
    /// Maps to VistA ORWCV NOTES parameter.
    /// Default 10 — shows latest notes on the cover sheet.
    /// </summary>
    [Id(6)]
    public int NotesDisplayCount { get; set; } = 10;

    /// <summary>
    /// Generic named parameters for extensibility.
    /// Maps to VistA PARAMETER file key/value pairs.
    /// </summary>
    [Id(2)]
    public Dictionary<string, string> Parameters { get; set; } = new();

    /// <summary>
    /// Set of enabled feature flags for this site.
    /// Used by the composition pattern (Site Flavor Architecture, Option 4)
    /// to enable optional grains per site.
    ///
    /// Recognized feature flags:
    ///   "PATIENT_MERGE"                — Patient record merge (VistA DG MERGE)
    ///   "IMMUNIZATION_FORECAST"        — Immunization forecasting (RPMS)
    ///   "EXTERNAL_REFERRAL"            — External referral tracking (RPMS RCIS)
    ///   "SUBSTANCE_ABUSE_TREATMENT"    — SA treatment programs / CDMIS (RPMS)
    ///   "PHARMACY_POS"                 — Pharmacy Point of Sale / NCPDP adjudication (RPMS)
    ///   "EPCS"                         — E-Prescribing for Controlled Substances / 21 CFR 1311
    ///   "GPRA_REPORTING"               — GPRA population health aggregate reporting (RPMS)
    ///   "PCC_SURVEILLANCE"             — PCC encounter-level surveillance for reportable conditions (RPMS)
    ///   "ICARE_DASHBOARD"              — iCare clinical dashboard (RPMS)
    ///   "APPOINTMENT_WAITLIST"         — Wait list with auto-rebooking (RPMS SD Wait List, File #409.3)
    ///   "PROVIDER_AVAILABILITY"        — Provider-level availability patterns, time blocks, scheduling tiers (Enhancement — VistA is clinic-centric only)
    ///   "PROVIDER_UNAVAILABILITY_BATCH" — Batch cancellation/reassignment when provider suddenly unavailable (Enhancement — VistA handles one-at-a-time)
    ///   "PATIENT_SELF_SCHEDULING"      — Patient portal appointment self-scheduling (Enhancement — inspired by VAOS, not part of core VistA/RPMS)
    /// </summary>
    [Id(7)]
    public HashSet<string> Features { get; set; } = new();

    [Id(3)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Id(4)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
