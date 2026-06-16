// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.Federation;

/// <summary>
/// Result of an <see cref="IFederationInboundApplier.ApplyBatchAsync"/> call.
/// Sender uses these counts for diagnostics; <see cref="Applied"/> +
/// <see cref="Errors"/> equals <see cref="Total"/>.
///
/// "Applied" includes events that the receiver's stream grain dedup'd as
/// duplicates (idempotent on <c>EventId</c>) — from the sender's perspective
/// the operation succeeded either way. Distinguishing fresh-vs-duplicate
/// requires comparing grain version before/after; defer until a transport
/// actually needs it.
/// </summary>
public sealed record InboundApplyResult(int Total, int Applied, int Errors)
{
    public static InboundApplyResult Empty { get; } = new(0, 0, 0);
}
