// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Services;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Stateless-worker grain that runs the narrative composition (potentially a slow,
/// external model call) off the per-patient summary grain. Following the existing
/// StatelessWorker reader-grain pattern (drug-interaction checker, patient search),
/// the runtime pools many activations and load-balances across them, so concurrent
/// summaries don't serialize on one grain and the per-patient grain is never the thing
/// holding a network call open. Grain key is a fixed constant.
/// </summary>
public interface IClinicalNarrativeWorkerGrain : IGrainWithStringKey
{
    /// <summary>Composes a narrative from the grounded context via the configured provider.</summary>
    Task<NarrativeResult> ComposeAsync(ClinicalSummaryContext context);
}
