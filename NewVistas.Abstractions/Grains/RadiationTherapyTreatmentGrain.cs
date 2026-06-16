// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class RadiationTherapyTreatmentGrain : Grain, IRadiationTherapyTreatmentGrain
{
    private readonly IPersistentState<RtTreatmentState> _state;

    public RadiationTherapyTreatmentGrain(
        [PersistentState("rtTreatmentState", "rtTreatmentStore")] IPersistentState<RtTreatmentState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.TreatmentId))
        {
            _state.State.TreatmentId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<RtTreatmentState> GetTreatmentAsync() => Task.FromResult(_state.State);

    public async Task RecordDeliveryAsync(
        string courseId,
        string patientId,
        int fractionNumber,
        DateTime treatmentDate,
        int doseDeliveredCgy,
        int? treatmentDurationMin,
        string? machineId,
        string? machineName,
        string? technicianId,
        string? technicianName,
        bool setupVerified,
        string? setupMethod,
        decimal? setupDeviationMm,
        bool interrupted,
        string? interruptionReason,
        string? notes)
    {
        _state.State.CourseId = courseId;
        _state.State.PatientId = patientId;
        _state.State.FractionNumber = fractionNumber;
        _state.State.TreatmentDate = treatmentDate;
        _state.State.DoseDeliveredCgy = doseDeliveredCgy;
        _state.State.TreatmentDurationMin = treatmentDurationMin;
        _state.State.MachineId = machineId;
        _state.State.MachineName = machineName;
        _state.State.TechnicianId = technicianId;
        _state.State.TechnicianName = technicianName;
        _state.State.SetupVerified = setupVerified;
        _state.State.SetupMethod = setupMethod;
        _state.State.SetupDeviationMm = setupDeviationMm;
        _state.State.Interrupted = interrupted;
        _state.State.InterruptionReason = interruptionReason;
        _state.State.Notes = notes;
        _state.State.Status = RtFractionStatus.Delivered;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordSkipAsync(
        string courseId,
        string patientId,
        int fractionNumber,
        DateTime scheduledDate,
        RtFractionStatus status,
        string? skipReason)
    {
        _state.State.CourseId = courseId;
        _state.State.PatientId = patientId;
        _state.State.FractionNumber = fractionNumber;
        _state.State.TreatmentDate = scheduledDate;
        _state.State.Status = status;
        _state.State.SkipReason = skipReason;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateStatusAsync(RtFractionStatus status, string? reason)
    {
        _state.State.Status = status;
        if (reason != null)
            _state.State.Notes = reason;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
