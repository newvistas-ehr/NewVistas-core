// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
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
    /// Number of recent item IDs kept per clinical domain in PatientState
    /// (labs, consults, surgeries, radiology, BCMA, imaging, ADT, etc.).
    /// Older IDs live in the per-domain history indexes and are fetched only
    /// when the user actively requests full history.
    /// Allergies are NEVER subject to this cap — the complete allergy list
    /// always travels with the patient.
    /// Default 5.
    /// </summary>
    [Id(8)]
    public int RecentItemsDisplayCount { get; set; } = 5;

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
    ///   "EXTERNAL_PHARMACY"            — Multiple outpatient pharmacies, patient-preferred pharmacy, and NCPDP SCRIPT e-prescribing
    ///                                    (Enhancement — VistA/RPMS dispense in-house + CMOP mail-order; ENABLED BY DEFAULT, see SiteFeatures)
    ///   "ONCOLOGY"                     — First-class oncology: tumor registry, staging, treatment episodes, radiation therapy, and the
    ///                                    NAACCR cancer-registry abstract (Modern enhancement; ENABLED BY DEFAULT, see SiteFeatures)
    /// </summary>
    [Id(7)]
    public HashSet<string> Features { get; set; } = new() { SiteFeatures.ExternalPharmacy, SiteFeatures.Oncology };

    [Id(3)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Id(4)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Well-known site feature-flag names — the strings stored in <see cref="SiteParametersState.Features"/>
/// and checked with <c>ISiteParametersGrain.IsFeatureEnabledAsync(...)</c>.
/// </summary>
public static class SiteFeatures
{
    /// <summary>
    /// Multiple outpatient pharmacies: a pharmacy directory, a patient-preferred (default) pharmacy,
    /// a per-prescription pharmacy choice, and NCPDP SCRIPT e-prescribing. This is beyond core
    /// VistA/RPMS (which dispense in-house + CMOP mail-order), so it is feature-gated — but it is
    /// <b>enabled by default</b> (pre-seeded into <see cref="SiteParametersState.Features"/>). A
    /// VA/IHS-style site can turn it off (<c>DisableFeatureAsync("EXTERNAL_PHARMACY")</c>) to restore
    /// facility-only dispensing; inpatient orders always use the hospital pharmacy regardless.
    /// </summary>
    public const string ExternalPharmacy = "EXTERNAL_PHARMACY";

    /// <summary>
    /// First-class oncology — the tumor registry, staging, treatment episodes, radiation-therapy
    /// courses, and the NAACCR cancer-registry abstract, surfaced as a dedicated Oncology area. A
    /// "Modern" enhancement (beyond the classic VistA tumor-registry posture); <b>enabled by
    /// default</b> (pre-seeded into <see cref="SiteParametersState.Features"/>) so it is present in
    /// every demo. A pure VistA/RPMS site can turn it off (<c>DisableFeatureAsync("ONCOLOGY")</c>).
    /// Oncology data stays readable by any clinician; only editing requires the ONCO MANAGER key.
    /// </summary>
    public const string Oncology = "ONCOLOGY";
}
