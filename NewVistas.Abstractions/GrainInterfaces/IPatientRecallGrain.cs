// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Optional feature grain for patient recall / automated recall letters.
/// Enabled per site via ISiteParametersGrain.Features containing "PATIENT_RECALL".
/// Follows the Site Flavor Architecture (Option 4 — Composition).
///
/// Maps to IHS RPMS SC Recall routines (File #403.5) which generate reminder
/// letters and phone calls for patients overdue for follow-up appointments.
/// Keyed by recall entry ID (e.g., "SD-RECALL:{guid}").
/// </summary>
public interface IPatientRecallGrain : IGrainWithStringKey
{
    Task<PatientRecallState> GetEntryAsync();

    Task<PatientRecallState> CreateEntryAsync(
        string patientId,
        string patientName,
        string clinicId,
        string clinicName,
        string recallType,
        DateTime recallDate,
        string? providerId,
        string? providerName,
        string? diagnosis,
        string? instructions,
        string createdByProviderId,
        string createdByProviderName);

    Task UpdateRecallDateAsync(DateTime newRecallDate, string updatedByName);
    Task GenerateLetterAsync(string letterType, string generatedByName);
    Task RecordContactAttemptAsync(string contactMethod, string result, string contactedByName, string? notes);
    Task MarkAppointmentScheduledAsync(string appointmentId, DateTime appointmentDateTime, string scheduledByName);
    Task MarkCompletedAsync(string completedByName, string? notes);
    Task CancelEntryAsync(string reason, string cancelledByName);
    Task MarkOverdueAsync();
}

/// <summary>
/// System-level index grain for patient recall entries.
/// Singleton keyed by "SD-RECALL-IDX".
/// Supports queries by clinic, patient, status, and overdue filtering.
/// </summary>
public interface IPatientRecallIndexGrain : IGrainWithStringKey
{
    Task AddOrUpdateAsync(PatientRecallIndexEntry entry);
    Task RemoveAsync(string entryId);
    Task<List<PatientRecallIndexEntry>> GetByClinicAsync(string clinicId, int maxResults = 50);
    Task<List<PatientRecallIndexEntry>> GetByPatientAsync(string patientId, int maxResults = 50);
    Task<List<PatientRecallIndexEntry>> GetByStatusAsync(string status, int maxResults = 50);
    Task<List<PatientRecallIndexEntry>> GetOverdueAsync(int maxResults = 50);
    Task<List<PatientRecallIndexEntry>> GetDueInRangeAsync(DateTime rangeStart, DateTime rangeEnd, int maxResults = 50);
    Task<List<PatientRecallIndexEntry>> SearchAsync(string? clinicId, string? status, string? recallType, int maxResults = 50);
    Task<int> GetCountAsync();
}
