// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Facility-wide Congressional inquiry index grain.
/// Singleton key: "PA-CONGRESS-IDX".
/// </summary>
public interface ICongressionalInquiryIndexGrain : IGrainWithStringKey
{
    Task UpsertInquiryAsync(CongressionalInquiryIndexEntry entry);
    Task<List<CongressionalInquiryIndexEntry>> GetAllInquiriesAsync();
    Task<List<CongressionalInquiryIndexEntry>> GetPendingInquiriesAsync();
    Task<List<CongressionalInquiryIndexEntry>> GetOverdueInquiriesAsync();
    Task<List<CongressionalInquiryIndexEntry>> GetInquiriesByPatientAsync(string patientId, int maxResults = 50);
}
