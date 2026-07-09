// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.Security;

/// <summary>
/// Well-known security key constants — maps to VistA File #19.1 (SECURITY KEY).
///
/// In VistA, security keys are held by users (File #200, field 51 "KEYS" multiple)
/// and checked at runtime before allowing access to menu options or RPCs.
/// MUMPS: $$HASKEY^XUSRB(DUZ,"ORES") returns 1 if user holds the ORES key.
///
/// These constants are used with [RequiresSecurityKey] attributes on grain methods
/// and checked by the AuthorizationCallFilter (IIncomingGrainCallFilter).
/// </summary>
public static class SecurityKeys
{
    // ─── Order Entry (OE/RR Package) ────────────────────────────────────

    /// <summary>
    /// ORES — Order Entry/Results Reporting (physician order entry).
    /// Required to place, sign, and discontinue orders.
    /// VistA: Held by providers with ordering privileges.
    /// </summary>
    public const string ORES = "ORES";

    /// <summary>
    /// ORELSE — Order Entry nurse/clerk mode.
    /// Allows entering orders on behalf of a provider (flagged for cosignature).
    /// VistA: Held by nurses and ward clerks who enter verbal/telephone orders.
    /// </summary>
    public const string ORELSE = "ORELSE";

    /// <summary>
    /// OREMAS — Order Entry master (admin-level order management).
    /// Allows editing/canceling any order regardless of ownership.
    /// </summary>
    public const string OREMAS = "OREMAS";

    // ─── Provider Actions ───────────────────────────────────────────────

    /// <summary>
    /// PROVIDER — General provider key.
    /// Required for clinical documentation, signing notes, recording assessments.
    /// VistA: File #200 field 53.5 PROVIDER TYPE must also be set.
    /// </summary>
    public const string PROVIDER = "PROVIDER";

    // ─── Pharmacy (PSO/PSJ/PSA Packages) ────────────────────────────────

    /// <summary>
    /// PSO PHARMACY — Outpatient pharmacy operations.
    /// Required to verify outpatient prescriptions, manage refills.
    /// </summary>
    public const string PSO_PHARMACY = "PSO PHARMACY";

    /// <summary>
    /// PSJ RPHARM — Inpatient pharmacy (ward pharmacist).
    /// Required to verify inpatient medication orders, manage IV admixtures.
    /// </summary>
    public const string PSJ_RPHARM = "PSJ RPHARM";

    /// <summary>
    /// PSA ORDERS — Drug accountability.
    /// Required to record receipts, dispenses, waste, and inventory counts.
    /// </summary>
    public const string PSA_ORDERS = "PSA ORDERS";

    /// <summary>
    /// PSB MANAGER — BCMA manager key.
    /// Required for BCMA configuration and override operations.
    /// </summary>
    public const string PSB_MANAGER = "PSB MANAGER";

    // ─── Laboratory (LR Package) ────────────────────────────────────────

    /// <summary>
    /// LRLAB — General lab access.
    /// Required for specimen collection, result entry, and lab order management.
    /// </summary>
    public const string LRLAB = "LRLAB";

    /// <summary>
    /// LRVERIFY — Lab result verification.
    /// Required to verify and release lab results. Held by lab supervisors.
    /// </summary>
    public const string LRVERIFY = "LRVERIFY";

    /// <summary>
    /// LRSU — Lab supervisor key.
    /// Required for lab instrument configuration and auto-verify rule management.
    /// </summary>
    public const string LRSU = "LRSU";

    // ─── Radiology (RA Package) ─────────────────────────────────────────

    /// <summary>
    /// RA VERIFY — Radiology result verification.
    /// Required to verify and release radiology reports.
    /// </summary>
    public const string RA_VERIFY = "RA VERIFY";

    /// <summary>
    /// RA TECHNOLOGIST — Radiology technologist actions.
    /// Required to record exam completion and technologist notes.
    /// </summary>
    public const string RA_TECHNOLOGIST = "RA TECHNOLOGIST";

    // ─── Clinical Documentation (TIU Package) ───────────────────────────

    /// <summary>
    /// TIU SIGN — Sign clinical documents.
    /// Required to apply electronic signature to TIU documents/notes.
    /// </summary>
    public const string TIU_SIGN = "TIU SIGN";

    /// <summary>
    /// TIU COSIGN — Cosign clinical documents.
    /// Required to cosign documents authored by trainees/residents.
    /// </summary>
    public const string TIU_COSIGN = "TIU COSIGN";

    // ─── Allergies (GMRA Package) ───────────────────────────────────────

