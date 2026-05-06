// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class CLCAdmissionGrain : Grain, ICLCAdmissionGrain
{
    private readonly IPersistentState<CLCAdmissionState> _state;

    public CLCAdmissionGrain(
        [PersistentState("clcAdmissionState", "clcAdmissionStore")] IPersistentState<CLCAdmissionState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.AdmissionId))
            _state.State.AdmissionId = this.GetPrimaryKeyString();
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task AdmitPatientAsync(
        string patientId,
        string patientName,
        DateTime? patientDOB,
        DateTime admitDate,
        CLCAdmitSource admitSource,
        GECLevelOfCare levelOfCare,
        string ward,
        string bedRoom,
        string attendingPhysician,
        string primaryDiagnosis,
        string referringFacility,
        DateTime? anticipatedDischargeDate,
        string notes)
    {
        _state.State.PatientId = patientId;
        _state.State.PatientName = patientName;
        _state.State.PatientDOB = patientDOB;
        _state.State.AdmitDate = admitDate;
        _state.State.AdmitSource = admitSource;
        _state.State.LevelOfCare = levelOfCare;
        _state.State.Ward = ward;
        _state.State.BedRoom = bedRoom;
        _state.State.AttendingPhysician = attendingPhysician;
        _state.State.PrimaryDiagnosis = primaryDiagnosis;
        _state.State.ReferringFacility = referringFacility;
        _state.State.AnticipatedDischargeDate = anticipatedDischargeDate;
        _state.State.Notes = notes;
        _state.State.Status = CLCAdmissionStatus.Active;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateLevelOfCareAsync(GECLevelOfCare levelOfCare)
    {
        _state.State.LevelOfCare = levelOfCare;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateBedAssignmentAsync(string ward, string bedRoom)
    {
        _state.State.Ward = ward;
        _state.State.BedRoom = bedRoom;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task MarkOnLeaveAsync()
    {
        _state.State.Status = CLCAdmissionStatus.OnLeave;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ReturnFromLeaveAsync()
    {
        _state.State.Status = CLCAdmissionStatus.Active;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DischargePatientAsync(CLCDischargeDestination destination, string dischargeNotes)
    {
        _state.State.Status = CLCAdmissionStatus.Discharged;
        _state.State.ActualDischargeDate = DateTime.UtcNow;
        _state.State.DischargeDestination = destination;
        _state.State.DischargeNotes = dischargeNotes;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task MarkDeceasedAsync(string notes)
    {
        _state.State.Status = CLCAdmissionStatus.Deceased;
        _state.State.ActualDischargeDate = DateTime.UtcNow;
        _state.State.DischargeDestination = CLCDischargeDestination.Deceased;
        _state.State.DischargeNotes = notes;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<CLCAdmissionState> GetAdmissionAsync() => Task.FromResult(_state.State);
}
