// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Blood Unit Grain — represents a single blood product unit in inventory.
///
/// Derived from VistA Blood Bank module:
///   File #65.04  — BLOOD UNIT (product type, ABO/Rh, status, collection info)
///
/// Grain key: "BB-UNIT:{unitId}"
/// </summary>
public interface IBloodUnitGrain : IGrainWithStringKey
{
    Task<BloodUnitState> GetUnitAsync();

    /// <summary>Adds a new blood unit to inventory.</summary>
    Task CreateAsync(
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
        string? notes);

    /// <summary>Reserves a unit for a specific patient crossmatch.</summary>
    Task ReserveAsync(string patientId, string crossmatchId);

    /// <summary>Marks the unit as transfused. Returns to clear reserved state.</summary>
    Task MarkTransfusedAsync(string patientId, string transfusionId, DateTime transfusionDate);

    /// <summary>Quarantines the unit (e.g., for reaction investigation).</summary>
    Task QuarantineAsync(string reason);

    /// <summary>Discards the unit (expired, failed QC, contaminated, etc.).</summary>
    Task DiscardAsync(string disposalReason);

    /// <summary>Releases a reservation if the crossmatch is cancelled.</summary>
    Task ReleaseReservationAsync();
}
