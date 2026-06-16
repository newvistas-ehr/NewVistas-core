// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Patient Recall — Site Flavor Architecture (Option 4: Composition).
/// Checks the PATIENT_RECALL feature flag before delegating to
/// the optional IPatientRecallGrain. If the feature is not enabled,
/// the recall grains are never activated and consume no resources.
///
/// Maps to IHS RPMS SC Recall routines (File #403.5) — automated recall
/// letters for overdue follow-up appointments.
/// </summary>
public partial class PatientWorkflowGrain
{
    private const string PatientRecallFeature = "PATIENT_RECALL";

    public async Task<PatientRecallState> CreateRecallEntryAsync(
        string clinicId, string clinicName,
        string recallType, DateTime recallDate,
        string? providerId, string? providerName,
        string? diagnosis, string? instructions,
        string createdByProviderId, string createdByProviderName)
    {
        // ── Feature gate ────────────────────────────────────────────
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(PatientRecallFeature);
        if (!enabled)
            throw new InvalidOperationException(
                "Patient recall is not enabled for this site. Enable the PATIENT_RECALL feature in Site Parameters.");

        PatientState patient = await GetPatientGrain().GetPatientAsync();

        string entryId = $"SD-RECALL:{Guid.NewGuid()}";
        IPatientRecallGrain grain =
            GrainFactory.GetGrain<IPatientRecallGrain>(entryId);

        PatientRecallState result = await grain.CreateEntryAsync(
            PatientId, patient.Name,
            clinicId, clinicName,
            recallType, recallDate,
            providerId, providerName,
            diagnosis, instructions,
            createdByProviderId, createdByProviderName);

        await LogAuditEventAsync(
            "SCHEDULING", "CREATE_RECALL", "Recall", entryId,
            createdByProviderId, createdByProviderName, null, null,
            $"Recall created for {clinicName}, type {recallType}, due {recallDate:d}");

        return result;
    }

    public async Task<List<PatientRecallIndexEntry>> GetRecallEntriesAsync()
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(PatientRecallFeature);
        if (!enabled) return [];

        IPatientRecallIndexGrain index =
            GrainFactory.GetGrain<IPatientRecallIndexGrain>("SD-RECALL-IDX");
        return await index.GetByPatientAsync(PatientId);
    }

    public async Task<PatientRecallState> GetRecallEntryAsync(string entryId)
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(PatientRecallFeature);
        if (!enabled)
            throw new InvalidOperationException("Patient recall is not enabled for this site.");

        IPatientRecallGrain grain = GrainFactory.GetGrain<IPatientRecallGrain>(entryId);
        return await grain.GetEntryAsync();
    }

    public async Task GenerateRecallLetterAsync(string entryId, string letterType, string generatedByName)
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(PatientRecallFeature);
        if (!enabled)
            throw new InvalidOperationException("Patient recall is not enabled for this site.");

        IPatientRecallGrain grain = GrainFactory.GetGrain<IPatientRecallGrain>(entryId);
        await grain.GenerateLetterAsync(letterType, generatedByName);
    }

    public async Task RecordRecallContactAttemptAsync(
        string entryId, string contactMethod, string result, string contactedByName, string? notes)
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(PatientRecallFeature);
        if (!enabled)
            throw new InvalidOperationException("Patient recall is not enabled for this site.");

        IPatientRecallGrain grain = GrainFactory.GetGrain<IPatientRecallGrain>(entryId);
        await grain.RecordContactAttemptAsync(contactMethod, result, contactedByName, notes);
    }

    public async Task ScheduleRecallAppointmentAsync(
        string entryId, string appointmentId, DateTime appointmentDateTime, string scheduledByName)
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(PatientRecallFeature);
        if (!enabled)
            throw new InvalidOperationException("Patient recall is not enabled for this site.");

        IPatientRecallGrain grain = GrainFactory.GetGrain<IPatientRecallGrain>(entryId);
        await grain.MarkAppointmentScheduledAsync(appointmentId, appointmentDateTime, scheduledByName);
    }

    public async Task CompleteRecallEntryAsync(string entryId, string completedByName, string? notes)
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(PatientRecallFeature);
        if (!enabled)
            throw new InvalidOperationException("Patient recall is not enabled for this site.");

        IPatientRecallGrain grain = GrainFactory.GetGrain<IPatientRecallGrain>(entryId);
        await grain.MarkCompletedAsync(completedByName, notes);
    }

    public async Task CancelRecallEntryAsync(string entryId, string reason, string cancelledByName)
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(PatientRecallFeature);
        if (!enabled)
            throw new InvalidOperationException("Patient recall is not enabled for this site.");

        IPatientRecallGrain grain = GrainFactory.GetGrain<IPatientRecallGrain>(entryId);
        await grain.CancelEntryAsync(reason, cancelledByName);
    }
}
