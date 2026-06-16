// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Grain for a single nursing intake/triage assessment with ESI scoring.
/// Grain key: "NURS-TRIAGE:{guid}"
/// </summary>
public interface INursingTriageGrain : IGrainWithStringKey
{
    Task<NursingTriageState> GetAsync();

    Task<string> CreateAsync(NursingTriageState initialState);

    Task AssignTriageLevelAsync(TriageLevel level, int? expectedResources);

    Task SignAsync(string nurseId, string nurseName);

    Task SetDispositionAsync(TriageDisposition disposition);

    Task UpdateVitalsAsync(
        decimal? temperature, int? heartRate, int? respiratoryRate,
        int? systolicBP, int? diastolicBP, decimal? spO2, int? painScore);
}
