// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
// USCDI — United States Core Data for Interoperability
// Reference: 45 CFR §170.213
// USCDI v1 (July 2020 Errata): adopted; expires January 1, 2026; maps to US Core STU 3.1.1
// USCDI v3: currently required (§170.213(b)); maps to US Core STU 6.1.0
// USCDI v4: future requirement; maps to US Core STU 7.0.0

namespace NewVistas.Abstractions.ONC;

/// <summary>
/// USCDI data classes and their required data elements.
/// Each data class maps to one or more FHIR resources per the US Core Implementation Guide.
/// The adopted version is USCDI v3 per §170.213(b) as updated by HTI-1 (effective March 11, 2024).
/// </summary>
public static class UscdiDataClasses
{
    /// <summary>USCDI version constants.</summary>
    public static class Versions
    {
        /// <summary>USCDI v1 (July 2020 Errata) — adopted under §170.213(a); expires January 1, 2026.</summary>
        public const string V1 = "USCDI v1";

        /// <summary>USCDI v3 — currently required under §170.213(b); maps to US Core STU 6.1.0.</summary>
        public const string V3 = "USCDI v3";

        /// <summary>USCDI v4 — future requirement; maps to US Core STU 7.0.0.</summary>
        public const string V4 = "USCDI v4";
    }

    /// <summary>
    /// Allergies and Intolerances — FHIR: AllergyIntolerance.
    /// Introduced in USCDI v1.
    /// Maps to: NewVistas AllergyGrain (File #120.8 GMRA).
    /// </summary>
    public static class AllergiesAndIntolerances
    {
        public const string SubstanceMedication = "Substance (Medication)";
        public const string SubstanceDrugClass = "Substance (Drug Class)";
        public const string Reaction = "Reaction";
    }

    /// <summary>
    /// Assessment and Plan of Treatment — FHIR: CarePlan.
    /// Renamed to "Patient Summary and Plan" in USCDI v4.
    /// Maps to: NewVistas TiuDocumentGrain (File #8925), ConsultGrain (File #123).
    /// </summary>
    public static class AssessmentAndPlanOfTreatment
    {
        public const string AssessmentAndPlan = "Assessment and Plan";
        /// <summary>Added in USCDI v3.</summary>
        public const string SdohAssessment = "SDOH Assessment";
    }

    /// <summary>
    /// Care Team Members — FHIR: CareTeam.
    /// Introduced in USCDI v1; expanded in v2.
    /// Maps to: NewVistas ConsultGrain (consulting provider), PatientWorkflowGrain.
    /// </summary>
    public static class CareTeamMembers
    {
        public const string Name = "Name";
        public const string Identifier = "Identifier";
        public const string Location = "Location";
        public const string Telecom = "Telecom";
        public const string Role = "Role";
    }

    /// <summary>
    /// Clinical Notes — FHIR: DocumentReference.
    /// Introduced in USCDI v1.
    /// Maps to: NewVistas TiuDocumentGrain (File #8925).
    /// </summary>
    public static class ClinicalNotes
    {
        public const string ConsultationNote = "Consultation Note";
        public const string DischargeSummaryNote = "Discharge Summary Note";
        public const string HistoryAndPhysical = "History & Physical";
        public const string ImagingNarrative = "Imaging Narrative";
        public const string LaboratoryReportNarrative = "Laboratory Report Narrative";
        public const string PathologyReportNarrative = "Pathology Report Narrative";
        public const string ProcedureNote = "Procedure Note";
        public const string ProgressNote = "Progress Note";
    }

    /// <summary>
    /// Clinical Tests — FHIR: Observation (clinical test), DiagnosticReport.
    /// NEW in USCDI v2.
    /// Maps to: NewVistas RadiologyGrain (File #75.1), LabTestGrain (File #63).
    /// </summary>
    public static class ClinicalTests
    {
        public const string ClinicalTest = "Clinical Test";
        public const string ClinicalTestResultOrReport = "Clinical Test Result/Report";
    }

