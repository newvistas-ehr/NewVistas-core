// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Patient Search Grain — StatelessWorker read layer over PATIENT-INDEX.
///
/// Grain Key: "PATIENT-SEARCH" (key is ignored by StatelessWorker routing)
///
/// Searches run against a silo-local immutable snapshot of the index,
/// validated against the index grain's version on every call (one 8-byte
/// payload) and refreshed by delta when behind — version-exact freshness with
/// no clock-based staleness, and search CPU scales out across silos instead
/// of funneling through the singleton index activation.
///
/// Writes still go to IPatientIndexGrain ("PATIENT-INDEX") — this grain is
/// read-only.
/// </summary>
public interface IPatientSearchGrain : IGrainWithStringKey
{
    /// <summary>
    /// Searches patients using the ORWPT LOOKUP heuristic (name prefix /
    /// SSN last-4 / DFN — see PatientIndexSearchHelper). Results are at most
    /// one version-check behind the index — effectively current.
    /// </summary>
    Task<List<PatientIndexEntry>> SearchAsync(string searchTerm, int maxResults = 25);
}
