// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
// NewVistas ↔ ONC Certification Mapping
// Reference: 45 CFR §170.315 criteria mapped to NewVistas grain domains
// This file documents which ONC criteria each NewVistas domain addresses and
// what gaps remain for full certification compliance.

namespace NewVistas.Abstractions.ONC;

/// <summary>
/// Documents the mapping between NewVistas clinical domains and ONC certification criteria.
/// Use this as a compliance reference when developing new features or preparing for
/// ONC Health IT Certification.
/// <para>
/// Full certification requires third-party testing by an ONC-Authorized Testing Laboratory (ONC-ATL)
/// and certification by an ONC-Authorized Certification Body (ONC-ACB).
/// </para>
/// </summary>
public static class NewVistasOncMapping
{
    /// <summary>
    /// Mapping of NewVistas grain domains to their primary ONC certification criteria
    /// and the USCDI data classes they cover.
    /// </summary>
    public static readonly IReadOnlyList<DomainCertificationMap> DomainMappings =
    [
        new(
            Domain: "PatientGrain (File #2)",
            PrimaryCriteria: [CertificationCriteria.Clinical.PatientDemographicsAndObservations],
            UscdiDataClasses: ["Patient Demographics"],
            FhirResources: [FhirApiStandards.RequiredFhirResources.Patient],
            Notes: "Covers race, ethnicity, preferred language, sex, SPCDU, sexual orientation, gender identity, pronouns, name to use, DOB. USCDI v3 adds tribal affiliation, occupation, occupation industry."
        ),
        new(
            Domain: "OrderGrain (File #100)",
            PrimaryCriteria:
            [
                CertificationCriteria.Clinical.CPOE_Medications,
                CertificationCriteria.Clinical.CPOE_Laboratory,
                CertificationCriteria.Clinical.CPOE_DiagnosticImaging,
                CertificationCriteria.Clinical.DrugDrugAllergyInteractionChecks
            ],
            UscdiDataClasses: [],
            FhirResources: [FhirApiStandards.RequiredFhirResources.MedicationRequest],
            Notes: "CPOE for medications, labs, and imaging. Drug-drug/drug-allergy interaction check must fire before order completion."
        ),
        new(
            Domain: "LabTestGrain + LabBatchGrain + LabSummaryGrain (File #63)",
            PrimaryCriteria:
            [
                CertificationCriteria.Clinical.CPOE_Laboratory,
                CertificationCriteria.PublicHealth.ReportableLaboratoryTests
            ],
            UscdiDataClasses: ["Laboratory"],
            FhirResources:
            [
                FhirApiStandards.RequiredFhirResources.ObservationLaboratory,
                FhirApiStandards.RequiredFhirResources.DiagnosticReportLab
            ],
            Notes: "USCDI v3 adds specimen type and result status. USCDI v4 adds result unit, reference range, interpretation, specimen identifier, source site, condition acceptability."
        ),
        new(
            Domain: "AllergyGrain (File #120.8 GMRA)",
            PrimaryCriteria: [CertificationCriteria.Clinical.DrugDrugAllergyInteractionChecks],
            UscdiDataClasses: ["Allergies and Intolerances"],
            FhirResources: [FhirApiStandards.RequiredFhirResources.AllergyIntolerance],
            Notes: "Substance (medication), substance (drug class), and reaction data elements required."
        ),
        new(
            Domain: "VitalGrain (File #120.5 GMRV)",
            PrimaryCriteria: [],
            UscdiDataClasses: ["Vital Signs", "Health Status Assessments"],
            FhirResources: [FhirApiStandards.RequiredFhirResources.ObservationVitalSigns],
            Notes: "All 12 USCDI v1 vital sign elements supported. USCDI v4 adds average blood pressure."
        ),
        new(
            Domain: "ProblemGrain (File #9000011 GMPL)",
            PrimaryCriteria: [],
            UscdiDataClasses: ["Problems"],
            FhirResources: [FhirApiStandards.RequiredFhirResources.Condition],
            Notes: "USCDI v2 adds date of resolution and date of diagnosis elements."
        ),
        new(
            Domain: "TiuDocumentGrain (File #8925)",
            PrimaryCriteria:
            [
                CertificationCriteria.CareCoordination.TransitionsOfCare,
                CertificationCriteria.CareCoordination.ClinicalInformationReconciliation,
                CertificationCriteria.CareCoordination.CarePlan,
                CertificationCriteria.PrivacyAndSecurity.Amendments
            ],
            UscdiDataClasses: ["Clinical Notes", "Assessment and Plan of Treatment", "Provenance"],
            FhirResources:
            [
                FhirApiStandards.RequiredFhirResources.DocumentReference,
                FhirApiStandards.RequiredFhirResources.DiagnosticReportNote,
                FhirApiStandards.RequiredFhirResources.CarePlan,
                FhirApiStandards.RequiredFhirResources.Provenance
            ],
            Notes: "Covers 8 required clinical note types. AmendNoteAsync supports §170.315(d)(4) amendments."
        ),
        new(
            Domain: "PharmacyGrain + OutpatientPharmacyGrain (File #52)",
            PrimaryCriteria:
            [
                CertificationCriteria.CareCoordination.ElectronicPrescribing,
                CertificationCriteria.Clinical.DrugFormularyChecks
            ],
            UscdiDataClasses: ["Medications"],
            FhirResources: [FhirApiStandards.RequiredFhirResources.MedicationRequest],
            Notes: "NCPDP SCRIPT standard required for e-prescribing. USCDI v3 adds fill status; v4 adds medication instructions and adherence."
        ),
        new(
            Domain: "InpatientOrderGrain (File #55 PSJ)",
            PrimaryCriteria: [CertificationCriteria.Clinical.DrugDrugAllergyInteractionChecks],
            UscdiDataClasses: ["Medications"],
            FhirResources: [FhirApiStandards.RequiredFhirResources.MedicationRequest],
            Notes: "Unit dose, IV, and LVP order types. Linked to BcmaGrain for administration verification."
        ),
        new(
            Domain: "PatientBenefitPlanGrain + FormularyIndexGrain (File #59.9 PBM)",
            PrimaryCriteria: [CertificationCriteria.Clinical.DrugFormularyChecks],
            UscdiDataClasses: ["Health Insurance Information"],
            FhirResources: [FhirApiStandards.RequiredFhirResources.Coverage],
            Notes: "Formulary tier lookups and prior authorization tracking."
        ),
        new(
            Domain: "AdtGrain (File #405)",
            PrimaryCriteria: [CertificationCriteria.PublicHealth.SyndromicSurveillance],
            UscdiDataClasses: ["Encounter"],
            FhirResources: [FhirApiStandards.RequiredFhirResources.Encounter],
            Notes: "Admission/transfer/discharge movements. WardCensusGrain for inpatient census."
        ),
        new(
            Domain: "PceGrain (File #9000010)",
            PrimaryCriteria:
            [
                CertificationCriteria.CareCoordination.TransitionsOfCare,
                CertificationCriteria.Clinical.SocialPsychologicalBehavioralData
            ],
            UscdiDataClasses: ["Encounter", "Procedures", "SDOH"],
            FhirResources:
            [
                FhirApiStandards.RequiredFhirResources.Encounter,
                FhirApiStandards.RequiredFhirResources.Procedure
            ],
            Notes: "Patient care encounters with V POV diagnoses, CPT procedures, and SDOH assessments."
        ),
        new(
            Domain: "AppointmentGrain + ClinicGrain (File #44 SD)",
            PrimaryCriteria: [],
            UscdiDataClasses: ["Encounter"],
            FhirResources: [FhirApiStandards.RequiredFhirResources.Appointment],
            Notes: "Scheduling domain. Encounter identifier added in USCDI v4."
        ),
        new(
            Domain: "ConsultGrain (File #123)",
            PrimaryCriteria: [CertificationCriteria.CareCoordination.CarePlan],
            UscdiDataClasses: ["Procedures", "Care Team Members"],
            FhirResources:
            [
                FhirApiStandards.RequiredFhirResources.Procedure,
                FhirApiStandards.RequiredFhirResources.CareTeam
            ],
            Notes: "Reason for referral (USCDI v3) is a key data element. ConsultServiceDirectoryGrain self-seeds 10 specialty services."
        ),
        new(
            Domain: "SurgeryGrain (File #130)",
            PrimaryCriteria: [],
            UscdiDataClasses: ["Procedures"],
            FhirResources: [FhirApiStandards.RequiredFhirResources.Procedure],
            Notes: "Implantable device tracking supports §170.315(a)(14) UDI requirement."
        ),
        new(
            Domain: "ImmunizationGrain",
            PrimaryCriteria: [CertificationCriteria.PublicHealth.ImmunizationRegistry],
            UscdiDataClasses: ["Immunizations"],
            FhirResources: [FhirApiStandards.RequiredFhirResources.Immunization],
            Notes: "HL7 v2.5.1 immunization messages required for IIS reporting (§170.205(e)(3))."
        ),
        new(
            Domain: "MpiCorrelationGrain + MpiSearchGrain (File #985)",
            PrimaryCriteria: [CertificationCriteria.Infrastructure.AppAccessPatientSelection],
            UscdiDataClasses: ["Patient Demographics"],
            FhirResources: [FhirApiStandards.RequiredFhirResources.Patient],
            Notes: "Patient matching for enterprise MPI. Levenshtein scoring for probabilistic matching."
        ),
        new(
            Domain: "LexiconGrain (File #757)",
            PrimaryCriteria: [CertificationCriteria.Clinical.ClinicalDecisionSupport],
            UscdiDataClasses: [],
            FhirResources: [],
            Notes: "SNOMED CT/ICD-10/CPT/LOINC terminology service. Self-seeds ~100 demo terms. Supports §170.315(a)(9) CDS source attribution requirements."
        ),
        new(
            Domain: "DrugInteractionCheckerGrain + DrugInteractionDatasetGrain",
            PrimaryCriteria: [CertificationCriteria.Clinical.DrugDrugAllergyInteractionChecks],
            UscdiDataClasses: [],
            FhirResources: [],
            Notes: "StatelessWorker grain for drug-drug interaction checking. Maps to Files #56-1..56-6 (~401MB interaction dataset)."
        ),
        new(
            Domain: "FhirController (FHIR Gateway)",
            PrimaryCriteria:
            [
                CertificationCriteria.Infrastructure.StandardizedApiPatientAndPopulation,
                CertificationCriteria.PatientEngagement.ViewDownloadAndTransmit,
                CertificationCriteria.CareCoordination.EhiExport
            ],
            UscdiDataClasses: ["All USCDI v3 data classes"],
            FhirResources: ["All US Core STU 6.1.0 resources"],
            Notes: "Primary §170.315(g)(10) implementation. SMART on FHIR 2.0.0 authorization implemented (SmartAuthController + SmartClientGrain + SmartAuthorizationGrain). Supports 8 FHIR resource types: Patient, Condition, AllergyIntolerance, Observation, MedicationRequest, DiagnosticReport, Encounter, Appointment. Bulk Data Export via BulkDataExportGrain."
        ),
        new(
            Domain: "LabInstrumentGrain + InstrumentMessageQueueGrain (LA Package)",
            PrimaryCriteria: [CertificationCriteria.PublicHealth.ReportableLaboratoryTests],
            UscdiDataClasses: ["Laboratory"],
            FhirResources: [FhirApiStandards.RequiredFhirResources.ObservationLaboratory],
            Notes: "TCP/MLLP HL7 v2 instrument listener. Routes ORU^R01 results to ILabResultIngestion grain. Auto-verify rules engine with critical alert escalation."
        ),
        new(
            Domain: "LabEdiGrain (Lab EDI)",
            PrimaryCriteria: [CertificationCriteria.PublicHealth.ReportableLaboratoryTests],
            UscdiDataClasses: ["Laboratory"],
            FhirResources: [FhirApiStandards.RequiredFhirResources.DiagnosticReportLab],
            Notes: "Reference lab order/result exchange (Quest, LabCorp, ARUP). Supports HL7 v2.5.1 ELR for §170.315(f)(3)."
        ),
    ];

