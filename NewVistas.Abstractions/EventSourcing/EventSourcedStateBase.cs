// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.Events;

namespace NewVistas.Abstractions.EventSourcing;

/// <summary>
/// Base class for grain state that participates in the clinical event-sourcing
/// outbox pattern. Holds the in-grain pending-event queue alongside the live
/// projection state, so a single <c>WriteStateAsync</c> persists both atomically.
///
/// Domain grain state classes inherit from this and add their own
/// <c>[Id(n)]</c>-decorated fields. The reserved <c>[Id]</c> range for outbox
/// fields starts at <see cref="OutboxIdBase"/> — domain states must not use
/// <c>[Id]</c> values in <c>[OutboxIdBase, OutboxIdBase+15]</c>.
///
/// The drain side (<see cref="OutboxDrainExtensions.DrainOutboxAsync"/>) reads
/// <see cref="PendingEvents"/>, ships envelopes to
/// <see cref="GrainInterfaces.IPatientClinicalEventStreamGrain"/>, and clears
/// drained envelopes via a follow-up <c>WriteStateAsync</c>.
/// </summary>
[GenerateSerializer]
public abstract class EventSourcedStateBase
{
    /// <summary>
    /// Reserved [Id] base for outbox fields. Subclass states must not use any
    /// [Id] in [OutboxIdBase, OutboxIdBase+15] for their own fields. 9000 was
    /// chosen to sit well above the highest current domain-state Id (PatientState
    /// uses up to [Id(79)]) so existing serialization is undisturbed.
    /// </summary>
    public const int OutboxIdBase = 9000;

    /// <summary>
    /// Pending event envelopes queued for delivery to the patient's clinical
    /// event stream grain. Persisted alongside the projection so the outbox
    /// survives grain reactivation.
    /// </summary>
    [Id(OutboxIdBase)]
    public List<EventEnvelope> PendingEvents { get; set; } = new();
}
