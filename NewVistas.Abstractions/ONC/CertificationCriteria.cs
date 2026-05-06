// ONC Health IT Certification Program — 45 CFR §170.315
// Reference: ONC certification criteria for Health IT (formerly "2015 Edition Health IT Certification Criteria")
// Updated by HTI-1 Final Rule (89 FR 1429, January 9, 2024; effective March 11, 2024)
// Further amended at 89 FR 101809, December 16, 2024

namespace NewVistas.Abstractions.ONC;

/// <summary>
/// Represents all ONC Health IT Certification criteria under 45 CFR §170.315.
/// Criteria are grouped by category (a–h) matching the regulation.
/// </summary>
public static class CertificationCriteria
{
    /// <summary>
    /// Category (a) — Clinical criteria.
    /// These criteria address clinical workflows in the EHR.
    /// </summary>
    public static class Clinical
    {
        /// <summary>§170.315(a)(1) — Computerized provider order entry: medications.
        /// Record, change, and access medication orders; optional "reason for order" field.
        /// Maps to: NewVistas OrderGrain (File #100), PatientWorkflowGrain.
        /// </summary>
        public const string CPOE_Medications = "§170.315(a)(1)";

        /// <summary>§170.315(a)(2) — Computerized provider order entry: laboratory.
        /// Record, change, and access laboratory orders.
        /// Maps to: NewVistas LabTestGrain (File #63).
        /// </summary>
        public const string CPOE_Laboratory = "§170.315(a)(2)";

        /// <summary>§170.315(a)(3) — Computerized provider order entry: diagnostic imaging.
        /// Record, change, and access diagnostic imaging orders.
        /// Maps to: NewVistas RadiologyGrain (File #75.1).
        /// </summary>
        public const string CPOE_DiagnosticImaging = "§170.315(a)(3)";

        /// <summary>§170.315(a)(4) — Drug-drug, drug-allergy interaction checks for CPOE.
        /// Before completing a medication order, automatically flag drug-drug and
        /// drug-allergy contraindications based on the patient's medication and allergy lists.
        /// Maps to: NewVistas DrugInteractionCheckerGrain, AllergyGrain (File #120.8).
        /// </summary>
        public const string DrugDrugAllergyInteractionChecks = "§170.315(a)(4)";

        /// <summary>§170.315(a)(5) — Patient demographics and observations.
        /// Record/change/access: race, ethnicity, preferred language, sex, sex parameter for
        /// clinical use (SPCDU), sexual orientation, gender identity, name to use, pronouns,
        /// date of birth.
        /// Maps to: NewVistas PatientGrain (File #2), MpiCorrelationGrain (File #985).
        /// </summary>
        public const string PatientDemographicsAndObservations = "§170.315(a)(5)";

        /// <summary>§170.315(a)(9) — Clinical decision support (CDS).
        /// CDS interventions fire during user interaction; support evidence-based and
        /// Predictive DSI selection, configuration, source attributes, and feedback loop.
        /// Note: Substantially expanded by §170.315(b)(11) in HTI-1 (see DecisionSupportInterventions).
        /// Maps to: NewVistas LexiconGrain (File #757), future CDS grain.
        /// </summary>
        public const string ClinicalDecisionSupport = "§170.315(a)(9)";

        /// <summary>§170.315(a)(10) — Drug-formulary and preferred drug list checks.
        /// Enable checking of drug formulary and preferred drug lists.
        /// Maps to: NewVistas FormularyIndexGrain, PharmacyBenefitsGrain (File #59.9).
        /// </summary>
        public const string DrugFormularyChecks = "§170.315(a)(10)";

        /// <summary>§170.315(a)(12) — Family health history.
        /// Record/change/access family health history using SNOMED CT familial concepts (§170.207(a)(1)).
        /// </summary>
        public const string FamilyHealthHistory = "§170.315(a)(12)";

        /// <summary>§170.315(a)(13) — Patient-specific education resources.
        /// Identify education resources based on problem list and medication list.
        /// </summary>
        public const string PatientEducationResources = "§170.315(a)(13)";