    /// <summary>
    /// Diagnostic Imaging — FHIR: ImagingStudy, DiagnosticReport.
    /// NEW in USCDI v2.
    /// Maps to: NewVistas RadiologyGrain (File #75.1), ImagingGrain (File #2005).
    /// </summary>
    public static class DiagnosticImaging
    {
        public const string DiagnosticImagingTest = "Diagnostic Imaging Test";
        public const string DiagnosticImagingResultOrReport = "Diagnostic Imaging Result/Report";
    }

    /// <summary>
    /// Encounter — FHIR: Encounter.
    /// NEW in USCDI v2.
    /// Maps to: NewVistas AdtGrain (File #405), PceGrain (File #9000010), AppointmentGrain (File #44).
    /// </summary>
    public static class Encounter
    {
        public const string Type = "Type";
        public const string Diagnosis = "Diagnosis";
        public const string Time = "Time";
        public const string Location = "Location";
        public const string Disposition = "Disposition";
        /// <summary>Added in USCDI v4.</summary>
        public const string Identifier = "Identifier";
    }

    /// <summary>
    /// Facility Information — FHIR: Location, Organization.
    /// NEW in USCDI v4.
    /// Maps to: NewVistas WardLocationIndexGrain, ClinicGrain (File #44).
    /// </summary>
    public static class FacilityInformation
    {
        public const string Identifier = "Identifier";
        public const string Type = "Type";
        public const string Name = "Name";
    }

    /// <summary>
    /// Goals and Preferences — FHIR: Goal.
    /// Introduced in USCDI v1; expanded in v4.
    /// Maps to: NewVistas TiuDocumentGrain (care plan goals).
    /// </summary>
    public static class GoalsAndPreferences
    {
        public const string PatientGoals = "Patient Goals";
        public const string HealthConcerns = "Health Concerns";
        /// <summary>Added in USCDI v4.</summary>
        public const string TreatmentInterventionPreference = "Treatment Intervention Preference";
        /// <summary>Added in USCDI v4.</summary>
        public const string CareExperiencePreference = "Care Experience Preference";
        /// <summary>Added in USCDI v3 (SDOH goals).</summary>
        public const string SdohGoals = "SDOH Goals";
    }

    /// <summary>
    /// Health Insurance Information — FHIR: Coverage.
    /// NEW in USCDI v2.
    /// Maps to: NewVistas PatientBenefitPlanGrain (File #59.9).
    /// </summary>
    public static class HealthInsuranceInformation
    {
        public const string CoverageStatus = "Coverage Status";
        public const string CoverageType = "Coverage Type";
        public const string RelationshipToSubscriber = "Relationship to Subscriber";
        public const string MemberIdentifier = "Member Identifier";
        public const string SubscriberIdentifier = "Subscriber Identifier";
        public const string GroupNumber = "Group Number";
        public const string PayerIdentifier = "Payer Identifier";
    }

    /// <summary>
    /// Health Status Assessments — FHIR: Observation.
    /// Expanded class in USCDI v3.
    /// Maps to: NewVistas VitalGrain (File #120.5 GMRV), MentalHealthGrain (File #601.71).
    /// </summary>
    public static class HealthStatusAssessments
    {
        public const string HealthConcerns = "Health Concerns";
        public const string FunctionalStatus = "Functional Status";
        public const string DisabilityStatus = "Disability Status";
        public const string MentalCognitiveStatus = "Mental/Cognitive Status";
        public const string PregnancyStatus = "Pregnancy Status";
        public const string SmokingStatus = "Smoking Status";
        /// <summary>Added in USCDI v4.</summary>
        public const string AlcoholUse = "Alcohol Use";
        /// <summary>Added in USCDI v4.</summary>
        public const string SubstanceUse = "Substance Use";
        /// <summary>Added in USCDI v4.</summary>
        public const string PhysicalActivity = "Physical Activity";
    }

    /// <summary>
    /// Immunizations — FHIR: Immunization.
    /// Introduced in USCDI v1.
    /// Maps to: NewVistas ImmunizationGrain.
    /// </summary>
    public static class Immunizations
    {
        public const string Immunizations_ = "Immunizations";
    }

