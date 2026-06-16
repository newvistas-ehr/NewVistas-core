// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient index of EDI 276/277 claim status inquiries.
/// Grain key: "CSI-IDX:{patientId}"
/// </summary>
public interface IClaimStatusInquiryIndexGrain : IGrainWithStringKey
{
    Task<List<ClaimStatusInquiryIndexEntry>> GetAllAsync();
    Task AddOrUpdateAsync(ClaimStatusInquiryIndexEntry entry);
    Task<List<ClaimStatusInquiryIndexEntry>> GetByClaimAsync(string claimId);
}
