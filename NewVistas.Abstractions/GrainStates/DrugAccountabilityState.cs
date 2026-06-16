// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// One inventory movement event — mirrors VistA File #58.81 (DRUG ACCOUNTABILITY TRANSACTION).
/// </summary>
[GenerateSerializer]
public class DrugAccountabilityTransaction
{
    /// <summary>Unique transaction ID (Guid string)</summary>
    [Id(0)]
    public string TransactionId { get; set; } = string.Empty;

    /// <summary>
    /// RECEIPT | DISPENSE | RETURN | WASTE | TRANSFER | ADJUSTMENT | INVENTORY_COUNT
    /// </summary>
    [Id(1)]
    public string TransactionType { get; set; } = string.Empty;

    /// <summary>Signed quantity: positive = stock in, negative = stock out</summary>
    [Id(2)]
    public decimal Quantity { get; set; }

    [Id(3)]
    public decimal BalanceBefore { get; set; }

    [Id(4)]
    public decimal BalanceAfter { get; set; }

    [Id(5)]
    public string? LotNumber { get; set; }

    [Id(6)]
    public string? Ndc { get; set; }

    [Id(7)]
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;

    [Id(8)]
    public string? UserId { get; set; }

    [Id(9)]
    public string? UserName { get; set; }

    /// <summary>Patient who received the drug (DISPENSE only)</summary>
    [Id(10)]
    public string? PatientId { get; set; }

    /// <summary>Prescription or order that triggered the dispense</summary>
    [Id(11)]
    public string? PrescriptionId { get; set; }

    /// <summary>Destination location (TRANSFER only)</summary>
    [Id(12)]
    public string? DestinationLocationId { get; set; }

    /// <summary>Witness required for controlled-substance WASTE</summary>
    [Id(13)]
    public string? WitnessId { get; set; }

    [Id(14)]
    public string? WitnessName { get; set; }

    [Id(15)]
    public string? Notes { get; set; }
}

/// <summary>
/// Per-drug-per-location inventory state — mirrors VistA File #58.8 (DRUG ACCOUNTABILITY STATS).
/// Grain key: "DA:{locationId}:{drugId}"
/// </summary>
[GenerateSerializer]
public class DrugAccountabilityState
{
    [Id(0)]
    public string DrugId { get; set; } = string.Empty;

    [Id(1)]
    public string DrugName { get; set; } = string.Empty;

    [Id(2)]
    public string LocationId { get; set; } = string.Empty;

    [Id(3)]
    public string LocationName { get; set; } = string.Empty;

    [Id(4)]
    public string? Ndc { get; set; }

    /// <summary>True if DEA scheduled controlled substance</summary>
    [Id(5)]
    public bool IsControlled { get; set; }

    /// <summary>True if stored in double-lock vault (Schedule II)</summary>
    [Id(6)]
    public bool IsVaultControlled { get; set; }

    [Id(7)]
    public decimal CurrentBalance { get; set; }

    /// <summary>TABLET, ML, VIAL, PATCH, etc.</summary>
    [Id(8)]
    public string UnitOfMeasure { get; set; } = string.Empty;

    /// <summary>Balance at or below which a low-stock alert fires</summary>
    [Id(9)]
    public decimal? ReorderPoint { get; set; }

    [Id(10)]
    public DateTime? LastInventoryDate { get; set; }

    [Id(11)]
    public List<DrugAccountabilityTransaction> Transactions { get; set; } = new();

    [Id(12)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Id(13)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Lightweight balance projection stored in the per-location index.
/// </summary>
[GenerateSerializer]
public class DrugBalanceSummary
{
    [Id(0)]
    public string DrugId { get; set; } = string.Empty;

    [Id(1)]
    public string DrugName { get; set; } = string.Empty;

    [Id(2)]
    public decimal CurrentBalance { get; set; }

    [Id(3)]
    public string UnitOfMeasure { get; set; } = string.Empty;

    [Id(4)]
    public bool IsControlled { get; set; }

    [Id(5)]
    public decimal? ReorderPoint { get; set; }

    [Id(6)]
    public string? Ndc { get; set; }
}

/// <summary>
/// Persistent state for the per-location drug inventory index grain.
/// Grain key: "DA-LOC:{locationId}"
/// </summary>
[GenerateSerializer]
public class DrugAccountabilityLocationState
{
    [Id(0)]
    public string LocationId { get; set; } = string.Empty;

    [Id(1)]
    public string LocationName { get; set; } = string.Empty;

    /// <summary>VAULT | DISPENSING | WARD_STOCK | AUTOMATED_DISPENSING</summary>
    [Id(2)]
    public string LocationType { get; set; } = string.Empty;

    [Id(3)]
    public List<DrugBalanceSummary> DrugBalances { get; set; } = new();

    [Id(4)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
