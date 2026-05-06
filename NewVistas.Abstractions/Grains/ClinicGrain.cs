// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class ClinicGrain : Grain, IClinicGrain
{
    private readonly IPersistentState<ClinicState> _state;

    public ClinicGrain(
        [PersistentState("clinicState", "clinicStore")]
        IPersistentState<ClinicState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.ClinicId))
        {
            _state.State.ClinicId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<ClinicState> GetClinicAsync() => Task.FromResult(_state.State);

    public async Task CreateClinicAsync(
        string name,
        string? division,
        string? stopCode,
        string? phoneNumber,
        string? physicalLocation,
        int appointmentLength,
        int maxPatientsPerDay,
        bool allowOverbooking,
        string? clinicType,
        string? primaryProviderId,
        string? primaryProviderName)
    {
        _state.State.Name = name;
        _state.State.Division = division;
        _state.State.StopCode = stopCode;
        _state.State.PhoneNumber = phoneNumber;
        _state.State.PhysicalLocation = physicalLocation;
        _state.State.AppointmentLength = appointmentLength;
        _state.State.MaxPatientsPerDay = maxPatientsPerDay;
        _state.State.AllowOverbooking = allowOverbooking;
        _state.State.ClinicType = clinicType ?? "C";
        _state.State.PrimaryProviderId = primaryProviderId;
        _state.State.PrimaryProviderName = primaryProviderName;
        _state.State.Status = "ACTIVE";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateClinicAsync(
        string? name,
        string? division,
        string? phoneNumber,
        string? physicalLocation,
        int? appointmentLength,
        int? maxPatientsPerDay,
        bool? allowOverbooking,
        string? status)
    {
        if (name != null) _state.State.Name = name;
        if (division != null) _state.State.Division = division;
        if (phoneNumber != null) _state.State.PhoneNumber = phoneNumber;
        if (physicalLocation != null) _state.State.PhysicalLocation = physicalLocation;
        if (appointmentLength.HasValue) _state.State.AppointmentLength = appointmentLength.Value;
        if (maxPatientsPerDay.HasValue) _state.State.MaxPatientsPerDay = maxPatientsPerDay.Value;
        if (allowOverbooking.HasValue) _state.State.AllowOverbooking = allowOverbooking.Value;
        if (status != null) _state.State.Status = status;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
