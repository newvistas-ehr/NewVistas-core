// ONC Standardized API Standards — 45 CFR §170.315(g)(10) and §170.215
// Reference: ONC 21st Century Cures Act Final Rule (85 FR 25641, May 1, 2020)
// Updated by HTI-1 Final Rule (89 FR 1429, January 9, 2024)
// Key compliance date: January 1, 2026 — US Core STU 6.1.0 and SMART 2.0.0 become mandatory

namespace NewVistas.Abstractions.ONC;

/// <summary>
/// Standards and requirements for the §170.315(g)(10) Standardized API for patient
/// and population services criterion.
/// This is the primary modern API criterion requiring FHIR R4 + SMART on FHIR.
/// All FHIR API operations in NewVistas should conform to these standards.
/// Maps to: NewVistas FhirController, FhirGatewayWorkflow.
/// </summary>
public static class FhirApiStandards
{
    /// <summary>
    /// Adopted API standards under 45 CFR §170.215.
    /// </summary>
    public static class AdoptedStandards
    {
        /// <summary>§170.215(a)(1) — HL7 FHIR Release 4.0.1 (FHIR R4).
        /// The foundational FHIR standard. All API responses must conform to FHIR R4.
        /// </summary>
        public const string FhirR4 = "HL7 FHIR Release 4.0.1";

        /// <summary>§170.215(b)(1)(i) — HL7 FHIR US Core STU 3.1.1.
        /// Maps to USCDI v1.
        /// Status: TRANSITIONAL — expires January 1, 2026.
        /// </summary>
        public const string UsCoreStu3_1_1 = "HL7 FHIR US Core STU 3.1.1";

        /// <summary>§170.215(b)(1)(ii) — HL7 FHIR US Core STU 6.1.0.
        /// Maps to USCDI v3.
        /// Status: CURRENTLY REQUIRED; mandatory from January 1, 2026.
        /// </summary>
        public const string UsCoreStu6_1_0 = "HL7 FHIR US Core STU 6.1.0";

        /// <summary>§170.215(c)(1) — SMART App Launch IG Release 1.0.0.
        /// Status: TRANSITIONAL — expires January 1, 2026.
        /// </summary>
        public const string SmartAppLaunch1_0_0 = "SMART App Launch IG Release 1.0.0";

        /// <summary>§170.215(c)(2) — SMART App Launch IG Release 2.0.0.
        /// Requires "Patient Access for Standalone Apps" and "Clinician Access for EHR Launch"
        /// capability sets, plus Token Introspection.
        /// Status: CURRENTLY REQUIRED; mandatory from January 1, 2026.
        /// </summary>
        public const string SmartAppLaunch2_0_0 = "SMART App Launch IG Release 2.0.0";

        /// <summary>§170.215(d)(1) — FHIR Bulk Data Access (Flat FHIR) v1.0.0 STU 1.
        /// Required for population-level ("multiple patients") data export.
        /// Mandatory "group-export" OperationDefinition must be supported.
        /// </summary>
        public const string BulkDataAccessV1_0_0 = "FHIR Bulk Data Access (Flat FHIR) v1.0.0 STU 1";

        /// <summary>§170.215(e)(1) — OpenID Connect Core 1.0 (with errata set 1).
        /// Required for user authentication in the FHIR API authorization flow.
        /// </summary>
        public const string OpenIdConnect1_0 = "OpenID Connect Core 1.0 (errata set 1)";
    }

    /// <summary>
    /// Key compliance dates for API standard transitions.
    /// </summary>
    public static class ComplianceDates
    {
        /// <summary>
        /// January 1, 2026: US Core STU 3.1.1 and SMART App Launch 1.0.0 expire.
        /// Systems must be fully transitioned to US Core STU 6.1.0 and SMART 2.0.0.
        /// USCDI v1 adoption also expires; USCDI v3 becomes sole required version.
        /// </summary>
        public const string StandardsTransitionDeadline = "2026-01-01";

        /// <summary>
        /// March 11, 2024: HTI-1 Final Rule effective date.
        /// §170.315(b)(11) (Decision Support Interventions) and §171.206 (Protecting Care Access)
        /// took effect on this date.
        /// </summary>
        public const string Hti1EffectiveDate = "2024-03-11";

        /// <summary>
        /// January 1, 2026: FHIR-based eCR required for §170.315(f)(5).
        /// Electronic case reporting must transition from HL7 v2 to FHIR-based eCR.
        /// </summary>
        public const string FhirBasedEcrRequired = "2026-01-01";
    }

    /// <summary>
    /// Functional requirements for §170.315(g)(10) compliance.
    /// </summary>
    public static class FunctionalRequirements
    {
        /// <summary>
        /// Data Response — Single Patient:
        /// Respond per the US Core Server CapabilityStatement for all USCDI data elements
        /// (mandatory + must-support). Must support all mandatory search parameters.
        /// </summary>
        public const string SinglePatientDataResponse = "US Core Server CapabilityStatement — all USCDI elements";

