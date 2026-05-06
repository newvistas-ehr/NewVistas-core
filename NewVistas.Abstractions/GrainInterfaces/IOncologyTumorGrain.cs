// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Represents a single tumor registry entry for a patient.
/// Maps to VistA Oncology files #160–#165 (ONC PRIMARY, ONC STAGING).
/// MUMPS routines: ONCRP.m, ONCS.m
/// Grain key pattern: "ONC-TUMOR:{guid}"
/// </summary>
public interface IOncologyTumorGrain : IGrainWithStringKey
{
    /// <summary>Returns the complete state of this tumor record.</summary>
    Task<OncologyTumorState> GetTumorAsync();

    /// <summary>
    /// Registers a new tumor, establishing the primary site, histology, diagnosis date,
    /// and responsible oncologist. ONCRP.m REGISTER.
    /// </summary>
    Task RegisterTumorAsync(
        string patientId,
        string primarySite,
        string primarySiteText,
        string histology,
        string histologyText,
        TumorLaterality laterality,
        DateTime dateOfDiagnosis,
        DiagnosisBasis diagnosisBasis,
        int sequenceNumber,
        string? oncologistId,
        string? oncologistName);

    /// <summary>
    /// Records TNM staging (clinical and/or pathologic) and SEER summary stage.
    /// ONCS.m STAGE.
    /// </summary>
    Task RecordStagingAsync(
        string? clinicalT,
        string? clinicalN,
        string? clinicalM,
        string? pathologicT,
        string? pathologicN,
        string? pathologicM,
        string? stageGroup,
        string? seerSummaryStage);

    /// <summary>Updates the current disease status (e.g. remission, recurrence, deceased). ONCRP.m STATUS.</summary>
    Task UpdateStatusAsync(OncologyStatus status, DateTime? statusChangeDate, string? notes);

    /// <summary>Records a disease recurrence with date and site. ONCRP.m RECUR.</summary>
    Task RecordRecurrenceAsync(DateTime recurrenceDate, string? recurrenceSite, string? notes);

    /// <summary>Records the date of last patient contact and updates follow-up status.</summary>
    Task RecordLastContactAsync(DateTime dateOfLastContact, OncologyStatus status);

    /// <summary>Links a treatment ID to this tumor record.</summary>
    Task AddTreatmentIdAsync(string treatmentId);

    /// <summary>Appends a free-text comment to the tumor record.</summary>
    Task AddCommentAsync(string comment);
}