    /// <summary>
    /// Laboratory — FHIR: Observation (laboratory), DiagnosticReport, Specimen.
    /// Introduced in USCDI v1; expanded in v2, v3, v4.
    /// Maps to: NewVistas LabTestGrain (File #63), LabBatchGrain, LabSummaryGrain, LabEdiGrain.
    /// </summary>
    public static class Laboratory
    {
        public const string Tests = "Tests";
        public const string ValuesOrResults = "Values/Results";
        /// <summary>Added in USCDI v2.</summary>
        public const string SpecimenTracking = "Specimen Tracking";
        /// <summary>Added in USCDI v3.</summary>
        public const string SpecimenType = "Specimen Type";
        /// <summary>Added in USCDI v3.</summary>
        public const string ResultStatus = "Result Status";
        /// <summary>Added in USCDI v4.</summary>
        public const string ResultUnitOfMeasure = "Result Unit of Measure";
        /// <summary>Added in USCDI v4.</summary>
        public const string ResultReferenceRange = "Result Reference Range";
        /// <summary>Added in USCDI v4.</summary>
        public const string ResultInterpretation = "Result Interpretation";
        /// <summary>Added in USCDI v4.</summary>
        public const string SpecimenIdentifier = "Specimen Identifier";
        /// <summary>Added in USCDI v4.</summary>
        public const string SpecimenSourceSite = "Specimen Source Site";
        /// <summary>Added in USCDI v4.</summary>
        public const string SpecimenConditionAcceptability = "Specimen Condition Acceptability";
    }

    /// <summary>
    /// Medications — FHIR: MedicationRequest, MedicationDispense.
    /// Introduced in USCDI v1; expanded in v2, v3, v4.
    /// Maps to: NewVistas PharmacyGrain (File #52), InpatientOrderGrain, OutpatientPharmacyGrain.
    /// </summary>
    public static class Medications
    {
        public const string Medications_ = "Medications";
        public const string MedicationAllergies = "Medication Allergies";
        /// <summary>Added in USCDI v2.</summary>
        public const string Dose = "Dose";
        /// <summary>Added in USCDI v2.</summary>
        public const string DoseUnitOfMeasure = "Dose Unit of Measure";
        /// <summary>Added in USCDI v2.</summary>
        public const string Indication = "Indication";
        /// <summary>Added in USCDI v3.</summary>
        public const string FillStatus = "Fill Status";
        /// <summary>Added in USCDI v4.</summary>
        public const string MedicationInstructions = "Medication Instructions";
        /// <summary>Added in USCDI v4.</summary>
        public const string MedicationAdherence = "Medication Adherence";
    }

    /// <summary>
    /// Patient Demographics — FHIR: Patient.
    /// Introduced in USCDI v1; expanded in v2 and v3.
    /// Maps to: NewVistas PatientGrain (File #2), MpiCorrelationGrain (File #985).
    /// </summary>
    public static class PatientDemographics
    {
        // v1 elements
        public const string FirstName = "First Name";
        public const string LastName = "Last Name";
        public const string PreviousName = "Previous Name";
        public const string MiddleName = "Middle Name";
        public const string Suffix = "Suffix";
        public const string BirthSex = "Birth Sex";
        public const string DateOfBirth = "Date of Birth";
        public const string Race = "Race";
        public const string Ethnicity = "Ethnicity";
        public const string PreferredLanguage = "Preferred Language";
        public const string Address = "Address";
        public const string PhoneNumber = "Phone Number";
        // v2 additions
        public const string GenderIdentity = "Gender Identity";
        public const string SexualOrientation = "Sexual Orientation";
        public const string PreviousAddress = "Previous Address";
        public const string Email = "Email";
        public const string DateOfDeath = "Date of Death";
        // v3 additions
        public const string TribalAffiliation = "Tribal Affiliation";
        public const string RelatedPersonName = "Related Person's Name";
        public const string RelatedPersonRelationship = "Related Person's Relationship";
        public const string Occupation = "Occupation";
        public const string OccupationIndustry = "Occupation Industry";
        /// <summary>Replaces Birth Sex in v3; includes Sex Parameter for Clinical Use (SPCDU).</summary>
        public const string Sex = "Sex";
        public const string SexParameterForClinicalUse = "Sex Parameter for Clinical Use (SPCDU)";
        public const string Pronouns = "Pronouns";
        public const string NameToUse = "Name to Use";
    }

