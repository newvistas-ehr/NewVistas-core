// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Bed Grain — manages a single hospital bed.
/// Key: "BED:{facilityId}:{bedId}"
/// Maps to VistA DG Bed Control / File #405.4.
/// </summary>
public interface IBedGrain : IGrainWithStringKey
{
    Task<BedState> GetBedAsync();

    Task SetupBedAsync(string wardId, string wardName, string roomNumber,
        string bedPosition, string bedType, string facilityId);

    Task AssignPatientAsync(string patientId, string patientName, DateTime? expectedDischarge);
    Task DischargePatientAsync();
    Task ReserveForPatientAsync(string patientId, string patientName);
    Task ClearReservationAsync();
    Task BlockBedAsync(string reason);
    Task UnblockBedAsync();
    Task SetCleaningAsync();
    Task SetAvailableAsync();
    Task SetIsolationAsync(string? isolationType);
    Task SetOutOfServiceAsync(string reason);
}

/// <summary>
/// Bed Board Grain — facility-wide bed index for real-time display.
/// Key: "BED-BOARD:{facilityId}"
/// </summary>
public interface IBedBoardGrain : IGrainWithStringKey
{
    Task<List<BedSummaryEntry>> GetAllBedsAsync();
    Task<List<BedSummaryEntry>> GetBedsByWardAsync(string wardId);
    Task<List<BedSummaryEntry>> GetAvailableBedsAsync();
    Task<List<BedSummaryEntry>> GetAvailableBedsByTypeAsync(string bedType);
    Task AddOrUpdateBedAsync(BedSummaryEntry entry);
    Task RemoveBedAsync(string bedId);

    Task<int> GetTotalBedCountAsync();
    Task<int> GetAvailableBedCountAsync();
    Task<int> GetOccupiedBedCountAsync();
}
