// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class CSDispenseRecordGrain : Grain, ICSDispenseRecordGrain
{
    private readonly IPersistentState<CSDispenseRecordState> _state;

    public CSDispenseRecordGrain(
        [PersistentState("csDispenseState", "csDispenseStore")] IPersistentState<CSDispenseRecordState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.RecordId))
            _state.State.RecordId = this.GetPrimaryKeyString();
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<CSDispenseRecordState> GetRecordAsync() => Task.FromResult(_state.State);

    public async Task CreateRecordAsync(
        string locationId,
        string locationName,
        string patientId,
        string patientName,
        DateTime? patientDateOfBirth,
        string drugId,
        string drugName,
        DEADrugSchedule deaSchedule,
        string? ndcNumber,
        decimal quantityDispensed,
        string unitOfMeasure,
        decimal runningBalance,
        CSDispenseType dispenseType,
        string prescriberId,
        string prescriberName,
        string? prescriberDEANumber,
        string dispensedById,
        string dispensedByName,
        string? witnessId,
        string? witnessName,
        DateTime dispenseDateTime,
        string? prescriptionNumber,
        string? orderId,
        string? notes)
    {
        _state.State.LocationId = locationId;
        _state.State.LocationName = locationName;
        _state.State.PatientId = patientId;
        _state.State.PatientName = patientName;
        _state.State.PatientDateOfBirth = patientDateOfBirth;
        _state.State.DrugId = drugId;
        _state.State.DrugName = drugName;
        _state.State.DEASchedule = deaSchedule;
        _state.State.NdcNumber = ndcNumber;
        _state.State.QuantityDispensed = quantityDispensed;
        _state.State.UnitOfMeasure = unitOfMeasure;
        _state.State.RunningBalance = runningBalance;
        _state.State.DispenseType = dispenseType;
        _state.State.PrescriberId = prescriberId;
        _state.State.PrescriberName = prescriberName;
        _state.State.PrescriberDEANumber = prescriberDEANumber;
        _state.State.DispensedById = dispensedById;
        _state.State.DispensedByName = dispensedByName;
        _state.State.WitnessId = witnessId;
        _state.State.WitnessName = witnessName;
        _state.State.DispenseDateTime = dispenseDateTime;
        _state.State.PrescriptionNumber = prescriptionNumber;
        _state.State.OrderId = orderId;
        _state.State.Notes = notes;
        _state.State.CreatedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
