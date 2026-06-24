// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Concurrency;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Services;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Stateless-worker host for the narrative service. Isolates the (possibly slow,
/// external) model call so the per-patient summary grain isn't pinned on network I/O,
/// and so many summaries can compose concurrently across pooled activations.
/// </summary>
[StatelessWorker]
public class ClinicalNarrativeWorkerGrain : Grain, IClinicalNarrativeWorkerGrain
{
    public const string Key = "CLINICAL-NARRATIVE";

    private readonly IClinicalNarrativeService _service;

    public ClinicalNarrativeWorkerGrain(IClinicalNarrativeService service)
    {
        _service = service;
    }

    public Task<NarrativeResult> ComposeAsync(ClinicalSummaryContext context) =>
        _service.ComposeAsync(context);
}