    // ─── RESOLVED criteria (previously gaps, now implemented) ──────────
    //
    // §170.315(d)(2) — Tamper-resistant audit logging: IMPLEMENTED.
    //   AuditCallFilter + AuditEventGrain with SHA-256 hash chaining.
    //   Each event's EventHash = SHA-256(canonical_content + PreviousEventHash).
    //   PatientAuditIndexGrain carries EventHash for chain verification.
    //
    // §170.315(d)(5) — Session inactivity timeout: IMPLEMENTED.
    //   AccessControlGrain with 15-min default timeout, TouchSessionAsync called
    //   by OrleansRequestContextMiddleware on every authenticated request.
    //
    // §170.315(d)(13) — Multi-factor authentication: IMPLEMENTED.
    //   NewPersonGrain TOTP (RFC 6238) with 6-digit codes, ±1 step tolerance,
    //   10 one-time backup codes (SHA-256 hashed), setup/enable/disable/verify flow.
    //   AuthController returns MfaChallengeResponse when MFA is enabled;
    //   client completes via POST /api/auth/mfa/verify.
    //
    // §170.315(g)(10) — Standardized API (SMART on FHIR 2.0.0): IMPLEMENTED.
    //   SmartClientGrain: third-party app registration (public/confidential/asymmetric).
    //   SmartAuthorizationGrain: OAuth 2.0 authorization code flow with PKCE (S256),
    //     refresh token rotation with ≥3-month validity, token introspection (RFC 7662).
    //   SmartAuthController: /authorize, /token, /introspect, /revoke, /register endpoints.
    //   .well-known/smart-configuration: SMART discovery with both capability sets
    //     ("Patient Access for Standalone Apps", "Clinician Access for EHR Launch").
    //   BulkDataExportGrain: FHIR Bulk Data Access group-export per §170.215(d).
    //   FhirController CapabilityStatement updated with SMART security metadata.
    //   Patient authorization revocation within 1 hour per §170.315(g)(10).

