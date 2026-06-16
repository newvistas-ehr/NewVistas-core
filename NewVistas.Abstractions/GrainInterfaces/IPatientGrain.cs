// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Patient Grain Interface based on VistA PATIENT file (#2)
/// Represents a single patient in the system
/// </summary>
public interface IPatientGrain : IGrainWithStringKey
{
    /// <summary>
    /// Gets the complete patient state
    /// </summary>
    Task<GrainStates.PatientState> GetPatientAsync();

    /// <summary>
    /// Updates the patient demographic information
    /// </summary>
    Task<PatientState> UpdateDemographicsAsync(
        string name,
        string sex,
        DateTime? dateOfBirth,
        string? socialSecurityNumber);

    /// <summary>
    /// Updates the patient address information
    /// </summary>
    Task UpdateAddressAsync(
        string? streetAddress1,
        string? streetAddress2,
        string? streetAddress3,
        string? city,
        string? state,
        string? zipCode);

    /// <summary>
    /// Updates the patient contact information
    /// </summary>
    Task UpdateContactInfoAsync(
        string? phoneResidence,
        string? phoneWork,
        string? email);

    /// <summary>
    /// Updates emergency contact information
    /// </summary>
    Task UpdateEmergencyContactAsync(
        string? name,
        string? relationship,
        string? phone);

    /// <summary>
    /// Updates veteran information
    /// </summary>
    Task UpdateVeteranInfoAsync(
        string veteran,
        int? serviceConnectedPercentage,
        string? eligibilityCode,
        string? primaryEligibilityCode);

    /// <summary>
    /// Updates military service information
    /// </summary>
    Task UpdateMilitaryServiceAsync(
        DateTime? serviceEntryDate,
        DateTime? serviceSeparationDate,
        string? serviceBranch,
        string? dischargeType,
        string? prisonerOfWar);

    /// <summary>
    /// Adds an appointment for the patient
    /// </summary>
    Task AddAppointmentAsync(DateTime appointmentDateTime);

    /// <summary>
    /// Removes an appointment for the patient
    /// </summary>
    Task RemoveAppointmentAsync(DateTime appointmentDateTime);

    /// <summary>
    /// Gets all appointments for the patient
    /// </summary>
    Task<List<DateTime>> GetAppointmentsAsync();

    /// <summary>
    /// Updates marital status
    /// </summary>
    Task UpdateMaritalStatusAsync(string? maritalStatus);

    /// <summary>
    /// Updates religious preference
    /// </summary>
    Task UpdateReligiousPreferenceAsync(string? religiousPreference);

    /// <summary>
    /// Adds a race to the patient record
    /// </summary>
    Task AddRaceAsync(string race);

    /// <summary>
    /// Adds an ethnicity to the patient record
    /// </summary>
    Task AddEthnicityAsync(string ethnicity);

    /// <summary>
    /// Updates current admission information
    /// </summary>
    Task UpdateCurrentAdmissionAsync(string? admissionId, string? roomBed, string? currentMovement);

    /// <summary>
    /// Records date of death
    /// </summary>
    Task RecordDateOfDeathAsync(DateTime dateOfDeath);

    /// <summary>
    /// Updates birth place information
    /// </summary>
    Task UpdateBirthPlaceAsync(string? city, string? state);

    /// <summary>
    /// Deactivates the patient record
    /// </summary>
    Task DeactivateAsync();

    /// <summary>
    /// Activates the patient record
    /// </summary>
    Task ActivateAsync();

    /// <summary>
    /// Gets the patient's full name
    /// </summary>
    Task<string> GetNameAsync();

    /// <summary>
    /// Checks if patient is a veteran
    /// </summary>
    Task<bool> IsVeteranAsync();

    /// <summary>
    /// Gets the patient's age
    /// </summary>
    Task<int?> GetAgeAsync();

    /// <summary>
    /// Adds an appointment ID to the patient's appointment collection
    /// </summary>
    Task AddAppointmentIdAsync(string appointmentId);

    /// <summary>
    /// Removes an appointment ID from the patient's appointment collection
    /// </summary>
    Task RemoveAppointmentIdAsync(string appointmentId);

