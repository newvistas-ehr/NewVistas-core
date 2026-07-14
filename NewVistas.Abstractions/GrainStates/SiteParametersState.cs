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
    ///   "HOSPITAL_AT_HOME"             — Acute inpatient-substitutive care in the home (CMS Acute Hospital Care at Home) — the HospitalAtHome
    ///                                    program type + the freed-bed source-admission handoff (Modern enhancement; ENABLED BY DEFAULT, see SiteFeatures)
    ///   "NEONATAL_CARE"                — Newborn nursery on top of the OB module — newborn chart, gestational-age/growth classification,
    ///                                    newborn screening, nursery census (Modern enhancement; ENABLED BY DEFAULT, see SiteFeatures)
    ///   "PHARMACOGENOMICS"             — Coded pharmacogenomic gene results (star-allele diplotype + CPIC phenotype) that drive drug-gene
    ///                                    decision support in the DUR engine (Modern enhancement; ENABLED BY DEFAULT, see SiteFeatures)
    ///   "HEREDITARY_GENETICS"          — Interpreted genetic test reports + coded variants (HGVS/ClinVar), structured family history, and a
    ///                                    hereditary-risk assessment (germline variant→syndrome; family-history→referral) (Modern; ENABLED BY DEFAULT)
    ///   "SPECIALTY_COVERSHEET"         — The cover sheet as a composition of sections driven by a layout (General/Oncology/Procedural), resolved
    ///                                    per-patient-then-viewer, over a non-suppressible safety spine (Modern; ENABLED BY DEFAULT, see SiteFeatures)
    ///   "PERSON_IDENTITY"              — Person anchor unifying one human across patient/staff/relative roles (ADR-002): cross-role view, link
    ///                                    workflow, employee-patient privacy guard, cascade opportunities (Modern; ENABLED BY DEFAULT, see SiteFeatures)
    ///   "BED_MANAGEMENT"               — Institution-aware bed board (unit/room/bed lifecycle + EVS turnover) and the inter-facility Transfer
    ///                                    Center (request→accept→complete; ADR-003) (Modern; ENABLED BY DEFAULT, see SiteFeatures)
    /// </summary>
    [Id(7)]
    public HashSet<string> Features { get; set; } = new() { SiteFeatures.ExternalPharmacy, SiteFeatures.Oncology, SiteFeatures.PrecisionOncology, SiteFeatures.HomeBasedCare, SiteFeatures.HomeHealthMedicare, SiteFeatures.HospitalAtHome, SiteFeatures.NeonatalCare, SiteFeatures.Pharmacogenomics, SiteFeatures.HereditaryGenetics, SiteFeatures.SpecialtyCoverSheet, SiteFeatures.PersonIdentity, SiteFeatures.BedManagement, SiteFeatures.EmergingConditions, SiteFeatures.SocialCare };

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
    /// Hospital-at-Home — acute, inpatient-substitutive care delivered in the home (CMS "Acute
    /// Hospital Care at Home"): the <c>HospitalAtHome</c> program type on a home-care episode plus the
    /// acute-substitution handoff panel (the freed-bed source-admission link). A "Modern" enhancement,
    /// <b>enabled by default</b> so it appears in demos; a site without an acute-care-at-home waiver can
    /// turn it off (<c>DisableFeatureAsync("HOSPITAL_AT_HOME")</c>), leaving the delivery-model axis
    /// (hospital-provided vs external agency) and the rest of Home-Based Care intact.
    /// </summary>
    public const string HospitalAtHome = "HOSPITAL_AT_HOME";

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

    /// <summary>
    /// Hereditary genetics &amp; family history — interpreted genetic test reports with coded reportable
    /// variants (HGVS nomenclature, ClinVar/ACMG classification, germline vs somatic) stored as
    /// first-class data rather than opaque lab text, plus a structured family history. A curated
    /// hereditary-risk knowledge base derives the hereditary syndrome from a pathogenic germline
    /// variant (BRCA→HBOC, MLH1→Lynch, …) and red-flag patterns from the family history (early-onset
    /// breast cancer, ovarian cancer, Lynch clustering, …) that warrant a genetics referral. A
    /// "Modern" enhancement, <b>enabled by default</b> so it appears in demos; a site can turn it off
    /// (<c>DisableFeatureAsync("HEREDITARY_GENETICS")</c>).
    /// </summary>
    public const string HereditaryGenetics = "HEREDITARY_GENETICS";

    /// <summary>
    /// Specialty cover sheet — the cover sheet as a composition of sections driven by a layout
    /// (General / Oncology / Procedural) rather than one fixed layout for everyone. The layout is
    /// resolved <b>per-patient-then-viewer</b> (the patient's clinical context sets the loudest
    /// default; the viewer's provider service/specialty picks the lens among the relevant layouts),
    /// and every layout renders over a non-suppressible safety spine (demographics/CWAD/allergies). A
    /// "Modern" enhancement, <b>enabled by default</b>; a site can turn it off
    /// (<c>DisableFeatureAsync("SPECIALTY_COVERSHEET")</c>), leaving the classic single-layout cover sheet.
    /// </summary>
    public const string SpecialtyCoverSheet = "SPECIALTY_COVERSHEET";

    /// <summary>
    /// Person identity (ADR-002) — a Person anchor that unifies one human across their patient, staff,
    /// and relative roles (the triplication fix + the nurse-who-is-a-patient). Surfaces the (privileged,
    /// audited, break-the-glass) cross-role view, the link workflow + candidate suggestion, the
    /// employee-patient privacy guard, the patient access report, and cascade-testing opportunities. A
    /// "Modern" enhancement, <b>enabled by default</b>; a site can turn it off
    /// (<c>DisableFeatureAsync("PERSON_IDENTITY")</c>) — the nullable Person back-pointers simply go unused.
    /// </summary>
    public const string PersonIdentity = "PERSON_IDENTITY";

    /// <summary>
    /// Bed &amp; room management (ADR-003) — institution-aware bed board (unit/room/bed
    /// lifecycle with EVS turnover, honest "placeable" capacity) plus the inter-facility
    /// Transfer Center (request→accept-with-reserved-bed→complete). A "Modern" enhancement,
    /// <b>enabled by default</b>. Gates the Bed Board / Transfer Center UI and the transfer
    /// workflow; structured ADT unit/bed placement itself is core VistA and is not gated.
    /// The Transfer Center additionally self-hides on single-institution sites.
    /// </summary>
    public const string BedManagement = "BED_MANAGEMENT";

    /// <summary>
    /// Emerging-condition surveillance (ADR-004) — the ProtoCondition module. Structured coded
    /// symptom capture (the wide-net review-of-systems the front door asks when an illness has no
    /// name yet), living versioned disease clusters with a deterministic explainable matcher,
    /// net-closing analytics (which feature discriminates the cluster from background), a
    /// count-threshold alert, an isolation-precaution cover-sheet banner, and promotion-to-code with
    /// problem-list migration + an eCR trigger. A "Modern" enhancement, <b>enabled by default</b>; a
    /// site can turn it off (<c>DisableFeatureAsync("EMERGING_CONDITIONS")</c>) — the surveillance nav,
    /// symptom survey, banners, and screening all go quiet.
    /// </summary>
    public const string EmergingConditions = "EMERGING_CONDITIONS";

    /// <summary>
    /// Whole-Person Social Care (ADR-005) — the HITS harvest first increment. A Person-anchored
    /// general <b>household</b> (a family/residential unit of people that outlives any one member,
    /// distinct from the financial means-test household), and a coded <b>SDOH screening</b> (AHC-HRSN)
    /// that closes the loop: positive social-need domains map to billable Z55–Z65 codes placed on the
    /// problem list and to referrals opened in the existing Social Work referral machinery, with a
    /// per-domain cohort index for population reporting. Composes over the existing Social Work module;
    /// does not replace it. A "Modern" enhancement, <b>enabled by default</b>; a site can turn it off
    /// (<c>DisableFeatureAsync("SOCIAL_CARE")</c>) — the Social Care nav, household, and SDOH surface go
    /// quiet. Household resolution additionally depends on PERSON_IDENTITY.
    /// </summary>
    public const string SocialCare = "SOCIAL_CARE";
}