        /// <summary>§170.315(a)(14) — Implantable device list.
        /// Record Unique Device Identifiers (UDIs) for the patient's implantable devices.
        /// Maps to: NewVistas PatientGrain — UDI fields (USCDI Unique Device Identifiers).
        /// </summary>
        public const string ImplantableDeviceList = "§170.315(a)(14)";

        /// <summary>§170.315(a)(15) — Social, psychological, and behavioral data (SDOH).
        /// Record/change/access SDOH data elements.
        /// Maps to: NewVistas PceGrain (SDOH assessments), future SdohGrain.
        /// </summary>
        public const string SocialPsychologicalBehavioralData = "§170.315(a)(15)";
    }

    /// <summary>
    /// Category (b) — Care coordination criteria.
    /// These criteria address the exchange and reconciliation of health information.
    /// </summary>
    public static class CareCoordination
    {
        /// <summary>§170.315(b)(1) — Transitions of care.
        /// Send and receive C-CDA transition of care/referral summaries via Direct protocol (§170.202);
        /// incorporate received C-CDA data into discrete fields.
        /// Maps to: NewVistas TiuDocumentGrain (File #8925), ConsultGrain (File #123).
        /// </summary>
        public const string TransitionsOfCare = "§170.315(b)(1)";

        /// <summary>§170.315(b)(2) — Clinical information reconciliation and incorporation.
        /// Reconcile and incorporate medications, allergies, and problems from received C-CDAs.
        /// Maps to: NewVistas AllergyGrain, ProblemGrain, PharmacyGrain.
        /// </summary>
        public const string ClinicalInformationReconciliation = "§170.315(b)(2)";

        /// <summary>§170.315(b)(3) — Electronic prescribing.
        /// Support NCPDP SCRIPT transactions (NewRx, RefillRequest, CancelRx, etc.);
        /// optional controlled substance (EPCS) support.
        /// Maps to: NewVistas PharmacyGrain (File #52), OutpatientPharmacyGrain.
        /// </summary>
        public const string ElectronicPrescribing = "§170.315(b)(3)";

        /// <summary>§170.315(b)(6) — Data export.
        /// Enable export of patient summaries as C-CDAs with configurable date range and location;
        /// supports batch export.
        /// Maps to: NewVistas FhirController (FHIR R4 export), future EhiExportGrain.
        /// </summary>
        public const string DataExport = "§170.315(b)(6)";

        /// <summary>§170.315(b)(7) — Security tags — summary of care — send.
        /// Create a C-CDA tagged as "restricted" per HL7 Data Segmentation for Privacy (DS4P) standard.
        /// </summary>
        public const string SecurityTagsSend = "§170.315(b)(7)";

        /// <summary>§170.315(b)(8) — Security tags — summary of care — receive.
        /// Receive and display a C-CDA tagged as restricted per DS4P.
        /// </summary>
        public const string SecurityTagsReceive = "§170.315(b)(8)";

        /// <summary>§170.315(b)(9) — Care plan.
        /// Record, change, access, create, and receive care plan data using C-CDA Care Plan document.
        /// Maps to: NewVistas TiuDocumentGrain (care plan document type), ConsultGrain.
        /// </summary>
        public const string CarePlan = "§170.315(b)(9)";

        /// <summary>§170.315(b)(10) — Electronic Health Information (EHI) export.
        /// Export all EHI for a single patient (on demand) and for all patients (bulk);
        /// document the export format.
        /// Maps to: NewVistas FhirController ($everything operation), future BulkExportGrain.
        /// </summary>
        public const string EhiExport = "§170.315(b)(10)";

        /// <summary>§170.315(b)(11) — Decision support interventions (NEW in HTI-1, January 9, 2024).
        /// Comprehensive DSI framework covering both evidence-based CDS and Predictive DSI (AI/ML).
        /// Requires source attribute transparency for AI/ML: training data demographics,
        /// model performance metrics by demographic group (race, ethnicity, sex, age, disability),
        /// validation methods, fairness considerations, and risk management documentation.
        /// Maps to: NewVistas future PredictiveDsiGrain, LexiconGrain.
        /// </summary>
        public const string DecisionSupportInterventions = "§170.315(b)(11)";
    }

