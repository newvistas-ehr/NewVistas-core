// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Tracks every inventory movement for a single drug at a single location.
/// Grain key: "DA:{locationId}:{drugId}"
/// </summary>
public class DrugAccountabilityGrain : Grain, IDrugAccountabilityGrain
{
    private readonly IPersistentState<DrugAccountabilityState> _state;

    public DrugAccountabilityGrain(
        [PersistentState("drugAccountabilityState", "drugAccountabilityStore")]
        IPersistentState<DrugAccountabilityState> state)
    {
        _state = state;
    }

    public Task<DrugAccountabilityState> GetStateAsync() =>
        Task.FromResult(_state.State);

    public async Task InitialiseAsync(string locationId, string locationName,
        string drugId, string drugName, string? ndc, bool isControlled,
        bool isVaultControlled, string unitOfMeasure, decimal? reorderPoint)
    {
        DrugAccountabilityState s = _state.State;
        s.LocationId = locationId;
        s.LocationName = locationName;
        s.DrugId = drugId;
        s.DrugName = drugName;
        s.Ndc = ndc;
        s.IsControlled = isControlled;
        s.IsVaultControlled = isVaultControlled;
        s.UnitOfMeasure = unitOfMeasure;
        s.ReorderPoint = reorderPoint;
        s.CurrentBalance = 0;
        s.CreatedDate = DateTime.UtcNow;
        s.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ReceiveStockAsync(decimal qty, string? lotNumber, string? ndc,
        string? userId, string? userName, string? notes)
    {
        await RecordTransaction("RECEIPT", qty, lotNumber: lotNumber, ndc: ndc,
            userId: userId, userName: userName, notes: notes);
    }

    public async Task DispenseToPatientAsync(decimal qty, string? patientId,
        string? prescriptionId, string? userId, string? userName)
    {
        await RecordTransaction("DISPENSE", -qty, patientId: patientId,
            prescriptionId: prescriptionId, userId: userId, userName: userName);
    }

    public async Task RecordReturnAsync(decimal qty, string? patientId,
        string? prescriptionId, string? userId, string? userName, string? notes)
    {
        await RecordTransaction("RETURN", qty, patientId: patientId,
            prescriptionId: prescriptionId, userId: userId, userName: userName, notes: notes);
    }

    public async Task RecordWasteAsync(decimal qty, string? witnessId, string? witnessName,
        string? userId, string? userName, string? reason)
    {
        await RecordTransaction("WASTE", -qty, witnessId: witnessId,
            witnessName: witnessName, userId: userId, userName: userName, notes: reason);
    }

    public async Task TransferToAsync(decimal qty, string destinationLocationId,
        string? userId, string? userName, string? notes)
    {
        await RecordTransaction("TRANSFER", -qty,
            destinationLocationId: destinationLocationId,
            userId: userId, userName: userName, notes: notes);
    }

    public async Task RecordInventoryCountAsync(decimal countedQty, string? userId,
        string? userName, string? notes)
    {
        // Delta may be positive (overage) or negative (shortage).
        decimal delta = countedQty - _state.State.CurrentBalance;
        await RecordTransaction("INVENTORY_COUNT", delta,
            userId: userId, userName: userName, notes: notes);
        _state.State.LastInventoryDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<decimal> GetBalanceAsync() =>
        Task.FromResult(_state.State.CurrentBalance);

    public Task<List<DrugAccountabilityTransaction>> GetTransactionHistoryAsync() =>
        Task.FromResult(_state.State.Transactions);

    // ─── private helper ────────────────────────────────────────────────────────

    private async Task RecordTransaction(string type, decimal signedQty,
        string? lotNumber = null, string? ndc = null,
        string? userId = null, string? userName = null,
        string? patientId = null, string? prescriptionId = null,
        string? destinationLocationId = null,
        string? witnessId = null, string? witnessName = null,
        string? notes = null)
    {
        DrugAccountabilityState s = _state.State;
        decimal before = s.CurrentBalance;
        s.CurrentBalance += signedQty;

        s.Transactions.Add(new DrugAccountabilityTransaction
        {
            TransactionId = Guid.NewGuid().ToString(),
            TransactionType = type,
            Quantity = signedQty,
            BalanceBefore = before,
            BalanceAfter = s.CurrentBalance,
            LotNumber = lotNumber,
            Ndc = ndc ?? s.Ndc,
            TransactionDate = DateTime.UtcNow,
            UserId = userId,
            UserName = userName,
            PatientId = patientId,
            PrescriptionId = prescriptionId,
            DestinationLocationId = destinationLocationId,
            WitnessId = witnessId,
            WitnessName = witnessName,
            Notes = notes
        });

        s.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
