// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Services;

/// <summary>
/// Validates a medication's route of administration against its dose form using
/// the RxNorm-derived <c>IDoseFormRouteIndexGrain</c>. Shared by the outpatient
/// pharmacy and inpatient order grains so the rule lives in exactly one place.
///
/// The check is WARN-ONLY: a mismatch yields <see cref="RouteValidationOutcome.Warn"/>
/// with suggested routes, never a hard block. It also fails open — an unknown or
/// unresolvable dose form returns <see cref="RouteValidationOutcome.Valid"/> — so
/// it never obstructs an order it cannot evaluate.
/// </summary>
public interface IRouteValidationService
{
    /// <summary>
    /// Validates <paramref name="route"/> against a known <paramref name="doseForm"/>.
    /// Returns Valid when either is blank or the dose form is unmapped.
    /// </summary>
    Task<RouteValidationResult> ValidateAsync(IGrainFactory grainFactory, string? doseForm, string? route);

    /// <summary>
    /// Resolves the dose form for a DRUG (#50) id and validates the route against
    /// it. Used by order-creation grains that carry only a drug pointer.
    /// </summary>
    Task<RouteValidationResult> ValidateByDrugAsync(IGrainFactory grainFactory, string? drugId, string? route);

    /// <summary>
    /// Resolves a drug's dose form: prefers the linked NDF VA Product's
    /// DosageFormName (#50.68), falling back to the local drug's DispenseUnit
    /// (#50, field 901). Returns null when nothing resolves.
    /// </summary>
    Task<string?> ResolveDoseFormAsync(IGrainFactory grainFactory, string? drugId);
}