        /// <summary>
        /// Data Response — Multiple Patients (Bulk):
        /// Respond per §170.215(a), (b)(1), and (d) for group-level queries using
        /// FHIR Bulk Data Access group-export operations.
        /// </summary>
        public const string MultiplePatientBulkExport = "FHIR Bulk Data Access group-export — §170.215(d)";

        /// <summary>
        /// Application Registration:
        /// Allow third-party applications to register with the authorization server.
        /// Registration process must be publicly documented without preconditions.
        /// </summary>
        public const string ApplicationRegistration = "Third-party app registration with authorization server";

        /// <summary>
        /// Secure Connection — Patient/User Scopes:
        /// TLS per SMART on FHIR (§170.215(b)(1) and (c)).
        /// </summary>
        public const string SecureConnectionPatientUserScopes = "TLS per SMART on FHIR §170.215(b)(1) and (c)";

        /// <summary>
        /// Secure Connection — System Scopes (Bulk):
        /// SMART Backend Services Authorization Guide per §170.215(d).
        /// </summary>
        public const string SecureConnectionSystemScopes = "SMART Backend Services Authorization Guide — §170.215(d)";

        /// <summary>
        /// First-Time Authorization:
        /// OAuth 2.0 per SMART on FHIR; must issue refresh tokens with minimum 3-month
        /// validity for confidential apps and capable native apps.
        /// </summary>
        public const string FirstTimeAuthorization = "OAuth 2.0 — SMART on FHIR; refresh tokens ≥3 months";

        /// <summary>
        /// Subsequent Authorization:
        /// Refresh access without requiring re-authorization when a valid refresh token is supplied;
        /// must issue a new refresh token with at least 3-month validity.
        /// </summary>
        public const string SubsequentAuthorization = "Refresh without re-authorization; new ≥3-month refresh token";

        /// <summary>
        /// Patient Authorization Revocation:
        /// The authorization server must revoke an application's access within 1 hour
        /// of a patient's revocation request.
        /// </summary>
        public const string PatientAuthorizationRevocation = "Revoke app access within 1 hour of patient request";

        /// <summary>
        /// Token Introspection:
        /// The authorization server must support token introspection to validate tokens
        /// it has issued per §170.215(c) (SMART App Launch 2.0.0).
        /// </summary>
        public const string TokenIntrospection = "Authorization server validates tokens it has issued — §170.215(c)";

        /// <summary>
        /// Public API Documentation:
        /// API syntax, parameters, return types, exceptions, required software components,
        /// and authorization server registration requirements must be publicly available
        /// via hyperlink without preconditions or additional steps.
        /// </summary>
        public const string PublicApiDocumentation = "Publicly available API docs via hyperlink without preconditions";
    }

    /// <summary>
    /// SMART App Launch capability sets required by §170.215(c)(2) (SMART 2.0.0).
    /// </summary>
    public static class SmartCapabilitySets
    {
        /// <summary>
        /// "Patient Access for Standalone Apps" capability set.
        /// Required for patient-facing applications authenticating outside the EHR session.
        /// </summary>
        public const string PatientAccessForStandaloneApps = "Patient Access for Standalone Apps";

        /// <summary>
        /// "Clinician Access for EHR Launch" capability set.
        /// Required for provider-facing applications launched from within the EHR.
        /// </summary>
        public const string ClinicianAccessForEhrLaunch = "Clinician Access for EHR Launch";
    }

    /// <summary>
    /// FHIR resource types required by US Core STU 6.1.0 (§170.215(b)(1)(ii))
    /// and their mapping to NewVistas grains.
    /// </summary>
    public static class RequiredFhirResources
    {
        /// <summary>Patient — maps to PatientGrain (File #2), MpiCorrelationGrain (File #985).</summary>
        public const string Patient = "Patient";

        /// <summary>Condition — maps to ProblemGrain (File #9000011 GMPL).</summary>
        public const string Condition = "Condition";

        /// <summary>AllergyIntolerance — maps to AllergyGrain (File #120.8 GMRA).</summary>
        public const string AllergyIntolerance = "AllergyIntolerance";

        /// <summary>Observation (vital-signs) — maps to VitalGrain (File #120.5 GMRV).</summary>
        public const string ObservationVitalSigns = "Observation (vital-signs)";

        /// <summary>Observation (laboratory) — maps to LabTestGrain (File #63).</summary>
        public const string ObservationLaboratory = "Observation (laboratory)";

        /// <summary>DiagnosticReport (lab) — maps to LabBatchGrain, LabEdiGrain.</summary>
        public const string DiagnosticReportLab = "DiagnosticReport (lab)";

        /// <summary>DiagnosticReport (note) — maps to TiuDocumentGrain (File #8925).</summary>
        public const string DiagnosticReportNote = "DiagnosticReport (note)";

        /// <summary>DocumentReference — maps to TiuDocumentGrain (File #8925).</summary>
        public const string DocumentReference = "DocumentReference";

        /// <summary>MedicationRequest — maps to PharmacyGrain (File #52), InpatientOrderGrain.</summary>
        public const string MedicationRequest = "MedicationRequest";

        /// <summary>Immunization — maps to ImmunizationGrain.</summary>
        public const string Immunization = "Immunization";