    /// <summary>
    /// GMRA ALLERGY — Allergy/ADR entry and management.
    /// Required to record, verify, and mark allergies.
    /// </summary>
    public const string GMRA_ALLERGY = "GMRA ALLERGY";

    // ─── Vitals (GMRV Package) ──────────────────────────────────────────

    /// <summary>
    /// GMRV VITALS — Vitals entry.
    /// Required to record and edit vital signs.
    /// </summary>
    public const string GMRV_VITALS = "GMRV VITALS";

    // ─── Problem List (GMPL Package) ────────────────────────────────────

    /// <summary>
    /// GMPL PROBLEM — Problem list management.
    /// Required to add, inactivate, and modify problems.
    /// </summary>
    public const string GMPL_PROBLEM = "GMPL PROBLEM";

    // ─── Scheduling (SD Package) ────────────────────────────────────────

    /// <summary>
    /// SD SCHEDULING — Appointment scheduling.
    /// Required to schedule, reschedule, and cancel appointments.
    /// </summary>
    public const string SD_SCHEDULING = "SD SCHEDULING";

    // ─── ADT (DG Package) ───────────────────────────────────────────────

    /// <summary>
    /// DG ADMIT — Admission/discharge/transfer operations.
    /// Required to record admissions, discharges, and transfers.
    /// </summary>
    public const string DG_ADMIT = "DG ADMIT";

    /// <summary>
    /// DG BED CONTROL — bed/room/unit structure management, bed blocking,
    /// out-of-service, EVS turnover, and inter-facility transfer coordination.
    /// Mirrors the VistA DGPM Bed Control menu.
    /// </summary>
    public const string DG_BED_CONTROL = "DG BED CONTROL";

    /// <summary>
    /// DG SENSITIVITY — Patient sensitivity management.
    /// Required to set sensitivity flags and manage authorized provider lists.
    /// </summary>
    public const string DG_SENSITIVITY = "DG SENSITIVITY";

    // ─── Registration (DG Package) ──────────────────────────────────────

    /// <summary>
    /// CanRegisterPatients — register new patients in the system.
    /// Required to create a fresh patient record (assign ICN, populate
    /// demographics, MPI correlation). Each org maps this key onto whichever
    /// roles do registration: VistA-aligned sites typically grant it to
    /// RegistrationClerk and Administrator; non-VA orgs may scope it
    /// differently (e.g., front-desk staff, nurses, the patient self in
    /// some self-registration deployments).
    /// </summary>
    public const string CanRegisterPatients = "CanRegisterPatients";

    /// <summary>
    /// CanMergePatients — merge a duplicate patient record into a surviving one.
    /// Destructive in effect: clinical data is moved, the source record is
    /// deactivated, and the source ICN is permanently aliased to the target ICN.
    /// Each org typically restricts this to senior registration staff,
    /// HIM/Privacy officers, or system administrators.
    /// VistA equivalent: DG MERGE (File #15.1).
    /// </summary>
    public const string CanMergePatients = "CanMergePatients";

    /// <summary>
    /// CanAuthorizeChs — approve or deny Contract Health Services (CHS / PRC)
    /// authorization requests under 25 CFR Part 136. CHS dollars are finite
    /// per fiscal year and approvals must consider eligibility, alternate
    /// resources, and IHS Medical Priority Class. Tribal health authorities
    /// typically grant this to a designated CHS coordinator role.
    /// </summary>
    public const string CanAuthorizeChs = "CanAuthorizeChs";

    /// <summary>
    /// CanSubmitGpra — package and submit GPRA aggregate quality reports to
    /// the IHS national office (per the GPRA+ submission spec). Authorized
    /// users can format a completed <c>IGpraReportGrain</c> into the IHS-
    /// required submission file and record the transmission outcome. Tribal
    /// authorities typically grant this to a quality / GPRA coordinator role.
    /// </summary>
    public const string CanSubmitGpra = "CanSubmitGpra";

    /// <summary>
    /// CanSubmitNdw — package and submit per-patient extracts to the IHS
    /// National Data Warehouse (NDW). NDW exports include demographics,
    /// problems, immunizations, and (eventually) encounters/labs/pharmacy
    /// in IHS-defined schemas. Tribal authorities typically grant this to
    /// a designated data-warehouse coordinator role, distinct from the
    /// GPRA coordinator since NDW touches identifiable patient data.
    /// </summary>
    public const string CanSubmitNdw = "CanSubmitNdw";