    /// <summary>
    /// Problems — FHIR: Condition.
    /// Introduced in USCDI v1; expanded in v2.
    /// Maps to: NewVistas ProblemGrain (File #9000011 GMPL).
    /// </summary>
    public static class Problems
    {
        public const string Problems_ = "Problems";
        /// <summary>Added in USCDI v2.</summary>
        public const string DateOfResolution = "Date of Resolution";
        /// <summary>Added in USCDI v2.</summary>
        public const string DateOfDiagnosis = "Date of Diagnosis";
    }

    /// <summary>
    /// Procedures — FHIR: Procedure.
    /// Introduced in USCDI v1; expanded in v2 and v3.
    /// Maps to: NewVistas SurgeryGrain (File #130), PceGrain (File #9000010), ConsultGrain (File #123).
    /// </summary>
    public static class Procedures
    {
        public const string Procedures_ = "Procedures";
        /// <summary>Added in USCDI v3.</summary>
        public const string ReasonForReferral = "Reason for Referral";
        /// <summary>Added in USCDI v3.</summary>
        public const string SdohInterventions = "SDOH Interventions";
        /// <summary>Added in USCDI v4.</summary>
        public const string PerformanceTime = "Performance Time";
    }

    /// <summary>
    /// Provenance — FHIR: Provenance.
    /// Introduced in USCDI v1.
    /// Maps to: NewVistas TiuDocumentGrain (author tracking).
    /// </summary>
    public static class Provenance
    {
        public const string AuthorTimeStamp = "Author Time Stamp";
        public const string AuthorOrganization = "Author Organization";
    }

    /// <summary>
    /// SDOH (Social Determinants of Health) — FHIR: Condition, Goal, ServiceRequest, Procedure.
    /// Formalized as a distinct grouping in USCDI v2.
    /// Maps to: NewVistas PceGrain (SDOH assessments), future SdohGrain.
    /// </summary>
    public static class SocialDeterminantsOfHealth
    {
        public const string SdohAssessment = "SDOH Assessment";
        public const string SdohGoals = "SDOH Goals";
        public const string SdohInterventions = "SDOH Interventions";
        public const string SdohProblemsOrHealthConcerns = "SDOH Problems/Health Concerns";
    }

    /// <summary>
    /// Unique Device Identifiers / Medical Devices — FHIR: Device.
    /// Introduced in USCDI v1.
    /// Maps to: NewVistas PatientGrain UDI fields.
    /// </summary>
    public static class UniqueDeviceIdentifiers
    {
        public const string UdiForImplantableDevices = "UDI for Implantable Devices";
    }

    /// <summary>
    /// Vital Signs — FHIR: Observation (vital-signs profile).
    /// Introduced in USCDI v1.
    /// Maps to: NewVistas VitalGrain (File #120.5 GMRV).
    /// </summary>
    public static class VitalSigns
    {
        public const string SystolicBloodPressure = "Systolic Blood Pressure";
        public const string DiastolicBloodPressure = "Diastolic Blood Pressure";
        public const string BodyHeight = "Body Height";
        public const string BodyWeight = "Body Weight";
        public const string HeartRate = "Heart Rate";
        public const string RespiratoryRate = "Respiratory Rate";
        public const string BodyTemperature = "Body Temperature";
        public const string PulseOximetry = "Pulse Oximetry";
        public const string InhaledOxygenConcentration = "Inhaled Oxygen Concentration";
        public const string BmiPercentile2To20Years = "BMI Percentile (2–20 yr)";
        public const string WeightForLengthPercentile0To36Months = "Weight-for-Length Percentile (0–36 mo)";
        public const string HeadCircumferencePercentile0To36Months = "Head Circumference Percentile (0–36 mo)";
        /// <summary>Added in USCDI v4.</summary>
        public const string AverageBloodPressure = "Average Blood Pressure";
    }
}
