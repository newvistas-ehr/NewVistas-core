// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.SiloHost.Infrastructure.Federation;

/// <summary>
/// One row in the federation outbox. Mirrors the columns of
/// <c>FederationOutbox</c> minus the bookkeeping fields the repository
/// manages internally (<c>EnqueuedUtc</c>, <c>Attempts</c>, <c>NextAttemptUtc</c>).
/// </summary>
public sealed record OutboxRow(
    string EventId,
    string PatientId,
    string Domain,
    string EventType,
    DateTime OccurredUtc,
    string SourceClusterId,
    string EventHash,
    string PreviousEventHash,
    byte[] EnvelopeBlob);
