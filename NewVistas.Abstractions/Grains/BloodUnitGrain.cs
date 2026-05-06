// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Blood Unit Grain — grain key: "BB-UNIT:{unitId}"
/// </summary>
public class BloodUnitGrain : Grain, IBloodUnitGrain
{
    private readonly IPersistentState<BloodUnitState> _state;

    public BloodUnitGrain(
        [PersistentState("bbUnitState", "bbUnitStore")]
        IPersistentState<BloodUnitState> state)
    {
        _state = state;
    }

    public Task<BloodUnitState> GetUnitAsync() => Task.FromResult(_state.State);

    public async Task CreateAsync(
        BloodProductType productType,
        AboBloodType aboType,
        RhBloodType rhType,
        DateTime collectionDate,
        DateTime expirationDate,
        string? sourceFacility,
        string? donorId,
        string? productCode,
        decimal? volumeML,
        bool isIrradiated,
        bool isLeukoreduced,
        bool isWashed,
        bool isAntigenNegative,
        string? antigenNegativeFor,
        string? notes)
    {
        _state.State.UnitId = this.GetPrimaryKeyString();
        _state.State.ProductType = productType;
        _state.State.AboType = aboType;
        _state.State.RhType = rhType;
        _state.State.CollectionDate = collectionDate;
        _state.State.ExpirationDate = expirationDate;
        _state.State.Status = BloodUnitStatus.Available;
        _state.State.SourceFacility = sourceFacility;
        _state.State.DonorId = donorId;
        _state.State.ProductCode = productCode;
        _state.State.VolumeML = volumeML;
        _state.State.IsIrradiated = isIrradiated;
        _state.State.IsLeukoreduced = isLeukoreduced;
        _state.State.IsWashed = isWashed;
        _state.State.IsAntigenNegative = isAntigenNegative;
        _state.State.AntigenNegativeFor = antigenNegativeFor;
        _state.State.Notes = notes;
        _state.State.CreatedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ReserveAsync(string patientId, string crossmatchId)
    {
        _state.State.Status = BloodUnitStatus.Reserved;
        _state.State.ReservedForPatientId = patientId;
        _state.State.ReservedForCrossmatchId = crossmatchId;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task MarkTransfusedAsync(string patientId, string transfusionId, DateTime transfusionDate)
    {
        _state.State.Status = BloodUnitStatus.Transfused;
        _state.State.TransfusedToPatientId = patientId;
        _state.State.TransfusionId = transfusionId;
        _state.State.TransfusedDate = transfusionDate;
        _state.State.ReservedForPatientId = null;
        _state.State.ReservedForCrossmatchId = null;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task QuarantineAsync(string reason)
    {
        _state.State.Status = BloodUnitStatus.Quarantine;
        _state.State.DisposalReason = reason;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DiscardAsync(string disposalReason)
    {
        _state.State.Status = BloodUnitStatus.Discarded;
        _state.State.DisposalReason = disposalReason;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ReleaseReservationAsync()
    {
        _state.State.Status = BloodUnitStatus.Available;
        _state.State.ReservedForPatientId = null;
        _state.State.ReservedForCrossmatchId = null;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
