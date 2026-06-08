// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Services;

/// <summary>
/// Silo-level singleton implementation of <see cref="IRouteValidationService"/>.
/// Stateless: it resolves dose forms and valid routes through grains supplied per
/// call, so a single instance is safely shared across the silo.
/// </summary>
public sealed class RouteValidationService : IRouteValidationService
{
    /// <summary>Well-known key of the dose-form/route index singleton grain.</summary>
    public const string IndexKey = "DOSE-FORM-ROUTE-INDEX";

    /// <inheritdoc/>
    public async Task<RouteValidationResult> ValidateAsync(IGrainFactory grainFactory, string? doseForm, string? route)
    {
        // Nothing to evaluate → pass (fail open).
        if (string.IsNullOrWhiteSpace(doseForm) || string.IsNullOrWhiteSpace(route))
            return RouteValidationResult.Valid(doseForm, route);

        IDoseFormRouteIndexGrain index = grainFactory.GetGrain<IDoseFormRouteIndexGrain>(IndexKey);
        List<string> validRoutes = await index.GetValidRoutesForDoseFormAsync(doseForm);

        // Unknown/unmapped dose form → pass (fail open).
        if (validRoutes.Count == 0)
            return RouteValidationResult.Valid(doseForm, route);

        if (validRoutes.Contains(route.Trim(), StringComparer.OrdinalIgnoreCase))
            return RouteValidationResult.Valid(doseForm, route);

        return new RouteValidationResult
        {
            Outcome = RouteValidationOutcome.Warn,
            DoseForm = doseForm,
            Route = route,
            SuggestedRoutes = validRoutes,
            Message =
                $"Route '{route}' is not a typical route for dose form '{doseForm}'. " +
                $"Expected one of: {string.Join(", ", validRoutes)}."
        };
    }

    /// <inheritdoc/>
    public async Task<RouteValidationResult> ValidateByDrugAsync(IGrainFactory grainFactory, string? drugId, string? route)
    {
        string? doseForm = await ResolveDoseFormAsync(grainFactory, drugId);
        return await ValidateAsync(grainFactory, doseForm, route);
    }

    /// <inheritdoc/>
    public async Task<string?> ResolveDoseFormAsync(IGrainFactory grainFactory, string? drugId)
    {
        if (string.IsNullOrWhiteSpace(drugId))
            return null;

        DrugState drug = await grainFactory.GetGrain<IDrugGrain>(drugId).GetDrugAsync();

        // Prefer the NDF VA Product's dosage form (the canonical dose form).
        if (!string.IsNullOrWhiteSpace(drug.VaProductIen))
        {
            VaProductState product = await grainFactory.GetGrain<IVaProductGrain>(drug.VaProductIen).GetProductAsync();
            if (!string.IsNullOrWhiteSpace(product.DosageFormName))
                return product.DosageFormName;
        }

        // Fall back to the local drug's dispense unit (e.g. "TABLET", "ML").
        return string.IsNullOrWhiteSpace(drug.DispenseUnit) ? null : drug.DispenseUnit;
    }
}
