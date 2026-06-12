// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Services;

/// <summary>
/// An immutable, versioned read snapshot of the patient index held by the
/// silo-local holder. ByPatientId is never mutated after construction —
/// refreshes swap in a whole new snapshot instance.
/// </summary>
public sealed record PatientIndexReadSnapshot(
    long Version,
    IReadOnlyDictionary<string, PatientIndexEntry> ByPatientId);

/// <summary>
/// Silo-level singleton holder for the patient-index read snapshot.
///
/// Populated PULL-THROUGH by PatientSearchGrain activations: on each search
/// the reader validates the held Version against PATIENT-INDEX (one long over
/// the wire) and applies a delta or full snapshot only when behind. Because
/// readers run on whatever silo received the call, every silo self-populates.
/// Sharing one immutable snapshot per silo keeps memory at one index copy per
/// silo regardless of how many worker activations exist.
/// </summary>
public interface IPatientIndexSnapshotService
{
    /// <summary>Current snapshot, or null if none has been pulled on this silo.</summary>
    PatientIndexReadSnapshot? TryGet();

    /// <summary>
    /// Atomically installs a new snapshot. Version-monotonic: an older or
    /// equal version is ignored, so concurrent reader refreshes cannot
    /// regress the holder.
    /// </summary>
    void Swap(PatientIndexReadSnapshot snapshot);
}

/// <summary>
/// Lock-free-read implementation of <see cref="IPatientIndexSnapshotService"/>
/// (volatile reference; short lock on Swap for the monotonic guard).
/// </summary>
public sealed class PatientIndexSnapshotService : IPatientIndexSnapshotService
{
    private readonly object _swapLock = new();
    private volatile PatientIndexReadSnapshot? _snapshot;

    /// <inheritdoc/>
    public PatientIndexReadSnapshot? TryGet() => _snapshot;

    /// <inheritdoc/>
    public void Swap(PatientIndexReadSnapshot snapshot)
    {
        lock (_swapLock)
        {
            if (_snapshot is null || snapshot.Version > _snapshot.Version)
                _snapshot = snapshot;
        }
    }
}
