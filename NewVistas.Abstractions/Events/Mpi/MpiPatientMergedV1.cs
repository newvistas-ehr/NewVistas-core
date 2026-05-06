// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.Events.Clinical;

namespace NewVistas.Abstractions.Events.Mpi;

/// <summary>
/// Federation event: two patient records were merged at the originating
/// cluster. The source ICN is now permanently aliased to the target ICN.
/// Peer clusters that receive this event update their local
/// <see cref="GrainInterfaces.IMpiCorrelationGrain"/> for the source ICN
/// (set <c>MergedIntoIcn</c>) and stamp the same alias on their MPI search
/// entry so future searches by the source ICN flag the merge.
///
/// <see cref="EventEnvelope.PatientId"/> is set to the source ICN for
/// dispatch consistency (the entity changing state is the source).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record MpiPatientMergedV1 : IClinicalEvent
{
    public const string MpiDomain = MpiPatientRegisteredV1.MpiDomain;

    [Id(0)] public string EventId { get; init; } = string.Empty;
    [Id(1)] public string PatientId { get; init; } = string.Empty;     // = SourceIcn (the alias)
    [Id(2)] public string Domain { get; init; } = MpiDomain;
    [Id(3)] public DateTime OccurredUtc { get; init; }
    [Id(4)] public string? UserId { get; init; }
    [Id(5)] public string? UserName { get; init; }

    /// <summary>The duplicate ICN (now the alias).</summary>
    [Id(6)] public string SourceIcn { get; init; } = string.Empty;

    /// <summary>The surviving ICN (target of the alias).</summary>
    [Id(7)] public string TargetIcn { get; init; } = string.Empty;

    /// <summary>Originating facility (= the cluster id where the merge was performed).</summary>
    [Id(8)] public string OriginatingFacilityId { get; init; } = string.Empty;

    public string Canonicalize() => string.Join("|",
        nameof(MpiPatientMergedV1),
        SourceIcn,
        TargetIcn,
        OriginatingFacilityId);
}