    /// <summary>
    /// Category (c) — Clinical quality measures criteria.
    /// These criteria address CQM data capture and reporting.
    /// </summary>
    public static class ClinicalQualityMeasures
    {
        /// <summary>§170.315(c)(1) — CQMs: record and export.
        /// For each CQM, record all required data and export in QRDA I format.
        /// </summary>
        public const string RecordAndExport = "§170.315(c)(1)";

        /// <summary>§170.315(c)(2) — CQMs: import and calculate.
        /// Import QRDA I files and calculate CQMs.
        /// </summary>
        public const string ImportAndCalculate = "§170.315(c)(2)";

        /// <summary>§170.315(c)(3) — CQMs: report.
        /// Generate QRDA III aggregate reports for submission.
        /// </summary>
        public const string Report = "§170.315(c)(3)";

        /// <summary>§170.315(c)(4) — CQMs: filter.
        /// Filter CQM data by required demographic and practice variables.
        /// </summary>
        public const string Filter = "§170.315(c)(4)";
    }

    /// <summary>
    /// Category (d) — Privacy and security criteria.
    /// These criteria address access control, audit, and data protection requirements.
    /// </summary>
    public static class PrivacyAndSecurity
    {
        /// <summary>§170.315(d)(1) — Authentication, access control, and authorization.
        /// Verify user identity via unique identifier; assign role-based access permissions.
        /// </summary>
        public const string AuthenticationAccessControlAuthorization = "§170.315(d)(1)";

        /// <summary>§170.315(d)(2) — Auditable events and tamper-resistance.
        /// Create audit logs of system activity; protect logs from tampering.
        /// </summary>
        public const string AuditableEventsAndTamperResistance = "§170.315(d)(2)";

        /// <summary>§170.315(d)(3) — Audit reports.
        /// Generate audit reports from audit logs.
        /// </summary>
        public const string AuditReports = "§170.315(d)(3)";

        /// <summary>§170.315(d)(4) — Amendments.
        /// Allow users to record a patient-requested amendment to health records.
        /// Maps to: NewVistas TiuDocumentGrain.AmendNoteAsync.
        /// </summary>
        public const string Amendments = "§170.315(d)(4)";

        /// <summary>§170.315(d)(5) — Automatic access time-out.
        /// Auto-terminate sessions after configurable inactivity period.
        /// </summary>
        public const string AutomaticAccessTimeOut = "§170.315(d)(5)";

        /// <summary>§170.315(d)(6) — Emergency access.
        /// Permit identified users to access EHI during emergencies.
        /// </summary>
        public const string EmergencyAccess = "§170.315(d)(6)";

        /// <summary>§170.315(d)(7) — End-user device encryption.
        /// Encrypt EHI stored on end-user devices using a permitted encryption standard.
        /// </summary>
        public const string EndUserDeviceEncryption = "§170.315(d)(7)";

        /// <summary>§170.315(d)(8) — Integrity.
        /// Create and verify message digests per §170.210(c)(2).
        /// </summary>
        public const string Integrity = "§170.315(d)(8)";

        /// <summary>§170.315(d)(9) — Trusted connection.
        /// Encrypt data in transit per §170.210(a)(2) (TLS).
        /// </summary>
        public const string TrustedConnection = "§170.315(d)(9)";

        /// <summary>§170.315(d)(10) — Auditing actions on health information.
        /// Audit specific actions (create, read, update, delete) on EHI.
        /// </summary>
        public const string AuditingActionsOnHealthInformation = "§170.315(d)(10)";

        /// <summary>§170.315(d)(11) — Accounting of disclosures.
        /// Record disclosures for treatment, payment, and operations per §170.210(d).
        /// </summary>
        public const string AccountingOfDisclosures = "§170.315(d)(11)";

        /// <summary>§170.315(d)(12) — Encrypt authentication credentials.
        /// Encrypt stored authentication credentials.
        /// </summary>
        public const string EncryptAuthenticationCredentials = "§170.315(d)(12)";

