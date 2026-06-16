// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient POS claim index grain.
/// Key: "POS-CLAIM-IDX:{patientId}"
/// </summary>
public interface IPharmacyPosClaimIndexGrain : IGrainWithStringKey
{
    Task<List<GrainStates.PosClaimIndexEntry>> GetAllAsync();
    Task<List<GrainStates.PosClaimIndexEntry>> GetByStatusAsync(GrainStates.PosClaimStatus status);
    Task AddEntryAsync(GrainStates.PosClaimIndexEntry entry);
    Task UpdateEntryStatusAsync(string claimId, GrainStates.PosClaimStatus status,
        decimal? insurancePaidAmount, decimal? patientResponsibility);
}