    /// <summary>
    /// Gets all appointment IDs for the patient
    /// </summary>
    Task<List<string>> GetAppointmentIdsAsync();

    /// <summary>
    /// Adds a lab test ID to the patient's lab test collection
    /// </summary>
    Task AddLabTestIdAsync(string labTestId);

    /// <summary>
    /// Removes a lab test ID from the patient's lab test collection
    /// </summary>
    Task RemoveLabTestIdAsync(string labTestId);

    /// <summary>
    /// Gets all lab test IDs for the patient
    /// </summary>
    Task<List<string>> GetLabTestIdsAsync();

    /// <summary>
    /// Adds an order ID to the patient's order collection
    /// </summary>
    Task AddOrderIdAsync(string orderId);

    /// <summary>
    /// Removes an order ID from the patient's order collection
    /// </summary>
    Task RemoveOrderIdAsync(string orderId);

    /// <summary>
    /// Gets all order IDs for the patient
    /// </summary>
    Task<List<string>> GetOrderIdsAsync();

    /// <summary>
    /// Adds an allergy entry to the patient's embedded allergy list.
    /// </summary>
    Task AddAllergyAsync(GrainStates.AllergyEntry entry);

    /// <summary>
    /// Removes an allergy by its AllergyId from the patient's embedded list.
    /// </summary>
    Task RemoveAllergyAsync(string allergyId);

    /// <summary>
    /// Gets all embedded allergy entries for the patient.
    /// </summary>
    Task<List<GrainStates.AllergyEntry>> GetAllergiesAsync();

    /// <summary>
    /// Gets a single allergy entry by AllergyId.
    /// </summary>
    Task<GrainStates.AllergyEntry?> GetAllergyAsync(string allergyId);

    /// <summary>
    /// Updates an existing allergy entry in place.
    /// </summary>
    Task UpdateAllergyAsync(GrainStates.AllergyEntry updated);

    // --- Embedded Problem List ---
    Task AddProblemAsync(GrainStates.ProblemEntry entry);
    Task RemoveProblemAsync(string problemId);
    Task<List<GrainStates.ProblemEntry>> GetProblemsAsync();
    Task<GrainStates.ProblemEntry?> GetProblemAsync(string problemId);
    Task UpdateProblemAsync(GrainStates.ProblemEntry updated);

    /// <summary>
    /// Inactivate a problem on the patient's problem list. Records a causal
    /// <c>ProblemInactivatedV1</c> event into the clinical event stream.
    /// </summary>
    Task InactivateProblemAsync(string problemId, DateTime dateResolved);

    // --- Pharmacy ---
    Task AddPharmacyIdAsync(string pharmacyId);
    Task RemovePharmacyIdAsync(string pharmacyId);
    Task<List<string>> GetPharmacyIdsAsync();

    // --- BCMA ---
    Task AddBcmaIdAsync(string bcmaId);
    Task RemoveBcmaIdAsync(string bcmaId);
    Task<List<string>> GetBcmaIdsAsync();

    // --- Radiology ---
    Task AddRadiologyIdAsync(string radiologyId);
    Task RemoveRadiologyIdAsync(string radiologyId);
    Task<List<string>> GetRadiologyIdsAsync();

    // --- Orders (Recent Cache) ---

    /// <summary>
    /// Adds an order summary to the recent orders cache and trims to maxCount.
    /// Called by the workflow grain after placing an order.
    /// </summary>
    Task AddRecentOrderAsync(GrainStates.OrderSummary summary, int maxCount);

    /// <summary>
    /// Gets the recent orders cache (last N orders for cover sheet display).
    /// </summary>
    Task<List<GrainStates.OrderSummary>> GetRecentOrdersAsync();

    /// <summary>
    /// Replaces the entire recent orders cache. Used when rebuilding from index.
    /// </summary>
    Task SetRecentOrdersAsync(List<GrainStates.OrderSummary> orders);

    // --- Vitals (Recent Cache) ---

