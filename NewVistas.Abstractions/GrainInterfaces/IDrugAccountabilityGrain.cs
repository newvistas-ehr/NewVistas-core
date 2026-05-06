// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-drug-per-location accountability grain — tracks every inventory movement
/// with before/after balances. Mirrors VistA File #58.8/#58.81 (DRUG ACCOUNTABILITY).
/// Grain key: "DA:{locationId}:{drugId}"
/// </summary>
public interface IDrugAccountabilityGrain : IGrainWithStringKey
{
    Task<DrugAccountabilityState> GetStateAsync();

    Task InitialiseAsync(string locationId, string locationName, string drugId,
        string drugName, string? ndc, bool isControlled, bool isVaultControlled,
        string unitOfMeasure, decimal? reorderPoint);

    /// <summary>Record incoming stock (RECEIPT). Positive quantity.</summary>
    Task ReceiveStockAsync(decimal qty, string? lotNumber, string? ndc,
        string? userId, string? userName, string? notes);

    /// <summary>Dispense to a patient (DISPENSE). Removes from balance.</summary>
    Task DispenseToPatientAsync(decimal qty, string? patientId, string? prescriptionId,
        string? userId, string? userName);

    /// <summary>Patient returns unused medication (RETURN). Adds to balance.</summary>
    Task RecordReturnAsync(decimal qty, string? patientId, string? prescriptionId,
        string? userId, string? userName, string? notes);

    /// <summary>Record wasted / destroyed quantity. Witness required for controlled substances.</summary>
    Task RecordWasteAsync(decimal qty, string? witnessId, string? witnessName,
        string? userId, string? userName, string? reason);

    /// <summary>Transfer quantity out to another location (TRANSFER).</summary>
    Task TransferToAsync(decimal qty, string destinationLocationId,
        string? userId, string? userName, string? notes);

    /// <summary>Physical inventory count — creates ADJUSTMENT record for any discrepancy.</summary>
    Task RecordInventoryCountAsync(decimal countedQty, string? userId,
        string? userName, string? notes);

    Task<decimal> GetBalanceAsync();

    Task<List<DrugAccountabilityTransaction>> GetTransactionHistoryAsync();
}