    /// <summary>
    /// CanManageDiabetesRegistry — record diabetes-specific measurements
    /// (HbA1c, foot/eye exam, eGFR, ACR) and enroll patients in the
    /// disease-specific diabetes registry. Read of the registry snapshot /
    /// pre-visit plan is open to clinicians; only mutating operations
    /// require this key. Tribal authorities typically grant to providers
    /// and the diabetes-program coordinator.
    /// </summary>
    public const string CanManageDiabetesRegistry = "CanManageDiabetesRegistry";

    // ─── Surgery (SR Package) ───────────────────────────────────────────

    /// <summary>
    /// SR SURGERY — Surgery package access.
    /// Required to manage surgical cases, record operative reports.
    /// </summary>
    public const string SR_SURGERY = "SR SURGERY";

    // ─── Consults (GMRC Package) ────────────────────────────────────────

    /// <summary>
    /// GMRC CONSULTS — Consult management.
    /// Required to receive, complete, and manage consult requests.
    /// </summary>
    public const string GMRC_CONSULTS = "GMRC CONSULTS";

    // ─── Mental Health (YS Package) ─────────────────────────────────────

    /// <summary>
    /// YS MH INSTRUMENT — Mental health instrument administration.
    /// Required to administer and score mental health screening instruments.
    /// </summary>
    public const string YS_MH_INSTRUMENT = "YS MH INSTRUMENT";

    // ─── Oncology (ONCO Package) ────────────────────────────────────────

    /// <summary>
    /// ONCO MANAGER — Oncology / tumor-registry management.
    /// Required to register tumors, record staging, manage treatment episodes and
    /// radiation-therapy courses, and generate/submit cancer-registry (NAACCR) abstracts.
    /// Oncology data is <b>readable by any clinician</b> — it is part of the chart, not a
    /// privacy silo like Mental Health — so only mutating operations require this key.
    /// Held by oncologists (and tumor registrars).
    /// </summary>
    public const string ONCO_MANAGER = "ONCO MANAGER";

    // ─── Home-Based Care (HBPC / HHC Package) ───────────────────────────

    /// <summary>
    /// HBHC MANAGER — Home-Based Care management (Home-Based Primary Care and, in Phase 2,
    /// Medicare skilled home health). Required to admit/discharge episodes, manage the
    /// interdisciplinary team and plan of care, schedule and document home visits, and record
    /// comprehensive assessments. Held by the home-care team (HBPC nurses, PT/OT/SLP, social
    /// work, the HBPC medical director). Home-care data is <b>readable by any clinician</b> — it
    /// is part of the chart for care coordination, not a privacy silo — so only mutating
    /// operations require this key. VistA equivalent: the HBPC (HBH) menu keys.
    /// </summary>
    public const string HBHC_MANAGER = "HBHC MANAGER";

    // ─── Emerging-Condition Surveillance (EPI) ──────────────────────────

    /// <summary>
    /// EPI MANAGER — emerging-condition / outbreak surveillance management.
    /// Required to create, edit, refine, and promote <c>ProtoCondition</c> clusters, run
    /// screening sweeps, and confirm/exclude cluster membership. Also lets an epidemiologist
    /// chart-review-import coded symptom observations. Surveillance <b>reads are open</b> to any
    /// clinician (like Oncology/Home-Care — it is part of the chart, not a privacy silo); only
    /// mutating operations require this key. Held by infection preventionists / epidemiologists.
    /// No direct VistA analogue — surveillance in VistA lived in PCE/PCC surveillance menus.
    /// </summary>
    public const string EPI_MANAGER = "EPI MANAGER";

    // ─── System Administration (XU Package) ─────────────────────────────

    /// <summary>
    /// XUPROG — Programmer access.
    /// Full system access — bypasses all security key checks.
    /// VistA: The "superuser" key. Use with extreme caution.
    /// </summary>
    public const string XUPROG = "XUPROG";

    /// <summary>
    /// XUMGR — System manager.
    /// Required for user management, key allocation, and system configuration.
    /// </summary>
    public const string XUMGR = "XUMGR";

    /// <summary>
    /// XUAUDIT — Audit trail management.
    /// Required to view and manage system audit logs.
    /// </summary>
    public const string XUAUDIT = "XUAUDIT";

    // ─── Interoperability ───────────────────────────────────────────────

    /// <summary>
    /// FHIR ACCESS — FHIR API access.
    /// Required for external FHIR client operations (ONC §170.315(g)(10)).
    /// </summary>
    public const string FHIR_ACCESS = "FHIR ACCESS";

    /// <summary>
    /// MPI MANAGER — Master Patient Index management.
    /// Required to correlate, merge, and manage patient identifiers across facilities.
    /// </summary>
    public const string MPI_MANAGER = "MPI MANAGER";
}
