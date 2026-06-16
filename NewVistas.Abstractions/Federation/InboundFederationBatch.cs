// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.Federation;

/// <summary>
/// Wire format for a federation push: a list of Orleans-serialized envelope
/// blobs plus the authenticated sender id. JSON-friendly because the
/// <c>byte[]</c> arrays serialize as base64 strings out of the box with
/// <c>System.Text.Json</c>.
///
/// Defined here (rather than in the WebServer) so the future outbound HTTP
/// transport on <c>RemoteOnlineProfile</c> can serialize the same shape
/// without duplicating the contract.
/// </summary>
[GenerateSerializer]
public sealed record InboundFederationBatch
{
    /// <summary>Authenticated cluster id of the sender.</summary>
    [Id(0)] public string FromClusterId { get; init; } = string.Empty;

    /// <summary>Each blob is one Orleans-serialized <c>EventEnvelope</c>.</summary>
    [Id(1)] public List<byte[]> EnvelopeBlobs { get; init; } = new();
}
