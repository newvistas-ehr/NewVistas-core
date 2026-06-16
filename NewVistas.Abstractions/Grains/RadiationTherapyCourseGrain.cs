// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class RadiationTherapyCourseGrain : Grain, IRadiationTherapyCourseGrain
{
    private readonly IPersistentState<RtCourseState> _state;

    public RadiationTherapyCourseGrain(
        [PersistentState("rtCourseState", "rtCourseStore")] IPersistentState<RtCourseState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.CourseId))
        {
            _state.State.CourseId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<RtCourseState> GetCourseAsync() => Task.FromResult(_state.State);

    public async Task CreateCourseAsync(
        string patientId,
        string courseName,
        string diagnosisCode,
        string diagnosisText,
        string treatmentSite,
        RtLaterality laterality,
        RtIntent intent,
        RtModality modality,
        int prescribedDoseCgy,
        int fractionsPlanned,
        int dosePerFractionCgy,
        string? beamEnergy,
        string? oncologistId,
        string? oncologistName,
        string? physicistId,
        string? physicistName,
        string? dosimetristId,
        string? dosimetristName,
        string? treatmentMachineId,
        string? treatmentMachineName,
        string? planningNotes)
    {
        _state.State.PatientId = patientId;
        _state.State.CourseName = courseName;
        _state.State.DiagnosisCode = diagnosisCode;
        _state.State.DiagnosisText = diagnosisText;
        _state.State.TreatmentSite = treatmentSite;
        _state.State.Laterality = laterality;
        _state.State.Intent = intent;
        _state.State.Modality = modality;
        _state.State.PrescribedDoseCgy = prescribedDoseCgy;
        _state.State.FractionsPlanned = fractionsPlanned;
        _state.State.DosePerFractionCgy = dosePerFractionCgy;
        _state.State.BeamEnergy = beamEnergy;
        _state.State.OncologistId = oncologistId;
        _state.State.OncologistName = oncologistName;
        _state.State.PhysicistId = physicistId;
        _state.State.PhysicistName = physicistName;
        _state.State.DosimetristId = dosimetristId;
        _state.State.DosimetristName = dosimetristName;
        _state.State.TreatmentMachineId = treatmentMachineId;
        _state.State.TreatmentMachineName = treatmentMachineName;
        _state.State.PlanningNotes = planningNotes;
        _state.State.Status = RtCourseStatus.Planned;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordSimulationAsync(DateTime simulationDate, string? planningNotes)
    {
        _state.State.SimulationDate = simulationDate;
        if (planningNotes != null)
            _state.State.PlanningNotes = planningNotes;
        _state.State.Status = RtCourseStatus.Simulated;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task StartCourseAsync(DateTime treatmentStartDate)
    {
        _state.State.TreatmentStartDate = treatmentStartDate;
        _state.State.Status = RtCourseStatus.Active;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CompleteCourseAsync(DateTime completionDate, string? notes)
    {
        _state.State.TreatmentCompletionDate = completionDate;
        _state.State.Status = RtCourseStatus.Completed;
        if (notes != null)
            _state.State.Notes = notes;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DiscontinueCourseAsync(DateTime discontinuationDate, string reason, string? notes)
    {
        _state.State.DiscontinuationDate = discontinuationDate;
        _state.State.DiscontinuationReason = reason;
        _state.State.Status = RtCourseStatus.Discontinued;
        if (notes != null)
            _state.State.Notes = notes;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task PlaceCourseOnHoldAsync(string? reason)
    {
        _state.State.Status = RtCourseStatus.OnHold;
        if (reason != null)
            _state.State.Notes = reason;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ResumeCourseAsync()
    {
        _state.State.Status = RtCourseStatus.Active;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordFractionDeliveredAsync(int doseDeliveredCgy)
    {
        _state.State.TotalDeliveredDoseCgy += doseDeliveredCgy;
        _state.State.FractionsCompleted += 1;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetBoostAsync(string boostSite, int boostDoseCgy, int boostFractionsPlanned)
    {
        _state.State.BoostFlag = true;
        _state.State.BoostSite = boostSite;
        _state.State.BoostDoseCgy = boostDoseCgy;
        _state.State.BoostFractionsPlanned = boostFractionsPlanned;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetBrachytherapyAsync(BrachytherapyDoseRate doseRate, string? isotope)
    {
        _state.State.BrachyDoseRate = doseRate;
        _state.State.BrachyIsotope = isotope;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
