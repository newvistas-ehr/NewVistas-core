// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Audit Event Grain — immutable record of a single auditable action.
///
/// Based on VistA AUDIT file (#1.1) from the Kernel package (XUSEC routines).
/// Every action that creates, modifies, or views sensitive clinical data generates
/// an audit event. Once written, the event is never mutated.
///
/// Grain Key: "AUDIT-{guid}"
/// </summary>
public interface IAuditEventGrain : IGrainWithStringKey
{
    /// <summary>
    /// Get the full audit event record.
    /// </summary>
    Task<GrainStates.AuditEventState> GetEventAsync();

    /// <summary>
    /// Write the audit event. Can only be called once — subsequent calls are no-ops.
    /// Mirrors VistA XUSEC LOG which writes to ^XTV(8989.3,) and AUDIT file (#1.1).
    /// Computes a tamper-evident hash chain per §170.315(d)(2).
    /// </summary>
    /// <param name="previousEventHash">
    /// Hash of the previous audit event in this patient's chain.
    /// Use <see cref="GenesisHash"/> for the first event in a patient's chain.
    /// </param>
    Task RecordAsync(
        string patientId,
        string domain,
        string action,
        string entityType,
        string entityId,
        string? userId,
        string? userName,
        string? locationId,
        string? locationName,
        string? details,
        string? oldValue,
        string? newValue,
        string previousEventHash);

    /// <summary>
    /// Verify this event's integrity by recomputing the hash from stored fields.
    /// Returns true if the stored EventHash matches — the event has not been tampered with.
    /// Part of §170.315(d)(2) tamper-resistance requirement.
    /// </summary>
    Task<bool> VerifyIntegrityAsync();

    /// <summary>
    /// Genesis hash used as the PreviousEventHash for the first event in a patient's chain.
    /// SHA-256 of "GENESIS" encoded as Base64.
    /// </summary>
    const string GenesisHash = "uLHcHSVOgHQgXEomvUlhJcQv5JOYaGTyYGaCHdPjlRo=";
}
