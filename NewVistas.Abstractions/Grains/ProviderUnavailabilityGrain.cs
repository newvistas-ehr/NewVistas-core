// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Provider Unavailability Grain — orchestrates the batch processing of appointments
/// when a provider becomes suddenly unavailable.
///
/// Enhancement: VistA handles provider sick call-outs by cancelling appointments one at a time.
/// This grain provides batch cancellation/reassignment which VistA never implemented.
/// Requires the PROVIDER_UNAVAILABILITY_BATCH feature flag to be enabled.
///
/// Key: "PROV-UNAVAIL:{guid}" — one grain per unavailability event.
/// This grain acts as a system-level orchestrator that calls into individual
/// IPatientWorkflowGrain instances for each affected appointment.
/// </summary>
public class ProviderUnavailabilityGrain : Grain, IProviderUnavailabilityGrain
{
    private const string FeatureFlag = "PROVIDER_UNAVAILABILITY_BATCH";
    private readonly IPersistentState<ProviderUnavailabilityState> _state;

    public ProviderUnavailabilityGrain(
        [PersistentState("providerUnavailability", "providerUnavailabilityStore")]
        IPersistentState<ProviderUnavailabilityState> state)
    {
        _state = state;
    }

    private async Task EnsureFeatureEnabledAsync()
    {
        ISiteParametersGrain siteParams = GrainFactory.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");
        bool enabled = await siteParams.IsFeatureEnabledAsync(FeatureFlag);
        if (!enabled)
            throw new InvalidOperationException(
                "Provider batch unavailability is not enabled for this site. " +
                "Enable the PROVIDER_UNAVAILABILITY_BATCH feature in Site Parameters. " +
                "Without this feature, cancel appointments individually (standard VistA workflow).");
    }

    public Task<ProviderUnavailabilityState> GetEventAsync()
        => Task.FromResult(_state.State);

    public async Task<ProviderUnavailabilityState> CreateEventAsync(
        string providerId,
        string providerName,
        DateTime unavailableFrom,
        DateTime unavailableTo,
        string reason,
        string? notes,
        string initiatedByUserId,
        string initiatedByUserName)
    {
        await EnsureFeatureEnabledAsync();
        _state.State.EventId = this.GetPrimaryKeyString();
        _state.State.ProviderId = providerId;
        _state.State.ProviderName = providerName;
        _state.State.UnavailableFrom = unavailableFrom;
        _state.State.UnavailableTo = unavailableTo;
        _state.State.Reason = reason;
        _state.State.Notes = notes;
        _state.State.Status = "Pending";
        _state.State.InitiatedByUserId = initiatedByUserId;
        _state.State.InitiatedByUserName = initiatedByUserName;
        _state.State.CreatedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        // Identify affected appointments
        IProviderScheduleIndexGrain schedIndex = GrainFactory.GetGrain<IProviderScheduleIndexGrain>(
            $"PROV-SCHED:{providerId}");

        int daySpan = Math.Max(1, (int)(unavailableTo - unavailableFrom).TotalDays + 1);
        List<ProviderScheduleEntry> upcoming = await schedIndex.GetUpcomingAsync(daySpan);

        List<ProviderScheduleEntry> affected = upcoming
            .Where(e => e.AppointmentDateTime >= unavailableFrom
                && e.AppointmentDateTime < unavailableTo
                && (e.Status == "Scheduled" || e.Status == "Checked In"))
            .ToList();

        _state.State.AffectedAppointments = affected.Select(a => new AffectedAppointmentRecord
        {
            AppointmentId = a.AppointmentId,
            PatientId = a.PatientId,
            PatientName = a.PatientName,
            ClinicId = a.ClinicId,
            ClinicName = a.ClinicName,
            AppointmentDateTime = a.AppointmentDateTime
        }).ToList();
        _state.State.TotalAffected = _state.State.AffectedAppointments.Count;

        await _state.WriteStateAsync();
        return _state.State;
    }