    /// <summary>
    /// Adds a vital summary to the recent vitals cache and trims to maxCount.
    /// Called by the workflow grain after recording a vital.
    /// </summary>
    Task AddRecentVitalAsync(GrainStates.VitalSummary summary, int maxCount);

    /// <summary>
    /// Gets the recent vitals cache (last N vitals for cover sheet display).
    /// </summary>
    Task<List<GrainStates.VitalSummary>> GetRecentVitalsAsync();

    /// <summary>
    /// Replaces the entire recent vitals cache. Used when rebuilding from index.
    /// </summary>
    Task SetRecentVitalsAsync(List<GrainStates.VitalSummary> vitals);

    // --- Vitals Legacy ---
    /// <summary>Legacy — kept for backward compatibility with importers.</summary>
    Task AddVitalIdAsync(string vitalId);
    Task<List<string>> GetVitalIdsAsync();

    // --- TIU Documents / Progress Notes ---
    Task AddTiuDocumentIdAsync(string documentId);
    Task RemoveTiuDocumentIdAsync(string documentId);
    Task<List<string>> GetTiuDocumentIdsAsync();

    // --- Notes (Recent Cache) ---

    /// <summary>
    /// Adds a note summary to the recent notes cache and trims to maxCount.
    /// Called by the workflow grain after creating/signing a note.
    /// </summary>
    Task AddRecentNoteAsync(GrainStates.TiuNoteSummary summary, int maxCount);

    /// <summary>
    /// Gets the recent notes cache (last N notes for cover sheet display).
    /// </summary>
    Task<List<GrainStates.TiuNoteSummary>> GetRecentNotesAsync();

    /// <summary>
    /// Replaces the entire recent notes cache. Used when rebuilding from index.
    /// </summary>
    Task SetRecentNotesAsync(List<GrainStates.TiuNoteSummary> notes);

    // --- Consults ---
    Task AddConsultIdAsync(string consultId);
    Task RemoveConsultIdAsync(string consultId);
    Task<List<string>> GetConsultIdsAsync();

    // --- Surgery ---
    Task AddSurgeryIdAsync(string surgeryId);
    Task RemoveSurgeryIdAsync(string surgeryId);
    Task<List<string>> GetSurgeryIdsAsync();

    // --- Clinical Reminders ---
    Task AddClinicalReminderIdAsync(string reminderId);
    Task RemoveClinicalReminderIdAsync(string reminderId);
    Task<List<string>> GetClinicalReminderIdsAsync();

    // --- Embedded Immunizations ---
    Task AddImmunizationAsync(GrainStates.ImmunizationEntry entry);
    Task RemoveImmunizationAsync(string immunizationId);
    Task<List<GrainStates.ImmunizationEntry>> GetImmunizationsAsync();
    Task<GrainStates.ImmunizationEntry?> GetImmunizationAsync(string immunizationId);
    Task UpdateImmunizationAsync(GrainStates.ImmunizationEntry updated);

    // --- Health Factors ---
    Task AddHealthFactorIdAsync(string healthFactorId);
    Task RemoveHealthFactorIdAsync(string healthFactorId);
    Task<List<string>> GetHealthFactorIdsAsync();

    // --- Mental Health ---
    Task AddMentalHealthIdAsync(string instrumentId);
    Task RemoveMentalHealthIdAsync(string instrumentId);
    Task<List<string>> GetMentalHealthIdsAsync();

    // --- Embedded Diet Orders ---
    Task AddDietOrderAsync(GrainStates.DieteticsEntry entry);
    Task RemoveDietOrderAsync(string dieteticsId);
    Task<List<GrainStates.DieteticsEntry>> GetDietOrdersAsync();
    Task<GrainStates.DieteticsEntry?> GetDietOrderAsync(string dieteticsId);
    Task UpdateDietOrderAsync(GrainStates.DieteticsEntry updated);

    // --- Embedded Prosthetics ---
    Task AddProstheticsItemAsync(GrainStates.ProstheticsEntry entry);
    Task RemoveProstheticsItemAsync(string prostheticsId);
    Task<List<GrainStates.ProstheticsEntry>> GetProstheticsItemsAsync();
    Task<GrainStates.ProstheticsEntry?> GetProstheticsItemAsync(string prostheticsId);
    Task UpdateProstheticsItemAsync(GrainStates.ProstheticsEntry updated);