        /// <summary>Procedure — maps to SurgeryGrain (File #130), PceGrain (File #9000010).</summary>
        public const string Procedure = "Procedure";

        /// <summary>Encounter — maps to AdtGrain (File #405), PceGrain (File #9000010), AppointmentGrain (File #44).</summary>
        public const string Encounter = "Encounter";

        /// <summary>CarePlan — maps to TiuDocumentGrain care plan document type.</summary>
        public const string CarePlan = "CarePlan";

        /// <summary>CareTeam — maps to ConsultGrain consulting provider, PatientWorkflowGrain.</summary>
        public const string CareTeam = "CareTeam";

        /// <summary>Goal — maps to TiuDocumentGrain (care plan goals).</summary>
        public const string Goal = "Goal";

        /// <summary>Device — maps to PatientGrain UDI fields.</summary>
        public const string Device = "Device";

        /// <summary>Coverage — maps to PatientBenefitPlanGrain (File #59.9).</summary>
        public const string Coverage = "Coverage";

        /// <summary>Location — maps to WardLocationIndexGrain, ClinicGrain (File #44).</summary>
        public const string Location = "Location";

        /// <summary>Organization — maps to institutional data grains.</summary>
        public const string Organization = "Organization";

        /// <summary>Practitioner — maps to PatientGrain provider fields, ConsultGrain.</summary>
        public const string Practitioner = "Practitioner";

        /// <summary>Provenance — maps to TiuDocumentGrain author tracking.</summary>
        public const string Provenance = "Provenance";

        /// <summary>Appointment — maps to AppointmentGrain (File #44), SchedulingWorkflow.</summary>
        public const string Appointment = "Appointment";

        /// <summary>RelatedPerson — maps to PatientGrain related person demographics (USCDI v3).</summary>
        public const string RelatedPerson = "RelatedPerson";
    }

    /// <summary>
    /// HL7 v2.5.1 standards required for public health reporting criteria (Category f).
    /// Referenced under §170.205.
    /// </summary>
    public static class Hl7V2Standards
    {
        /// <summary>§170.205(e)(3) — HL7 v2.5.1 immunization messages for IIS reporting (§170.315(f)(1)).</summary>
        public const string ImmunizationMessaging = "HL7 v2.5.1 immunization messages — §170.205(e)(3)";

        /// <summary>HL7 v2.5.1 ADT/observation messages for syndromic surveillance (§170.315(f)(2)).</summary>
        public const string SyndromicSurveillanceMessaging = "HL7 v2.5.1 ADT/observation messages — syndromic surveillance";

        /// <summary>HL7 v2.5.1 ELR messages for reportable laboratory tests (§170.315(f)(3)).
        /// Maps to: NewVistas LabEdiGrain, LabInstrumentGrain (LA Package).
        /// </summary>
        public const string ElectronicLaboratoryReporting = "HL7 v2.5.1 ELR messages — §170.315(f)(3)";

        /// <summary>§170.205(r)(1) — Antimicrobial use and resistance reporting (§170.315(f)(6)).</summary>
        public const string AntimicrobialUseAndResistanceReporting = "AU&R data — §170.205(r)(1)";

        /// <summary>§170.205(s)(1) — Health care survey data (§170.315(f)(7)).</summary>
        public const string HealthCareSurveyData = "Health care survey data — §170.205(s)(1)";
    }

    /// <summary>
    /// Direct Project transport standards required under Category (h) and §170.202.
    /// </summary>
    public static class DirectProjectStandards
    {
        /// <summary>§170.202(a)(2) — Applicability Statement for Secure Health Transport (Direct).
        /// Required for §170.315(h)(1).
        /// </summary>
        public const string ApplicabilityStatementForSecureHealthTransport = "§170.202(a)(2) — Direct";

        /// <summary>§170.202(e)(1) — Delivery Notification in Direct.
        /// Required for §170.315(h)(1).
        /// </summary>
        public const string DeliveryNotificationInDirect = "§170.202(e)(1) — Delivery Notification";

        /// <summary>§170.202(b) — XDR edge protocol.
        /// Required for §170.315(h)(2).
        /// </summary>
        public const string XdrEdgeProtocol = "§170.202(b) — XDR";

        /// <summary>§170.202(d) — XDM edge protocol.
        /// Required for §170.315(h)(2).
        /// </summary>
        public const string XdmEdgeProtocol = "§170.202(d) — XDM";
    }

    /// <summary>
    /// Encryption and transport security standards under §170.210.
    /// </summary>
    public static class SecurityStandards
    {
        /// <summary>§170.210(a)(2) — TLS encryption for data in transit (§170.315(d)(9)).</summary>
        public const string TlsForDataInTransit = "TLS — §170.210(a)(2)";

        /// <summary>§170.210(c)(2) — Message digest algorithms for integrity (§170.315(d)(8)).</summary>
        public const string MessageDigestForIntegrity = "Message digest — §170.210(c)(2)";

        /// <summary>§170.210(d) — Accounting of disclosures standard (§170.315(d)(11)).</summary>
        public const string AccountingOfDisclosures = "Accounting of disclosures — §170.210(d)";
    }
}
