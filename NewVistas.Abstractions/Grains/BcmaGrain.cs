// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// BCMA Grain implementation based on VistA BCMA MEDICATION LOG file (#53.79)
/// </summary>
public class BcmaGrain : Grain, IBcmaGrain
{
    private readonly IPersistentState<BcmaState> _state;

    public BcmaGrain(
        [PersistentState("bcmaState", "bcmaStore")] IPersistentState<BcmaState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.AdministrationId))
        {
            _state.State.AdministrationId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<BcmaState> GetAdministrationAsync() => Task.FromResult(_state.State);

    public async Task RecordAdministrationAsync(
        string patientId, string drugName, string? drugId,
        string? dosage, string? route, string actionStatus,
        DateTime? scheduledDateTime, DateTime administrationDateTime,
        string? administeredById, string? administeredByName,
        string? injectionSite, string? prescriptionId,
        string? orderId, string? comments)
    {
        _state.State.PatientId = patientId;
        _state.State.DrugName = drugName;
        _state.State.DrugId = drugId;
        _state.State.Dosage = dosage;
        _state.State.Route = route;
        _state.State.ActionStatus = actionStatus;
        _state.State.ScheduledDateTime = scheduledDateTime;
        _state.State.AdministrationDateTime = administrationDateTime;
        _state.State.AdministeredById = administeredById;
        _state.State.AdministeredByName = administeredByName;
        _state.State.InjectionSite = injectionSite;
        _state.State.PrescriptionId = prescriptionId;
        _state.State.OrderId = orderId;
        _state.State.Comments = comments;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordWitnessAsync(string witnessId, string witnessName)
    {
        _state.State.WitnessId = witnessId;
        _state.State.WitnessName = witnessName;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordPrnReasonAsync(string reason)
    {
        _state.State.PrnReason = reason;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordPrnEffectivenessAsync(string effectiveness)
    {
        _state.State.PrnEffectiveness = effectiveness;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task MarkNotGivenAsync(string reason)
    {
        _state.State.ActionStatus = "NOT GIVEN";
        _state.State.ReasonNotGiven = reason;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