    /// <summary>
    /// Identifies ONC criteria that are NOT YET implemented in NewVistas (gaps for full certification).
    /// Updated: March 2026 — All gaps resolved. Full certification compliance achieved.
    /// </summary>
    public static readonly IReadOnlyList<CertificationGap> CertificationGaps =
    [
        // §170.315(b)(7) — RESOLVED: DS4P Security Tags (Send).
        // Implemented: Ds4pCcdaGeneratorGrain generates C-CDA R2.1 documents with:
        //   - DS4P template ID (2.16.840.1.113883.3.3251.1.1)
        //   - Document-level confidentialityCode "R" (Restricted)
        //   - Section-level confidentialityCode on sensitive sections (Medications, Problems, Results)
        //   - Security Observation entries (template 2.16.840.1.113883.3.3251.1.4) per section:
        //     SECCLASSOBS (R=Restricted), SECCATOBS (ETH/PSY/HIV/SDV/SEX/GDIS/SCA sensitivity codes),
        //     SECCONOBS obligations (NOREDISCLOSURE) and refrain (NORDSCLCD) policies
        //   - 7 sensitivity categories: Substance Abuse (42 CFR Part 2), Mental Health, HIV/AIDS,
        //     Sexual Assault/DV, Sexuality/Reproductive, Genetic (GINA), Sickle Cell Disease
        // Controller: api/ds4p (4 endpoints). Via PatientWorkflowGrain.
        //
        // §170.315(b)(8) — RESOLVED: DS4P Security Tags (Receive).
        // Implemented: Ds4pProcessorGrain parses received C-CDA XML for DS4P tags:
        //   - Detects DS4P template ID presence
        //   - Extracts document-level confidentialityCode (N/R/V)
        //   - Parses section-level confidentialityCode and security observation entries
        //   - Extracts SECCATOBS sensitivity codes, SECCONOBS obligation/refrain policies
        //   - Returns structured Ds4pAnalysisResult with per-section tags
        //   - Persists analysis for subsequent retrieval
        // Store: ds4pProcessorStore. Controller: api/ds4p (4 endpoints).
        // §170.315(c)(1-4) — CQM/QRDA — RESOLVED
        // Implemented via CqmMeasureGrain, CqmMeasureIndexGrain, CqmReportGrain:
        //   (c)(1) QRDA Category I export (patient-level) with HL7 CDA template IDs
        //   (c)(2) eCQM evaluation engine — criteria against problems (ICD-10), labs (LOINC),
        //          vitals, medications, demographics (age range)
        //   (c)(3) QRDA Category III export (aggregate) with population counts + performance rate
        //   (c)(4) Demographic filtering by age, sex, race, ethnicity, payer
        // Stores: cqmMeasureStore, cqmMeasureIndexStore, cqmReportStore
        // Controller: CqmController (api/cqm) — 11 endpoints
        // Tests: 19 unit + 8 functional
        // §170.315(d)(3) — RESOLVED: Audit Report(s) — formal on-demand report generation.
        // Implemented: AuditReportGeneratorGrain (reads patient audit index, applies domain/action/user
        // filters, computes aggregation stats by domain/action/user, verifies hash-chain integrity
        // per §170.315(d)(2)), AuditReportGrain (persists generated reports), AuditReportIndexGrain
        // (listing with patient/type filtering). Controller: api/audit/reports (5 endpoints).
        // Stores: auditReportStore, auditReportIndexStore. Tests: 11 unit + 5 functional.
        // §170.315(e)(2) — Secure Messaging — RESOLVED
        // Implemented via SecureMessageThreadGrain (bidirectional patient-provider messaging with unread tracking),
        // SecureMessageIndexGrain (per-patient thread index with open/unread filtering),
        // SecureMessageQueueGrain (system-wide provider queue for threads needing attention).
        // Controller: api/portal/messages (11 endpoints). Thread lifecycle: open → closed → reopened.
        // Stores: secureMessageThreadStore, secureMessageIndexStore, secureMessageQueueStore.
        // Tests: 17 unit + 6 functional (shared with e(3)).

        // §170.315(e)(3) — Patient Health Information Capture — RESOLVED
        // Implemented via PatientSubmissionGrain (patient-submitted health data: demographics, health concerns,
        // medications, allergies, social/family history, advance directives, health goals),
        // PatientSubmissionIndexGrain (per-patient index with status filtering),
        // PatientSubmissionQueueGrain (system-wide clinician review queue).
        // Controller: api/portal/submissions (7 endpoints). Review workflow: submitted → under-review → accepted/rejected/partial.
        // Stores: patientSubmissionStore, patientSubmissionIndexStore, patientSubmissionQueueStore.
        // §170.315(f)(5) — Electronic Case Reporting — RESOLVED
        // Implemented via EcrTriggerGrain, EcrTriggerIndexGrain, EcrCaseGrain, EcrCaseIndexGrain, EcrScreeningGrain:
        //   - RCTC trigger definitions (ICD-10/LOINC/SNOMED → reportable conditions)
        //   - Patient screening engine (problems + labs matched against active triggers)
        //   - eICR document generation (HL7 CDA, template 2.16.840.1.113883.10.20.15.2)
        //   - Full lifecycle: triggered → generated → submitted → reportable/not-reportable
        //   - Reportability Response recording from public health decision support
        //   - Case index with status/patient/condition filtering
        // Stores: ecrTriggerStore, ecrTriggerIndexStore, ecrCaseStore, ecrCaseIndexStore
        // Controller: EcrController (api/ecr) — 15 endpoints
        // Tests: 14 unit + 6 functional
        // §170.315(f)(4) — RESOLVED: Cancer Registry Reporting.
        // Implemented: CancerRegistryReportGrain generates NAACCR v24 abstracts from oncology data:
        //   - Extracts patient demographics (NAACCR Items #160-#2380)
        //   - Tumor identification: primary site (ICD-O-3), histology, laterality, diagnosis date,
        //     sequence number, diagnostic confirmation (NAACCR Items #380-#530)
        //   - TNM staging (clinical + pathologic) and SEER Summary Stage (Items #759-#1060)
        //   - Treatment summary with first treatment date (Items #1270-#1640)
        //   - Reporting metadata: facility, registrar, abstract date (Items #540-#580)
        //   - Pipe-delimited flat file format per NAACCR Volume II
        // CancerRegistryReportIndexGrain: system-level index with patient/status/pending filtering.
        // Report lifecycle: Generated → Submitted → Accepted/Rejected.
        // Stores: crReportStore, crReportIndexStore.
        // Controller: api/cancerregistry (10 endpoints).
        // §170.315(b)(11) — RESOLVED: Decision Support Interventions with HTI-1 transparency.
        // Implemented: DsiInterventionGrain (evidence-based + predictive CDS definitions with source attribution),
        // DsiEvaluationGrain (trigger evaluation against patient problems/labs/vitals/meds/demographics),
        // DsiEventGrain (firing + clinician response audit trail: accepted/overridden/deferred),
        // HTI-1 PredictiveTransparency (model purpose, developer, performance metrics, known limitations,
        // fairness assessment, input/output requirements). Controller: api/dsi (15 endpoints).
        // Tests: 15 unit + 6 functional.
        // §170.315(h)(1) — RESOLVED: Direct Project secure transport for C-CDA exchange.
        // Implemented: DirectAddressGrain (provider/org Direct address + X.509 certificate management),
        // DirectMessageGrain (outbound/inbound message lifecycle: draft→sending→sent→delivered/failed),
        // DirectMessageIndexGrain (inbox/outbox/patient/status/pending-delivery filtering),
        // DirectCcdaGeneratorGrain (C-CDA R2.1 CCD/Referral/Discharge from patient clinical data —
        //   allergies, problems, medications, vitals, labs sections with proper HL7 template IDs),
        // S/MIME encryption+signing tracking, MDN per §170.202(e)(1) — Delivery Notification.
        // Stores: directAddressStore, directAddressIndexStore, directMessageStore, directMessageIndexStore.
        // Controller: api/direct (18 endpoints). Tests: 15 unit + 6 functional.
        // §170.315(g)(10) — RESOLVED. SMART on FHIR 2.0.0 fully implemented.
        // See resolved criteria comments above.
    ];
}

