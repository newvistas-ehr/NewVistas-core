// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Concurrency;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.Services;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Stateless-worker host for the radiology-finding extractor. Isolates the (possibly slow,
/// external) model call so the per-report extraction grain isn't pinned on network I/O.
/// </summary>
[StatelessWorker]
public class RadiologyExtractionWorkerGrain : Grain, IRadiologyExtractionWorkerGrain
{
    public const string Key = "RADIOLOGY-EXTRACTION";

    private readonly IRadiologyFindingExtractor _extractor;

    public RadiologyExtractionWorkerGrain(IRadiologyFindingExtractor extractor)
    {
        _extractor = extractor;
    }

    public Task<RadiologyExtractionResult> ExtractAsync(string reportText) =>
        _extractor.ExtractAsync(reportText);
}
