// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.Events.Clinical;

namespace NewVistas.Abstractions.Events.Mpi;

/// <summary>
/// Federation event: a patient was registered locally at the originating
/// cluster. Peer clusters that receive this event add the patient to their
/// own MPI search index so a clinician at any facility can find the patient
/// by name/SSN/DOB without a per-search cross-cluster query.
///
/// Carried over the same federation pipeline as clinical events: wrapped in
/// an <see cref="EventEnvelope"/>, sealed by the originating
/// <c>IPatientClinicalEventStreamGrain</c>, shipped via the federation
/// outbox + transport, and dispatched on the receiving silo by
/// <c>FederationInboundApplier</c> based on the <see cref="Domain"/> tag.
///
/// <para>
/// <b>Why an IClinicalEvent for non-clinical data?</b> Reuses the existing
/// federation wire format (envelope, hash chain, source-cluster stamping)
/// without a parallel transport. The Domain tag (<c>"MPI"</c>) tells the
/// inbound applier to route to the MPI inbound handler instead of the
/// per-patient clinical event stream.
/// </para>
/// </summary>
[GenerateSerializer, Immutable]
public sealed record MpiPatientRegisteredV1 : IClinicalEvent
{
    /// <summary>Constant domain tag — drives inbound applier dispatch to the MPI handler.</summary>
    public const string MpiDomain = "MPI";

    [Id(0)] public string EventId { get; init; } = string.Empty;
    [Id(1)] public string PatientId { get; init; } = string.Empty;     // = Icn
    [Id(2)] public string Domain { get; init; } = MpiDomain;
    [Id(3)] public DateTime OccurredUtc { get; init; }
    [Id(4)] public string? UserId { get; init; }
    [Id(5)] public string? UserName { get; init; }

    /// <summary>Patient name in <c>Last,First M</c> format.</summary>
    [Id(6)] public string PatientName { get; init; } = string.Empty;

    /// <summary>SSN; carried so peer indexes can be searched by SSN-last-4.</summary>
    [Id(7)] public string Ssn { get; init; } = string.Empty;

    /// <summary>Date of birth.</summary>
    [Id(8)] public DateTime? DateOfBirth { get; init; }

    /// <summary>Sex ("M" or "F").</summary>
    [Id(9)] public string? Sex { get; init; }

    /// <summary>Originating facility (= the cluster id where the patient was registered).</summary>
    [Id(10)] public string OriginatingFacilityId { get; init; } = string.Empty;

    public string Canonicalize() => string.Join("|",
        nameof(MpiPatientRegisteredV1),
        PatientName,
        Ssn,
        DateOfBirth?.ToString("O") ?? string.Empty,
        Sex ?? string.Empty,
        OriginatingFacilityId);
}
