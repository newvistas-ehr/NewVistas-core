// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Facility-wide ROI request index grain.
/// Singleton key: "ROI-REQUEST-IDX".
/// </summary>
public interface IROIRequestIndexGrain : IGrainWithStringKey
{
    Task UpsertRequestAsync(ROIRequestIndexEntry entry);
    Task<List<ROIRequestIndexEntry>> GetAllRequestsAsync();
    Task<List<ROIRequestIndexEntry>> GetRequestsByStatusAsync(ROIRequestStatus status);
    Task<List<ROIRequestIndexEntry>> GetRequestsByPatientAsync(string patientId, int maxResults = 50);
    Task<List<ROIRequestIndexEntry>> GetOverdueRequestsAsync();
    Task<List<ROIRequestIndexEntry>> GetRequestsByRequesterTypeAsync(RequesterType requesterType);
}
