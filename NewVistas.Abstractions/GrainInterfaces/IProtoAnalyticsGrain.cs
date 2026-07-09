// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.Clinical;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Net-closing analytics worker for one proto-condition. Grain key: <c>PROTO-ANALYTICS:{protoConditionId}</c>.
/// Stateless compute: reads the proto's confirmed cohort and the assessed-population background
/// (from the symptom cohort shards / curated catalog), then runs the deterministic analytics engine.
/// Open read — surveillance analytics are part of the chart, not a privacy silo.
/// </summary>
public interface IProtoAnalyticsGrain : IGrainWithStringKey
{
    /// <summary>Computes the feature-signal lift report, refinement suggestions, and split evidence.</summary>
    Task<ProtoAnalyticsReport> AnalyzeAsync();
}
