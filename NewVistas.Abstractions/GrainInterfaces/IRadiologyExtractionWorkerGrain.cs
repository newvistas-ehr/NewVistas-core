// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.Services;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Stateless-worker grain that runs radiology-finding extraction (potentially a slow,
/// external model call) off the per-report extraction grain — same isolation pattern as
/// the clinical-narrative worker. Grain key is a fixed constant.
/// </summary>
public interface IRadiologyExtractionWorkerGrain : IGrainWithStringKey
{
    Task<RadiologyExtractionResult> ExtractAsync(string reportText);
}
