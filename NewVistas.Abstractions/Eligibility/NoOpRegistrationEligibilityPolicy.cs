// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;

namespace NewVistas.Abstractions.Eligibility;

/// <summary>
/// Default policy: do nothing. Used by deployments that have no automated
/// eligibility rules to apply at registration (dev/test, IHS, international,
/// private clinics that handle eligibility through a separate workflow).
///
/// Registered via <c>TryAddSingleton</c> in <c>CommonSiloConfig.AddCommonSiloServices</c>
/// so it is the silent default; VA-aligned profiles replace it explicitly.
/// </summary>
public sealed class NoOpRegistrationEligibilityPolicy : IRegistrationEligibilityPolicy
{
    public Task DetermineAndApplyAsync(
        string icn,
        RegistrationRequest request,
        IPatientWorkflowGrain workflow) =>
        Task.CompletedTask;
}
