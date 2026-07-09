// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Coded symptom capture — the surveillance input surface (Phase A of the Emerging Conditions
/// module). Feature-gated by <c>EMERGING_CONDITIONS</c>; when off, mutating calls are rejected
/// with a clear error and reads return empty so non-participating sites incur no surprises.
///
/// The <c>EmergingConditionsFeature</c> const and <c>RequireEmergingConditionsFeatureAsync</c>
/// helper defined here are shared by the later Emerging-Conditions workflow partial.
/// </summary>
public partial class PatientWorkflowGrain
{
    internal const string EmergingConditionsFeature = "EMERGING_CONDITIONS";

    private IPatientSymptomGrain PatientSymptoms() =>
        GrainFactory.GetGrain<IPatientSymptomGrain>($"SYMPTOMS:{PatientId}");

    public async Task<int> RecordSymptomObservationsAsync(List<SymptomObservation> observations)
    {
        await RequireEmergingConditionsFeatureAsync();
        return await PatientSymptoms().RecordObservationsAsync(observations ?? new());
    }

    public async Task<List<SymptomObservation>> GetPatientSymptomsAsync()
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(EmergingConditionsFeature);
        if (!enabled)
            return new();
        return await PatientSymptoms().GetLatestAsync();
    }

    private async Task RequireEmergingConditionsFeatureAsync()
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(EmergingConditionsFeature);
        if (!enabled)
            throw new InvalidOperationException(
                "Emerging-condition surveillance is not enabled for this site. Enable the EMERGING_CONDITIONS feature in Site Parameters.");
    }
}
