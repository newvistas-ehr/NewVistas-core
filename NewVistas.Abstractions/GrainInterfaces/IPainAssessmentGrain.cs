// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Grain for a single structured pain assessment using a validated tool.
/// Grain key: "NURS-PAIN:{guid}"
/// </summary>
public interface IPainAssessmentGrain : IGrainWithStringKey
{
    Task<PainAssessmentState> GetAsync();

    Task<string> CreateAsync(PainAssessmentState initialState);

    Task SignAsync(string nurseId, string nurseName);

    Task RecordReassessmentAsync(
        int postInterventionScore, int minutesSinceIntervention, bool interventionEffective);
}
