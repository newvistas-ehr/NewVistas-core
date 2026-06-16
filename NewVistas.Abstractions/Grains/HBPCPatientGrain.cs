// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class HBPCPatientGrain : Grain, IHBPCPatientGrain
{
    private readonly IPersistentState<HBPCPatientState> _state;

    public HBPCPatientGrain(
        [PersistentState("hbpcPatientState", "hbpcPatientStore")] IPersistentState<HBPCPatientState> state)
    {
        _state = state;
    }

    public async Task EnrollPatientAsync(
        string patientId,
        string patientName,
        DateTime enrollmentDate,
        HBPCLevelOfCare levelOfCare,
        string primaryDiagnosis,
        string primaryCaregiver,
        string homeAddress)
    {
        _state.State.PatientId = patientId;
        _state.State.PatientName = patientName;
        _state.State.EnrollmentDate = enrollmentDate;
        _state.State.LevelOfCare = levelOfCare;
        _state.State.PrimaryDiagnosis = primaryDiagnosis;
        _state.State.PrimaryCaregiver = primaryCaregiver;
        _state.State.HomeAddress = homeAddress;
        _state.State.ProgramStatus = HBPCProgramStatus.Active;
        _state.State.TotalVisitsThisYear = 0;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateLevelOfCareAsync(HBPCLevelOfCare levelOfCare)
    {
        _state.State.LevelOfCare = levelOfCare;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddGoalAsync(string goal)
    {
        if (!_state.State.Goals.Contains(goal))
            _state.State.Goals.Add(goal);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddCareTeamMemberAsync(string memberNameAndRole)
    {
        if (!_state.State.CareTeamMembers.Contains(memberNameAndRole))
            _state.State.CareTeamMembers.Add(memberNameAndRole);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddSecondaryDiagnosisAsync(string diagnosis)
    {
        if (!_state.State.SecondaryDiagnoses.Contains(diagnosis))
            _state.State.SecondaryDiagnoses.Add(diagnosis);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SuspendEnrollmentAsync()
    {
        _state.State.ProgramStatus = HBPCProgramStatus.Suspended;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ReactivateEnrollmentAsync()
    {
        _state.State.ProgramStatus = HBPCProgramStatus.Active;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordVisitAsync(DateTime visitDate, DateTime? nextScheduledVisit)
    {
        _state.State.LastVisitDate = visitDate;
        _state.State.NextScheduledVisit = nextScheduledVisit;
        _state.State.TotalVisitsThisYear++;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DischargePatientAsync(HBPCDischargeReason reason, string dischargeNotes)
    {
        _state.State.ProgramStatus = HBPCProgramStatus.Discharged;
        _state.State.DischargeDate = DateTime.UtcNow;
        _state.State.DischargeReason = reason;
        _state.State.DischargeNotes = dischargeNotes;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task MarkDeceasedAsync(string notes)
    {
        _state.State.ProgramStatus = HBPCProgramStatus.Deceased;
        _state.State.DischargeDate = DateTime.UtcNow;
        _state.State.DischargeReason = HBPCDischargeReason.Deceased;
        _state.State.DischargeNotes = notes;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<HBPCPatientState> GetPatientAsync() => Task.FromResult(_state.State);
}
