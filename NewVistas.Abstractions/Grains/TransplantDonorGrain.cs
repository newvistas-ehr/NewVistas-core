// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class TransplantDonorGrain : Grain, ITransplantDonorGrain
{
    private readonly IPersistentState<TransplantDonorState> _state;

    public TransplantDonorGrain(
        [PersistentState("txDonorState", "txDonorStore")] IPersistentState<TransplantDonorState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.DonorId))
            _state.State.DonorId = this.GetPrimaryKeyString();
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<TransplantDonorState> GetDonorAsync() => Task.FromResult(_state.State);

    public async Task CreateDonorAsync(
        DonorType donorType,
        TransplantOrganType organType,
        string donorName,
        DateTime? dateOfBirth,
        BloodType bloodType,
        decimal? weightKg,
        decimal? heightCm,
        string? causeOfDeath,
        DateTime? crossClampDateTime,
        DateTime recoveryDateTime,
        DateTime? expirationDateTime,
        string? hlaTyping,
        decimal? coldIschemiaTimeHours,
        string locationId,
        string locationName,
        string recoveredById,
        string recoveredByName,
        string? notes)
    {
        _state.State.DonorType = donorType;
        _state.State.OrganType = organType;
        _state.State.DonorName = donorName;
        _state.State.DateOfBirth = dateOfBirth;
        _state.State.BloodType = bloodType;
        _state.State.WeightKg = weightKg;
        _state.State.HeightCm = heightCm;
        _state.State.CauseOfDeath = causeOfDeath;
        _state.State.CrossClampDateTime = crossClampDateTime;
        _state.State.RecoveryDateTime = recoveryDateTime;
        _state.State.ExpirationDateTime = expirationDateTime;
        _state.State.HlaTyping = hlaTyping;
        _state.State.ColdIschemiaTimeHours = coldIschemiaTimeHours;
        _state.State.LocationId = locationId;
        _state.State.LocationName = locationName;
        _state.State.RecoveredById = recoveredById;
        _state.State.RecoveredByName = recoveredByName;
        _state.State.Notes = notes;
        _state.State.Status = DonorStatus.Available;
        _state.State.CreatedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AllocateToPatientAsync(string patientId, string patientName, DateTime allocationDateTime)
    {
        _state.State.AllocatedToPatientId = patientId;
        _state.State.AllocatedToPatientName = patientName;
        _state.State.AllocationDateTime = allocationDateTime;
        _state.State.Status = DonorStatus.Allocated;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordTransplantAsync(DateTime transplantDateTime)
    {
        _state.State.TransplantDateTime = transplantDateTime;
        _state.State.Status = DonorStatus.Transplanted;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DiscardOrganAsync(string reason)
    {
        _state.State.DiscardReason = reason;
        _state.State.Status = DonorStatus.Discarded;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
