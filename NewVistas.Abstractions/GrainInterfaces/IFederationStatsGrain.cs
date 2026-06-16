// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton grain that computes federation health stats on demand. Today
/// returns only outbox stats; future panels (peer activity, issued certs)
/// extend this surface.
///
/// One grain per cluster, keyed by <see cref="GlobalKey"/>. Implementation
/// lives silo-side (it queries the local SQL outbox); profiles without an
/// outbox configured return <see cref="OutboxStats.NotAvailable"/>.
/// </summary>
public interface IFederationStatsGrain : IGrainWithStringKey
{
    public const string GlobalKey = "GLOBAL";

    /// <summary>
    /// Aggregate snapshot of the federation outbox: pending count, sent
    /// count, oldest pending row, max attempts on a pending row, last
    /// sent timestamp.
    /// </summary>
    Task<OutboxStats> GetOutboxStatsAsync();
}
