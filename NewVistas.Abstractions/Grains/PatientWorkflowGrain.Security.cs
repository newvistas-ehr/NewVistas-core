// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public partial class PatientWorkflowGrain
{
    // ─── Security / Patient Access (DG SENSITIVITY, XUSEC) ───────────────

    private IPatientAccessControlGrain GetPatientAccessControlGrain()
        => GrainFactory.GetGrain<IPatientAccessControlGrain>($"PAC:{PatientId}");

    /// <summary>
    /// Set patient sensitivity flags on both the PAC grain and the patient grain.
    /// </summary>
    public async Task SetPatientSensitivityAsync(bool isSensitive, string sensitivityLevel, List<string> categories)
    {
        await GetPatientAccessControlGrain().SetSensitivityAsync(isSensitive, sensitivityLevel, categories);

        // Mirror on the patient grain for fast cover sheet lookup
        await GetPatientGrain().UpdateSensitivityFlagsAsync(isSensitive, sensitivityLevel);
    }

    public async Task<bool> CheckPatientAccessAsync(string userId)
    {
        // Check explicit authorized provider list first (PAC grain)
        bool authorized = await GetPatientAccessControlGrain().CheckAccessAsync(userId);
        if (authorized) return true;

        // Check care team membership as secondary authorization
        return await GetCareTeamGrain().HasActiveMemberAsync(userId);
    }

    public Task RecordPatientAccessAsync(string userId, string userName, string accessReason, bool wasBreakTheGlass, string? justificationText)
        => GetPatientAccessControlGrain().RecordAccessAsync(userId, userName, accessReason, wasBreakTheGlass, justificationText);

    public Task AddAuthorizedProviderAsync(string providerId)
        => GetPatientAccessControlGrain().AddAuthorizedProviderAsync(providerId);

    public Task RemoveAuthorizedProviderAsync(string providerId)
        => GetPatientAccessControlGrain().RemoveAuthorizedProviderAsync(providerId);

    public Task<List<PatientAccessLog>> GetPatientAccessLogAsync()
        => GetPatientAccessControlGrain().GetAccessLogAsync();
}
