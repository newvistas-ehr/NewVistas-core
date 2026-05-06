// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Federation;

/// <summary>
/// Default <see cref="IMpiFederationAnnouncer"/> for single-cluster
/// deployments and tests. Does nothing — registration and merge complete
/// with the same on-cluster effects whether or not federation is wired up.
///
/// Registered via <c>TryAddSingleton</c> in <c>CommonSiloConfig.AddCommonSiloServices</c>;
/// federated profiles override with an outbox-backed implementation.
/// </summary>
public sealed class NoOpMpiFederationAnnouncer : IMpiFederationAnnouncer
{
    public Task AnnouncePatientRegisteredAsync(MpiSearchEntry searchEntry, string originatingFacilityId) =>
        Task.CompletedTask;

    public Task AnnouncePatientMergedAsync(string sourceIcn, string targetIcn, string originatingFacilityId) =>
        Task.CompletedTask;
}