        /// <summary>§170.315(d)(13) — Multi-factor authentication.
        /// Support MFA for user access.
        /// </summary>
        public const string MultiFactorAuthentication = "§170.315(d)(13)";
    }

    /// <summary>
    /// Category (e) — Patient engagement criteria.
    /// These criteria address patient access to their own health data.
    /// </summary>
    public static class PatientEngagement
    {
        /// <summary>§170.315(e)(1) — View, download, and transmit to third party.
        /// Patients can view, download, and transmit their data via internet-based technology;
        /// conforming to §170.204(a)(1) (SMART on FHIR); supports C-CDA and USCDI data.
        /// Maps to: NewVistas FhirController, PatientAccessController (future).
        /// </summary>
        public const string ViewDownloadAndTransmit = "§170.315(e)(1)";

        /// <summary>§170.315(e)(2) — Secure messaging.
        /// Enable bidirectional secure messaging between patients and providers.
        /// </summary>
        public const string SecureMessaging = "§170.315(e)(2)";

        /// <summary>§170.315(e)(3) — Patient health information capture.
        /// Allow patients to electronically capture and submit health data into the EHR.
        /// </summary>
        public const string PatientHealthInformationCapture = "§170.315(e)(3)";
    }

    /// <summary>
    /// Category (f) — Public health reporting criteria.
    /// These criteria address electronic reporting to public health agencies.
    /// </summary>
    public static class PublicHealth
    {
        /// <summary>§170.315(f)(1) — Transmission to immunization registries.
        /// Create and transmit HL7 v2.5.1 immunization messages (§170.205(e)(3)) to IIS.
        /// Maps to: NewVistas ImmunizationGrain.
        /// </summary>
        public const string ImmunizationRegistry = "§170.315(f)(1)";

        /// <summary>§170.315(f)(2) — Transmission to public health agencies: syndromic surveillance.
        /// Create and transmit HL7 v2.5.1 ADT/observation messages for syndromic surveillance.
        /// Maps to: NewVistas AdtGrain (File #405).
        /// </summary>
        public const string SyndromicSurveillance = "§170.315(f)(2)";

        /// <summary>§170.315(f)(3) — Transmission to public health agencies: reportable laboratory tests.
        /// Create and transmit HL7 v2.5.1 electronic laboratory reporting (ELR) messages.
        /// Maps to: NewVistas LabTestGrain (File #63), LabEdiGrain.
        /// </summary>
        public const string ReportableLaboratoryTests = "§170.315(f)(3)";

        /// <summary>§170.315(f)(4) — Transmission to cancer registries.
        /// Create and transmit HL7 v2.5.1 or NAACCR messages to cancer registries.
        /// </summary>
        public const string CancerRegistries = "§170.315(f)(4)";

        /// <summary>§170.315(f)(5) — Transmission to public health agencies: electronic case reporting (eCR).
        /// Create case reports for eCR; transitions to FHIR-based eCR by January 1, 2026.
        /// </summary>
        public const string ElectronicCaseReporting = "§170.315(f)(5)";

        /// <summary>§170.315(f)(6) — Transmission to public health agencies: antimicrobial use and resistance.
        /// Create and transmit AU&R data per §170.205(r)(1).
        /// </summary>
        public const string AntimicrobialUseAndResistance = "§170.315(f)(6)";

        /// <summary>§170.315(f)(7) — Transmission to public health agencies: health care surveys.
        /// Create and transmit health care survey data per §170.205(s)(1).
        /// </summary>
        public const string HealthCareSurveys = "§170.315(f)(7)";
    }

    /// <summary>
    /// Category (g) — Design and performance (infrastructure) criteria.
    /// These criteria address API access, promoting interoperability measures,
    /// and design/quality processes.
    /// </summary>
    public static class Infrastructure
    {
        /// <summary>§170.315(g)(1) — Automated numerator recording.
        /// Create reports enabling review of patients/actions counting toward Promoting
        /// Interoperability (PI) measure numerators.
        /// </summary>
        public const string AutomatedNumeratorRecording = "§170.315(g)(1)";

