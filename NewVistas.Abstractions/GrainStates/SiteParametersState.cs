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
    ///   "PRECISION_ONCOLOGY"           — Molecular biomarker profiling + precision-oncology therapy matching (biomarker → targeted/
    ///                                    immunotherapy decision support) (Modern enhancement; ENABLED BY DEFAULT, see SiteFeatures)
    ///   "HOME_BASED_CARE"              — Home-Based Primary Care: episodes, interdisciplinary team, plan of care, home visits, and the
    ///                                    caseload/census (Modern enhancement; ENABLED BY DEFAULT, see SiteFeatures)
    ///   "HOME_HEALTH_MEDICARE"         — Medicare skilled home health on top of HBPC — OASIS, PDGM, certification, EVV, NOA billing
    ///                                    (Modern enhancement; ENABLED BY DEFAULT for demos, see SiteFeatures)
    ///   "NEONATAL_CARE"                — Newborn nursery on top of the OB module — newborn chart, gestational-age/growth classification,
    ///                                    newborn screening, nursery census (Modern enhancement; ENABLED BY DEFAULT, see SiteFeatures)
    ///   "PHARMACOGENOMICS"             — Coded pharmacogenomic gene results (star-allele diplotype + CPIC phenotype) that drive drug-gene
    ///                                    decision support in the DUR engine (Modern enhancement; ENABLED BY DEFAULT, see SiteFeatures)
    /// </summary>
    [Id(7)]
    public HashSet<string> Features { get; set; } = new() { SiteFeatures.ExternalPharmacy, SiteFeatures.Oncology, SiteFeatures.PrecisionOncology, SiteFeatures.HomeBasedCare, SiteFeatures.HomeHealthMedicare, SiteFeatures.NeonatalCare, SiteFeatures.Pharmacogenomics };

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

    /// <summary>
    /// Precision oncology — per-tumor molecular biomarker profiling (EGFR, ALK, PD-L1, MSI, TMB,
    /// BRCA, …) and rule-based precision-oncology therapy matching (biomarker → targeted/immuno
    /// therapy; read-only decision support, never auto-orders). The precision-medicine layer
    /// beyond the classic tumor registry. A "Modern" enhancement, <b>enabled by default</b>; a
    /// site running the classic registry only can turn it off
    /// (<c>DisableFeatureAsync("PRECISION_ONCOLOGY")</c>).
    /// </summary>
    public const string PrecisionOncology = "PRECISION_ONCOLOGY";

    /// <summary>
    /// Home-Based Care (Home-Based Primary Care) — longitudinal, team-based primary care in the
    /// patient's home: episodes, the interdisciplinary team, an interdisciplinary plan of care,
    /// home visits, comprehensive assessments, and the facility caseload/census. A "Modern"
    /// enhancement (VistA's HBPC <c>HBH</c> package is only workload reporting on top of CPRS),
    /// <b>enabled by default</b> so it is present in every demo. A site can turn it off
    /// (<c>DisableFeatureAsync("HOME_BASED_CARE")</c>). Home-care data is readable by any clinician;
    /// only editing requires the HBHC MANAGER key.
    /// </summary>
    public const string HomeBasedCare = "HOME_BASED_CARE";

    /// <summary>
    /// Medicare skilled home health layered on top of Home-Based Care — certified 60-day periods
    /// (with 30-day payment periods), OASIS assessments + scrubbing, PDGM payment grouping, EVV,
    /// and NOA/claims billing; activates the <c>MedicareSkilledHomeHealth</c> program type. A
    /// "Modern" enhancement, <b>enabled by default</b> so it shows in demos. A pure VA/HBPC site
    /// (no Medicare skilled benefit) can turn it off (<c>DisableFeatureAsync("HOME_HEALTH_MEDICARE")</c>),
    /// leaving the longitudinal HBPC model from Phase 1.
    /// </summary>
    public const string HomeHealthMedicare = "HOME_HEALTH_MEDICARE";

    /// <summary>
    /// Neonatal care — a newborn nursery layered on top of the OB/prenatal module. Each newborn
    /// (registered from the mother's delivery) gets its own chart: birth data with gestational-age,
    /// birth-weight and size-for-gestational-age classification, the newborn exam, newborn screening
    /// (metabolic / CCHD / hearing / bilirubin), interval weight &amp; feeding, a nursery level of care,
    /// and discharge — plus a facility nursery census. A "Modern" enhancement, <b>enabled by
    /// default</b> so it appears in demos; a site can turn it off
    /// (<c>DisableFeatureAsync("NEONATAL_CARE")</c>), leaving the maternal OB record only.
    /// </summary>
    public const string NeonatalCare = "NEONATAL_CARE";

    /// <summary>
    /// Pharmacogenomics (PGx) — coded gene results (star-allele diplotype + CPIC phenotype) returned
    /// from a genotyping lab, stored as discrete data so drug-gene decision support fires at
    /// prescribing time. The DUR engine matches each prescription against the patient's PGx profile
    /// (curated CPIC/FDA knowledge base) so a contraindication (e.g. CYP2C19 poor metabolizer →
    /// clopidogrel, HLA-B*57:01 → abacavir) surfaces alongside drug-drug / drug-allergy checks. A
    /// "Modern" enhancement, <b>enabled by default</b> so it appears in demos; a site can turn it off
    /// (<c>DisableFeatureAsync("PHARMACOGENOMICS")</c>), and the DUR PGx check becomes a no-op.
    /// </summary>
    public const string Pharmacogenomics = "PHARMACOGENOMICS";
}