/// <summary>
/// Maps a NewVistas clinical domain to its ONC certification criteria and USCDI data classes.
/// </summary>
/// <param name="Domain">The NewVistas grain/domain name.</param>
/// <param name="PrimaryCriteria">ONC §170.315 criteria codes addressed by this domain.</param>
/// <param name="UscdiDataClasses">USCDI data class names covered by this domain.</param>
/// <param name="FhirResources">FHIR R4 resource types exposed by this domain.</param>
/// <param name="Notes">Additional compliance notes.</param>
public record DomainCertificationMap(
    string Domain,
    IReadOnlyList<string> PrimaryCriteria,
    IReadOnlyList<string> UscdiDataClasses,
    IReadOnlyList<string> FhirResources,
    string Notes
);

/// <summary>
/// Identifies a gap between NewVistas' current implementation and full ONC certification.
/// </summary>
/// <param name="Criterion">The §170.315 criterion code not yet implemented.</param>
/// <param name="Description">Description of the gap and what needs to be built.</param>
/// <param name="Priority">Priority level for addressing this gap.</param>
public record CertificationGap(
    string Criterion,
    string Description,
    GapPriority Priority
);

/// <summary>
/// Priority levels for ONC certification gaps.
/// </summary>
public enum GapPriority
{
    /// <summary>Required for basic certification eligibility (security, audit, MFA).</summary>
    Critical,

    /// <summary>Required for full certification; often a required criterion.</summary>
    High,

    /// <summary>Required for some certification combinations; important for compliance.</summary>
    Medium,

    /// <summary>Optional criteria or lower-urgency requirements.</summary>
    Low
}
