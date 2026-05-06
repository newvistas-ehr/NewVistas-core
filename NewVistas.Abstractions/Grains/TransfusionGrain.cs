// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Transfusion Grain — grain key: "BB-TX:{transfusionId}"
/// </summary>
public class TransfusionGrain : Grain, ITransfusionGrain
{
    private readonly IPersistentState<TransfusionState> _state;

    public TransfusionGrain(
        [PersistentState("bbTransfusionState", "bbTransfusionStore")]
        IPersistentState<TransfusionState> state)
    {
        _state = state;
    }

    public Task<TransfusionState> GetTransfusionAsync() => Task.FromResult(_state.State);

    public async Task StartAsync(
        string patientId,
        string unitId,
        string? crossmatchId,
        string productType,
        string aboType,
        string rhType,
        string administeredByUserId,
        string administeredByUserName,
        string orderedByUserId,
        string orderedByUserName,
        string? infusionSite,
        string? preTransfusionVitals)
    {
        _state.State.TransfusionId = this.GetPrimaryKeyString();
        _state.State.PatientId = patientId;
        _state.State.UnitId = unitId;
        _state.State.CrossmatchId = crossmatchId;
        _state.State.ProductType = productType;
        _state.State.AboType = aboType;
        _state.State.RhType = rhType;
        _state.State.StartDateTime = DateTime.UtcNow;
        _state.State.Status = TransfusionStatus.InProgress;
        _state.State.AdministeredByUserId = administeredByUserId;
        _state.State.AdministeredByUserName = administeredByUserName;
        _state.State.OrderedByUserId = orderedByUserId;
        _state.State.OrderedByUserName = orderedByUserName;
        _state.State.InfusionSite = infusionSite;
        _state.State.PreTransfusionVitals = preTransfusionVitals;
        _state.State.ReactionType = TransfusionReactionType.None;
        _state.State.CreatedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CompleteAsync(DateTime endDateTime, decimal? volumeML, string? postTransfusionVitals)
    {
        _state.State.EndDateTime = endDateTime;
        _state.State.VolumeML = volumeML;
        _state.State.PostTransfusionVitals = postTransfusionVitals;
        _state.State.Status = TransfusionStatus.Completed;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task StopAsync(
        DateTime endDateTime,
        string stopReason,
        TransfusionReactionType reactionType,
        string? reactionNotes)
    {
        _state.State.EndDateTime = endDateTime;
        _state.State.StopReason = stopReason;
        _state.State.ReactionType = reactionType;
        _state.State.ReactionNotes = reactionNotes;
        _state.State.Status = reactionType != TransfusionReactionType.None
            ? TransfusionStatus.Reaction
            : TransfusionStatus.Stopped;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
