// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Grain representing a single Stanley-Brown Safety Plan for a patient.
/// Key pattern: "SP-PLAN:{guid}"
/// </summary>
public interface ISafetyPlanGrain : IGrainWithStringKey
{
    Task<SafetyPlanState> GetPlanAsync();

    Task CreatePlanAsync(string planId, string patientId, string patientName, string providerId, string providerName);

    Task UpdateWarningSigns(List<string> signs);

    Task UpdateCopingStrategies(List<string> strategies);

    Task UpdateContacts(
        List<string> distractionContacts,
        List<SupportContact> supportContacts,
        List<ProfessionalContact> professionalContacts,
        List<string> crisisLineNumbers);

    Task UpdateMeansRestriction(List<string> meansRemoved, string notes);

    Task UpdateReasonsForLiving(List<string> reasons);

    Task ReviewPlanAsync(DateTime reviewDate);

    Task ArchivePlanAsync();
}
