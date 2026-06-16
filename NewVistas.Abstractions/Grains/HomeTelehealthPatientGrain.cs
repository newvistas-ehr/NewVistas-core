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
/// Home Telehealth Patient Grain — enrollment, device assignments, and alert thresholds.
/// Grain key: "HT-PATIENT:{patientId}"
/// </summary>
public class HomeTelehealthPatientGrain : Grain, IHomeTelehealthPatientGrain
{
    private readonly IPersistentState<HomeTelehealthPatientState> _state;

    public HomeTelehealthPatientGrain(
        [PersistentState("htPatientState", "htPatientStore")] IPersistentState<HomeTelehealthPatientState> state)
    {
        _state = state;
    }

    public Task<HomeTelehealthPatientState> GetAsync() => Task.FromResult(_state.State);

    public async Task EnrollAsync(
        string patientId,
        string? careCoordinatorId,
        string? careCoordinatorName,
        string? primaryCareProviderId,
        string? primaryCareProviderName,
        HtCareProtocol protocol,
        string? notes)
    {
        _state.State.PatientId = patientId;
        _state.State.IsEnrolled = true;
        _state.State.EnrollmentDate = DateTime.UtcNow;
        _state.State.DisenrollmentDate = null;
        _state.State.DisenrollmentReason = null;
        _state.State.CareCoordinatorId = careCoordinatorId;
        _state.State.CareCoordinatorName = careCoordinatorName;
        _state.State.PrimaryCareProviderId = primaryCareProviderId;
        _state.State.PrimaryCareProviderName = primaryCareProviderName;
        _state.State.Protocol = protocol;
        _state.State.Notes = notes;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DisenrollAsync(string? reason)
    {
        _state.State.IsEnrolled = false;
        _state.State.DisenrollmentDate = DateTime.UtcNow;
        _state.State.DisenrollmentReason = reason;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AssignDeviceAsync(string deviceId, string deviceName, HtDeviceType deviceType)
    {
        // Replace any existing active assignment for the same device
        _state.State.AssignedDevices.RemoveAll(d => d.DeviceId == deviceId && d.ReturnedDate == null);
        _state.State.AssignedDevices.Add(new HtAssignedDevice
        {
            DeviceId = deviceId,
            DeviceName = deviceName,
            DeviceType = deviceType,
            AssignedDate = DateTime.UtcNow
        });
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ReturnDeviceAsync(string deviceId)
    {
        HtAssignedDevice? device = _state.State.AssignedDevices
            .FirstOrDefault(d => d.DeviceId == deviceId && d.ReturnedDate == null);
        if (device != null)
        {
            device.ReturnedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task SetAlertThresholdsAsync(List<HtAlertThreshold> thresholds)
    {
        _state.State.AlertThresholds = thresholds;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
