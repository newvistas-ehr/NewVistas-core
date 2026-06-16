// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.Events;

namespace NewVistas.Abstractions.Federation;

/// <summary>
/// No-op replication sink. The default for every site profile and the right
/// choice for a fully-isolated single-cluster deployment with no upstream
/// federation.
///
/// Lives in <c>NewVistas.Abstractions</c> rather than the silo host so it can
/// also be registered by test clusters in <c>NewVistas.UnitTests</c> without
/// dragging in silo-host infrastructure.
/// </summary>
public sealed class NullClinicalEventReplicationSink : IClinicalEventReplicationSink
{
    public Task PublishAsync(EventEnvelope envelope, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
