// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Services;

/// <summary>
/// Decorator that makes the narrative seam robust to a fallible or unavailable model:
/// it tries a primary (live) service and, if that throws, falls back to a known-good
/// grounded service (the offline template). The summary is degraded but never broken —
/// the central thesis that the model is the least load-bearing part, made operational.
///
/// The fallback result is tagged so the provenance shows the live call was bypassed.
/// </summary>
public sealed class ResilientClinicalNarrativeService : IClinicalNarrativeService
{
    private readonly IClinicalNarrativeService _primary;
    private readonly IClinicalNarrativeService _fallback;

    public ResilientClinicalNarrativeService(
        IClinicalNarrativeService primary,
        IClinicalNarrativeService fallback)
    {
        _primary = primary;
        _fallback = fallback;
    }

    public bool IsLiveModel => _primary.IsLiveModel;

    public string ProviderName => _primary.ProviderName;

    public async Task<NarrativeResult> ComposeAsync(
        ClinicalSummaryContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _primary.ComposeAsync(context, cancellationToken);
        }
        catch (Exception)
        {
            NarrativeResult fallback = await _fallback.ComposeAsync(context, cancellationToken);
            fallback.ProviderName = $"{_fallback.ProviderName} (fallback from {_primary.ProviderName})";
            return fallback;
        }
    }
}
