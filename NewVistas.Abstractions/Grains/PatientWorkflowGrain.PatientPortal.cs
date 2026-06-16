// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public partial class PatientWorkflowGrain
{
    // ─── Patient Portal Scheduling (§170.315(e)(1)) ──────────────────────────
    // Enhancement: Patient self-scheduling is NOT part of core VistA or RPMS.
    // VistA uses VAOS (an external web app) for patient scheduling; RPMS has no equivalent.
    // All methods below require the PATIENT_SELF_SCHEDULING feature flag to be enabled.

    public async Task<string> PatientSelfScheduleAppointmentAsync(
        string clinicId, DateTime appointmentDateTime, string? purpose, string? appointmentType)
    {
        // Feature gate
        if (!await GetSiteParams().IsFeatureEnabledAsync(PatientSelfSchedulingFeature))
            throw new InvalidOperationException(
                "Patient self-scheduling is not enabled for this site. Contact your care team to schedule appointments.");

        // 1. Eligibility gate
        PatientEligibilityResult eligibility = await CheckPatientEligibilityForSchedulingAsync();
        if (!eligibility.IsEligible)
            throw new InvalidOperationException(
                $"Not eligible for scheduling: {string.Join(" ", eligibility.Reasons)}");

        // 2. Verify clinic accepts patient self-scheduling
        // Check the clinic index entry (where AcceptsPatientSelfSchedule is managed)
        // and fall back to ClinicState for other fields
        IClinicGrain clinicGrain = GrainFactory.GetGrain<IClinicGrain>(clinicId);
        ClinicState clinic = await clinicGrain.GetClinicAsync();

        // Check both sources: index entry (primary for this flag) and clinic state
        IClinicIndexGrain clinicIndex = GrainFactory.GetGrain<IClinicIndexGrain>("SD-CLINIC-INDEX");
        List<ClinicEntry> allClinics = await clinicIndex.GetAllClinicsAsync();
        ClinicEntry? indexEntry = allClinics.FirstOrDefault(c => c.ClinicId == clinicId);
        bool acceptsSelfSchedule = clinic.AcceptsPatientSelfSchedule
            || (indexEntry?.AcceptsPatientSelfSchedule ?? false);

        if (!acceptsSelfSchedule)
            throw new InvalidOperationException("This clinic does not accept patient self-scheduling.");

        // 3. Restrict appointment types patients can book
        if (appointmentType == "URGENT" || appointmentType == "CONSULT")
            throw new InvalidOperationException(
                $"Appointment type '{appointmentType}' requires staff scheduling. " +
                "Please contact your care team.");

        // 4. Delegate to existing scheduling with patient-specific createdBy
        return await ScheduleAppointmentAsync(
            clinicId, clinic.Name, appointmentDateTime,
            clinic.AppointmentLength,
            clinic.PrimaryProviderId, clinic.PrimaryProviderName,
            purpose, appointmentType ?? "REGULAR",
            allowDoubleBook: false);
    }

    public async Task<CancellationPolicyResult> PatientCancelAppointmentAsync(
        string appointmentId, string? reason)
    {
        if (!await GetSiteParams().IsFeatureEnabledAsync(PatientSelfSchedulingFeature))
            throw new InvalidOperationException(
                "Patient self-scheduling is not enabled for this site. Contact your care team to cancel appointments.");

        // Verify appointment belongs to this patient
        AppointmentState appt = await GrainFactory.GetGrain<IAppointmentGrain>(appointmentId).GetAppointmentAsync();
        if (appt.PatientId != PatientId)
            throw new InvalidOperationException("Appointment does not belong to this patient.");

        if (appt.Status != "Scheduled")
            throw new InvalidOperationException($"Cannot cancel appointment with status '{appt.Status}'.");

        // Check cancellation policy (soft enforcement)
        int hoursUntil = (int)(appt.AppointmentDateTime - DateTime.UtcNow).TotalHours;
        int requiredNoticeHours = 24;

        // Try to read from site parameters
        try
        {
            string? configuredHours = await GrainFactory
                .GetGrain<ISiteParametersGrain>("SITE:DEFAULT")
                .GetParameterAsync("PATIENT_CANCEL_NOTICE_HOURS");
            if (configuredHours != null && int.TryParse(configuredHours, out int parsed))
                requiredNoticeHours = parsed;
        }
        catch { /* Use default if site params unavailable */ }

        bool isWithinWindow = hoursUntil < requiredNoticeHours;
        string cancelReason = reason ?? "Patient cancelled";
        if (isWithinWindow)
            cancelReason = $"[LATE CANCEL] {cancelReason}";

        // Always allow cancellation (soft enforcement)
        await CancelAppointmentWithReasonAsync(appointmentId, cancelReason, $"PATIENT:{PatientId}");

        return new CancellationPolicyResult
        {
            IsAllowed = true,
            IsWithinNoticeWindow = isWithinWindow,
            HoursUntilAppointment = hoursUntil,
            RequiredNoticeHours = requiredNoticeHours,
            PolicyMessage = isWithinWindow
                ? $"This cancellation is within the {requiredNoticeHours}-hour notice window. " +
                  "Your care team has been notified of the late cancellation."
                : null,
            WasCancelled = true
        };
    }

    public async Task PatientRescheduleAppointmentAsync(
        string appointmentId, DateTime newDateTime, string? reason)
    {
        if (!await GetSiteParams().IsFeatureEnabledAsync(PatientSelfSchedulingFeature))
            throw new InvalidOperationException(
                "Patient self-scheduling is not enabled for this site. Contact your care team to reschedule appointments.");

        // Verify appointment belongs to this patient
        AppointmentState appt = await GrainFactory.GetGrain<IAppointmentGrain>(appointmentId).GetAppointmentAsync();
        if (appt.PatientId != PatientId)
            throw new InvalidOperationException("Appointment does not belong to this patient.");

        if (appt.Status != "Scheduled")
            throw new InvalidOperationException($"Cannot reschedule appointment with status '{appt.Status}'.");

        await RescheduleAppointmentAsync(appointmentId, newDateTime, reason, $"PATIENT:{PatientId}");
    }

    public async Task<AppointmentWaitListState> PatientJoinWaitListAsync(
        string clinicId, string desiredAppointmentType, string? preferredProviderId,
        DateTime? desiredDateRangeStart, DateTime? desiredDateRangeEnd, string? comments)
    {
        PatientState patient = await GetPatientGrain().GetPatientAsync();
        IClinicGrain clinicGrain = GrainFactory.GetGrain<IClinicGrain>(clinicId);
        ClinicState clinic = await clinicGrain.GetClinicAsync();

        return await AddToWaitListAsync(
            clinicId, clinic.Name, desiredAppointmentType,
            preferredProviderId, null,
            "ROUTINE", // Patients always get ROUTINE priority
            desiredDateRangeStart, desiredDateRangeEnd,
            comments,
            $"PATIENT:{PatientId}", patient.Name);
    }

    public async Task<List<ClinicEntry>> GetPatientBookableClinicsAsync()
    {
        IClinicIndexGrain clinicIndex = GrainFactory.GetGrain<IClinicIndexGrain>("SD-CLINIC-INDEX");
        List<ClinicEntry> all = await clinicIndex.GetAllClinicsAsync();
        return all.Where(c => c.AcceptsPatientSelfSchedule && c.Status == "ACTIVE").ToList();
    }

    public async Task<List<AvailableSlot>> GetPatientBookableSlotsAsync(string clinicId, DateTime date)
    {
        IClinicGrain clinicGrain = GrainFactory.GetGrain<IClinicGrain>(clinicId);
        ClinicState clinic = await clinicGrain.GetClinicAsync();

        // Try provider-aware slots with PATIENT tier filtering
        if (!string.IsNullOrEmpty(clinic.PrimaryProviderId))
        {
            List<AvailableSlot> providerSlots = await GetPatientSchedulableSlotsAsync(
                clinicId, date, clinic.PrimaryProviderId);
            if (providerSlots.Count > 0)
                return providerSlots;
        }

        // Fall back to clinic-wide grid, returning all available slots
        List<AvailableSlot> slots = await GetClinicScheduleIndex(clinicId)
            .GetAvailableSlotsAsync(date, 8, 17, clinic.AppointmentLength);
        return slots.Where(s => s.IsAvailable).ToList();
    }

    public async Task<List<AppointmentState>> GetAppointmentsWithDetailsAsync(int max = 50)
    {
        List<AppointmentEntry> entries = await GetScheduleIndex().GetAppointmentsAsync(max);
        List<AppointmentState> details = new();

        foreach (AppointmentEntry entry in entries)
        {
            try
            {
                AppointmentState state = await GrainFactory
                    .GetGrain<IAppointmentGrain>(entry.AppointmentId).GetAppointmentAsync();
                details.Add(state);
            }
            catch
            {
                // Skip appointments that can't be loaded
            }
        }

        return details;
    }
}