    // --- Imaging ---
    Task AddImagingIdAsync(string imagingId);
    Task RemoveImagingIdAsync(string imagingId);
    Task<List<string>> GetImagingIdsAsync();

    // --- ADT Episodes ---
    Task AddAdtIdAsync(string adtId);
    Task RemoveAdtIdAsync(string adtId);
    Task<List<string>> GetAdtIdsAsync();

    // --- Embedded Means Tests ---
    Task AddMeansTestAsync(GrainStates.MeansTestEntry entry);
    Task RemoveMeansTestAsync(string meansTestId);
    Task<List<GrainStates.MeansTestEntry>> GetMeansTestsAsync();
    Task<GrainStates.MeansTestEntry?> GetMeansTestAsync(string meansTestId);
    Task UpdateMeansTestAsync(GrainStates.MeansTestEntry updated);

    // --- Embedded Service Connected Conditions ---
    Task AddScConditionAsync(GrainStates.ScConditionEntry entry);
    Task RemoveScConditionAsync(string conditionId);
    Task<List<GrainStates.ScConditionEntry>> GetScConditionsAsync();
    Task<GrainStates.ScConditionEntry?> GetScConditionAsync(string conditionId);
    Task UpdateScConditionAsync(GrainStates.ScConditionEntry updated);

    // --- VistA Identity ---

    /// <summary>
    /// Sets the VistA Data File Number (DFN) for this patient.
    /// Called at registration when the local facility assigns a PATIENT file (#2) IEN.
    /// </summary>
    Task<PatientState> SetDfnAsync(string dfn);

    /// <summary>
    /// Sets the Integration Control Number (ICN) assigned by the VA Master Patient Index.
    /// Called when MPI correlation completes (MPIF001.m GETICN).
    /// Format: {10-digit}V{6-digit} (e.g., "1234567890V123456").
    /// </summary>
    Task<PatientState> SetIcnAsync(string icn);

    // --- Sensitivity Flags ---

    /// <summary>
    /// Updates the quick-lookup sensitivity flags on the patient record.
    /// Called by the workflow grain when sensitivity is set on the PAC grain.
    /// </summary>
    Task UpdateSensitivityFlagsAsync(bool isSensitive, string? sensitivityLevel);

    /// <summary>
    /// Marks this patient as merged into another patient.
    /// Deactivates the record and stores the merge metadata.
    /// Maps to VistA DG MERGE file (#15.1).
    /// </summary>
    Task MarkAsMergedAsync(string survivingPatientId, string mergedByUserId);

    // --- Capped Domain ID Lists ---
    // PatientState keeps a recent window of item IDs per clinical domain
    // (site parameter RecentItemsDisplayCount); full history lives in
    // IPatientHistoryIndexGrain. Allergies are never capped.

    /// <summary>
    /// Returns a copy of the ID list for a PatientHistoryDomains constant.
    /// Pre-migration this is the complete history; post-migration it is the
    /// recent window only (full history is in the history index).
    /// </summary>
    Task<List<string>> GetDomainIdsAsync(string domain);

    /// <summary>
    /// True once the domain's full ID history has been flushed to the
    /// per-domain history index (i.e., trimming is permitted).
    /// </summary>
    Task<bool> IsDomainMigratedAsync(string domain);

    /// <summary>
    /// Appends an item ID to the domain list (duplicate-safe) and, if the
    /// domain is migrated, trims the list to the most recent maxCount.
    /// Never trims pre-migration — that would lose un-flushed history.
    /// </summary>
    Task AddDomainIdCappedAsync(string domain, string id, int maxCount);

    /// <summary>
    /// Marks the domain migrated and trims its list to maxCount in one state
    /// write. Call ONLY after the full list has been flushed to the history
    /// index (the workflow grain's AppendCappedIdAsync owns this ordering).
    /// </summary>
    Task MarkDomainMigratedAndTrimAsync(string domain, int maxCount);
}
