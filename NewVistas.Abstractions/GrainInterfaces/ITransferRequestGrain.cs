// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Inter-facility transfer request — a consult-style status machine
/// (REQUESTED → ACCEPTED → COMPLETED, or DECLINED/CANCELLED). The grain holds
/// state + transitions; ORCHESTRATION (bed reservation, ADT movements, MPI
/// side-effects, center indexes) lives in PatientWorkflowGrain.InterfacilityTransfer.
/// Grain key: "XFER-{guid}". Store: transferRequestStore.
/// </summary>
public interface ITransferRequestGrain : IGrainWithStringKey
{
    Task<TransferRequestState> GetAsync();

    /// <summary>Idempotent create (a re-issued request on the same key is a no-op).</summary>
    Task CreateAsync(
        string patientId, string? patientName,
        string sendingInstitutionId, string? sendingInstitutionName,
        string? sendingUnitId, string? sendingAdmissionId,
        string? sendingAttendingId, string? sendingAttendingName,
        string receivingInstitutionId, string? receivingInstitutionName,
        string? requestedLevelOfCare, BedType? requestedBedType, BedIsolationType isolationRequired,
        string urgency, string? clinicalSummary, string? reasonForTransfer);

    /// <summary>
    /// REQUESTED → ACCEPTED with the reserved bed. Idempotent when already ACCEPTED
    /// with the same bed; throws from terminal states. The caller must have RESERVED
    /// the bed BEFORE this call (reserve-first — no orphan reservations on a race).
    /// </summary>
    Task AcceptAsync(string unitId, string bedId, string? note);

    /// <summary>Re-reserve path when the accepted bed became unavailable. Stays ACCEPTED.</summary>
    Task ReassignBedAsync(string unitId, string bedId, string? note);

    /// <summary>ACCEPTED → COMPLETED with the movement artifacts. Idempotent when COMPLETED.</summary>
    Task CompleteAsync(DateTime arrivalDateTime, string dischargeAdtId, string admissionAdtId,
        string? receivingAttendingId, string? receivingAttendingName);

    /// <summary>REQUESTED → DECLINED (terminal). Only valid before acceptance.</summary>
    Task DeclineAsync(string reason);

    /// <summary>REQUESTED|ACCEPTED → CANCELLED (terminal). The caller releases any reservation.</summary>
    Task CancelAsync(string? reason);
}
