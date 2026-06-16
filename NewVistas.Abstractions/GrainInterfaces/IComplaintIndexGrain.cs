// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Facility-wide complaint index grain.
/// Singleton key: "PA-COMPLAINT-IDX".
/// </summary>
public interface IComplaintIndexGrain : IGrainWithStringKey
{
    Task UpsertComplaintAsync(ComplaintIndexEntry entry);
    Task<List<ComplaintIndexEntry>> GetAllComplaintsAsync();
    Task<List<ComplaintIndexEntry>> GetComplaintsByStatusAsync(ComplaintStatus status);
    Task<List<ComplaintIndexEntry>> GetComplaintsByPatientAsync(string patientId, int maxResults = 50);
    Task<List<ComplaintIndexEntry>> GetComplaintsByTypeAsync(ComplaintType complaintType);
    Task<List<ComplaintIndexEntry>> GetOverdueComplaintsAsync();
}