        /// <summary>§170.315(g)(2) — Automated measure calculation.
        /// Calculate PI program percentage-based measures automatically.
        /// </summary>
        public const string AutomatedMeasureCalculation = "§170.315(g)(2)";

        /// <summary>§170.315(g)(3) — Safety-enhanced design.
        /// Identify use of a user-centered design (UCD) process per NISTIR 7741 or similar.
        /// </summary>
        public const string SafetyEnhancedDesign = "§170.315(g)(3)";

        /// <summary>§170.315(g)(4) — Quality management system.
        /// Identify use of a QMS (e.g., ISO 9001, CMMI) in development, testing, and maintenance.
        /// </summary>
        public const string QualityManagementSystem = "§170.315(g)(4)";

        /// <summary>§170.315(g)(5) — Accessibility-centered design.
        /// Identify use of an accessibility standard (e.g., Section 508, WCAG).
        /// </summary>
        public const string AccessibilityCenteredDesign = "§170.315(g)(5)";

        /// <summary>§170.315(g)(6) — Consolidated CDA creation performance.
        /// Demonstrate C-CDA creation within defined performance thresholds.
        /// </summary>
        public const string CcdaCreationPerformance = "§170.315(g)(6)";

        /// <summary>§170.315(g)(7) — Application access: patient selection.
        /// API that accepts patient identifiers and returns matching patient records.
        /// Predecessor to §170.315(g)(10).
        /// Maps to: NewVistas MpiSearchGrain (File #985).
        /// </summary>
        public const string AppAccessPatientSelection = "§170.315(g)(7)";

        /// <summary>§170.315(g)(8) — Application access: data category request.
        /// API that returns all data for a specific data category for a patient using C-CDA.
        /// Predecessor to §170.315(g)(10).
        /// </summary>
        public const string AppAccessDataCategoryRequest = "§170.315(g)(8)";

        /// <summary>§170.315(g)(9) — Application access: all data request.
        /// API that returns all patient data as a C-CDA document.
        /// Predecessor to §170.315(g)(10).
        /// </summary>
        public const string AppAccessAllDataRequest = "§170.315(g)(9)";

        /// <summary>§170.315(g)(10) — Standardized API for patient and population services.
        /// The primary modern API criterion. Requires:
        /// - HL7 FHIR R4 (4.0.1) base standard
        /// - HL7 FHIR US Core STU 6.1.0 profiles (mandatory from January 1, 2026)
        /// - SMART App Launch IG 2.0.0 (mandatory from January 1, 2026)
        /// - FHIR Bulk Data Access (Flat FHIR) v1.0.0 STU 1 for population-level queries
        /// - OpenID Connect Core 1.0 for authentication
        /// - OAuth 2.0 refresh tokens with minimum 3-month validity
        /// - Patient authorization revocation within 1 hour
        /// - Token introspection support
        /// - Public API documentation without preconditions
        /// Maps to: NewVistas FhirController, FhirGatewayWorkflow.
        /// </summary>
        public const string StandardizedApiPatientAndPopulation = "§170.315(g)(10)";
    }

    /// <summary>
    /// Category (h) — Transport methods and other protocols.
    /// These criteria address secure transport for health information exchange.
    /// </summary>
    public static class TransportMethods
    {
        /// <summary>§170.315(h)(1) — Direct Project.
        /// Send and receive health information using the Applicability Statement for Secure
        /// Health Transport (§170.202(a)(2)); support Delivery Notification in Direct (§170.202(e)(1)).
        /// </summary>
        public const string DirectProject = "§170.315(h)(1)";

        /// <summary>§170.315(h)(2) — Direct Project, Edge Protocol, and XDR/XDM.
        /// Send and receive using Direct + XDR/XDM edge protocols (§170.202(b) and (d))
        /// in addition to standard Direct transport.
        /// </summary>
        public const string DirectProjectEdgeProtocol = "§170.315(h)(2)";
    }
}
