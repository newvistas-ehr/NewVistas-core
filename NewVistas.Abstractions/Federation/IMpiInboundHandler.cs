// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.Events;

namespace NewVistas.Abstractions.Federation;

/// <summary>
/// Receives MPI federation envelopes (Domain == <c>"MPI"</c>) on a cluster
/// and applies them to local MPI grains. Invoked by
/// <see cref="FederationInboundApplier"/> on the dispatch step.
///
/// One implementation registered as a singleton per silo — default
/// (<see cref="DefaultMpiInboundHandler"/>) handles the standard MPI events
/// (<c>MpiPatientRegisteredV1</c>, <c>MpiPatientMergedV1</c>). Deployments
/// that need additional MPI event types (e.g., demographics-update) register
/// their own.
/// </summary>
public interface IMpiInboundHandler
{
    /// <summary>
    /// Apply a single MPI envelope to local MPI grains. The applier already
    /// validated the envelope shape (non-empty PatientId, source-cluster
    /// stamped). Throw on unrecoverable errors; the calling applier counts
    /// them as failures and continues with the rest of the batch.
    /// </summary>
    Task ApplyAsync(EventEnvelope envelope, CancellationToken cancellationToken);
}