    public async Task<ProviderUnavailabilityResult> ExecuteBatchCancellationAsync()
    {
        if (_state.State.Status != "Pending")
            throw new InvalidOperationException($"Cannot execute: event status is {_state.State.Status}");

        _state.State.Status = "Processing";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();

        ProviderUnavailabilityResult result = new()
        {
            EventId = _state.State.EventId,
            TotalAffected = _state.State.TotalAffected
        };

        // 1. Set provider status to UNAVAILABLE
        IProviderAvailabilityGrain availGrain = GrainFactory.GetGrain<IProviderAvailabilityGrain>(
            $"PROV-AVAIL:{_state.State.ProviderId}");
        await availGrain.UpdateProviderStatusAsync("UNAVAILABLE", _state.State.Reason, _state.State.InitiatedByUserName);

        // 2. Add time block covering the unavailability period
        await availGrain.AddTimeBlockAsync(new ProviderTimeBlock
        {
            BlockType = _state.State.Reason switch
            {
                "ILLNESS" => "SICK_LEAVE",
                "INJURY" => "SICK_LEAVE",
                _ => "OTHER"
            },
            StartDateTime = _state.State.UnavailableFrom,
            EndDateTime = _state.State.UnavailableTo,
            Reason = $"Provider unavailability: {_state.State.Reason}" +
                (_state.State.Notes != null ? $" — {_state.State.Notes}" : ""),
            CreatedBy = _state.State.InitiatedByUserName
        });

        // 3. Process each affected appointment
        string cancelReason = $"Provider unavailable ({_state.State.Reason})";
        foreach (AffectedAppointmentRecord record in _state.State.AffectedAppointments)
        {
            try
            {
                IPatientWorkflowGrain workflow = GrainFactory.GetGrain<IPatientWorkflowGrain>(record.PatientId);
                await workflow.CancelAppointmentWithReasonAsync(record.AppointmentId, cancelReason, "SYSTEM");

                record.ActionTaken = "CANCELLED";
                _state.State.CancelledCount++;

                // Generate cancellation letter
                try
                {
                    await workflow.GenerateAppointmentLetterAsync(record.AppointmentId, "CANCELLATION");
                    record.NotificationGenerated = true;
                    _state.State.NotificationsSent++;
                }
                catch
                {
                    // Letter generation is best-effort
                }

                result.Processed++;
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Errors.Add($"Failed to cancel {record.AppointmentId}: {ex.Message}");
            }
        }

        _state.State.Status = "Completed";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();

        return result;
    }

    public async Task<ProviderUnavailabilityResult> ExecuteBatchReassignmentAsync(
        string replacementProviderId,
        string replacementProviderName)
    {
        if (_state.State.Status != "Pending")
            throw new InvalidOperationException($"Cannot execute: event status is {_state.State.Status}");

        _state.State.Status = "Processing";
        _state.State.ReplacementProviderId = replacementProviderId;
        _state.State.ReplacementProviderName = replacementProviderName;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();

        ProviderUnavailabilityResult result = new()
        {
            EventId = _state.State.EventId,
            TotalAffected = _state.State.TotalAffected
        };

        // Set original provider status to UNAVAILABLE
        IProviderAvailabilityGrain availGrain = GrainFactory.GetGrain<IProviderAvailabilityGrain>(
            $"PROV-AVAIL:{_state.State.ProviderId}");
        await availGrain.UpdateProviderStatusAsync("UNAVAILABLE", _state.State.Reason, _state.State.InitiatedByUserName);

        // Add time block
        await availGrain.AddTimeBlockAsync(new ProviderTimeBlock
        {
            BlockType = _state.State.Reason switch
            {
                "ILLNESS" => "SICK_LEAVE",
                "INJURY" => "SICK_LEAVE",
                _ => "OTHER"
            },
            StartDateTime = _state.State.UnavailableFrom,
            EndDateTime = _state.State.UnavailableTo,
            Reason = $"Provider unavailability: {_state.State.Reason}",
            CreatedBy = _state.State.InitiatedByUserName
        });

        string reassignReason = $"Provider reassigned due to {_state.State.Reason}";
        foreach (AffectedAppointmentRecord record in _state.State.AffectedAppointments)
        {
            try
            {
                IPatientWorkflowGrain workflow = GrainFactory.GetGrain<IPatientWorkflowGrain>(record.PatientId);
                await workflow.ReassignAppointmentProviderAsync(
                    record.AppointmentId, replacementProviderId, replacementProviderName, reassignReason);

                record.ActionTaken = "REASSIGNED";
                _state.State.ReassignedCount++;

                // Generate provider change letter
                try
                {
                    await workflow.GenerateAppointmentLetterAsync(record.AppointmentId, "PROVIDER_CHANGE");
                    record.NotificationGenerated = true;
                    _state.State.NotificationsSent++;
                }
                catch
                {
                    // Letter generation is best-effort
                }

                result.Processed++;
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Errors.Add($"Failed to reassign {record.AppointmentId}: {ex.Message}");
            }
        }

        _state.State.Status = "Completed";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();

        return result;
    }

    public async Task CompleteEventAsync()
    {
        _state.State.Status = "Completed";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CancelEventAsync(string reason)
    {
        _state.State.Status = "Cancelled";
        _state.State.Notes = (_state.State.Notes ?? "") + $" [Cancelled: {reason}]";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
