// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.EventSourcing;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>Well-known transfer request status strings (consult-style status machine).</summary>
public static class TransferRequestStatus
{
    /// <summary>Submitted by the sending facility; awaiting the receiving facility's decision.</summary>
    public const string Requested = "REQUESTED";
    /// <summary>Accepted by the receiving facility — a specific bed is reserved.</summary>
    public const string Accepted = "ACCEPTED";
    /// <summary>Patient arrived: discharged at the sender, admitted at the receiver.</summary>
    public const string Completed = "COMPLETED";
    /// <summary>Declined by the receiving facility (terminal; only from REQUESTED).</summary>
    public const string Declined = "DECLINED";
    /// <summary>Cancelled by the sender (terminal; releases any reservation).</summary>
    public const string Cancelled = "CANCELLED";
}

/// <summary>One actor-stamped entry in a transfer request's status timeline.</summary>
[GenerateSerializer]
public class TransferStatusEvent
{
    [Id(0)] public string Status { get; set; } = string.Empty;
    [Id(1)] public DateTime Timestamp { get; set; }
    [Id(2)] public string? ActorId { get; set; }
    [Id(3)] public string? ActorName { get; set; }
    [Id(4)] public string? Note { get; set; }
}

/// <summary>
/// Inter-facility transfer/placement request — the transfer-center analog of a
/// consult (request → accept → complete), producing File #405 movements on
/// completion. The receiving facility controls its own beds: ACCEPT reserves a
/// specific bed; COMPLETE occupies it via the normal admission workflow.
/// Grain key: "XFER-{guid}". Inherits the clinical-event outbox.
/// </summary>
[GenerateSerializer]
public class TransferRequestState : EventSourcedStateBase
{
    [Id(0)] public string TransferId { get; set; } = string.Empty;

    /// <summary>ICN-keyed patient id (ADR-001). Empty = not yet created (idempotency sentinel).</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;
    [Id(2)] public string? PatientName { get; set; }

    // ── Sending side ────────────────────────────────────────────────────
    [Id(3)] public string SendingInstitutionId { get; set; } = string.Empty;
    [Id(4)] public string? SendingInstitutionName { get; set; }
    [Id(5)] public string? SendingUnitId { get; set; }

    /// <summary>The patient's current ADT movement at the sender (discharged on completion).</summary>
    [Id(6)] public string? SendingAdmissionId { get; set; }
    [Id(7)] public string? SendingAttendingId { get; set; }
    [Id(8)] public string? SendingAttendingName { get; set; }

    // ── Receiving side ──────────────────────────────────────────────────
    [Id(9)] public string ReceivingInstitutionId { get; set; } = string.Empty;
    [Id(10)] public string? ReceivingInstitutionName { get; set; }

    // ── Clinical ask ────────────────────────────────────────────────────
    /// <summary>Requested level of care ("ICU", "TELEMETRY", "MED-SURG", ...).</summary>
    [Id(11)] public string? RequestedLevelOfCare { get; set; }
    [Id(12)] public BedType? RequestedBedType { get; set; }
    [Id(13)] public BedIsolationType IsolationRequired { get; set; }

    /// <summary>EMERGENT | URGENT | ROUTINE.</summary>
    [Id(14)] public string Urgency { get; set; } = "ROUTINE";
    [Id(15)] public string? ClinicalSummary { get; set; }
    [Id(16)] public string? ReasonForTransfer { get; set; }

    // ── Status machine ──────────────────────────────────────────────────
    [Id(17)] public string Status { get; set; } = TransferRequestStatus.Requested;
    [Id(18)] public string? DeclineReason { get; set; }
    [Id(19)] public string? CancelReason { get; set; }
    [Id(20)] public List<TransferStatusEvent> Timeline { get; set; } = new();

    // ── Reservation (set on ACCEPT via the receiving unit grain) ────────
    [Id(21)] public string? ReservedUnitId { get; set; }
    [Id(22)] public string? ReservedBedId { get; set; }

    // ── Completion artifacts ────────────────────────────────────────────
    /// <summary>Sender's File #405 discharge (same movement id as SendingAdmissionId).</summary>
    [Id(23)] public string? DischargeAdtId { get; set; }
    /// <summary>Receiver's new File #405 admission.</summary>
    [Id(24)] public string? AdmissionAdtId { get; set; }
    [Id(25)] public string? ReceivingAttendingId { get; set; }
    [Id(26)] public string? ReceivingAttendingName { get; set; }

    // ── Bookkeeping ─────────────────────────────────────────────────────
    [Id(27)] public DateTime? RequestDateTime { get; set; }
    [Id(28)] public DateTime? AcceptedDateTime { get; set; }
    [Id(29)] public DateTime? CompletedDateTime { get; set; }
    [Id(30)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    [Id(31)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Future cross-cluster hook (federated transfer) — always null in v1.</summary>
    [Id(32)] public string? RemoteClusterId { get; set; }

    /// <summary>Deep copy for event snapshots (outbox payloads must not alias live state).</summary>
    public TransferRequestState Clone() => new()
    {
        TransferId = TransferId,
        PatientId = PatientId,
        PatientName = PatientName,
        SendingInstitutionId = SendingInstitutionId,
        SendingInstitutionName = SendingInstitutionName,
        SendingUnitId = SendingUnitId,
        SendingAdmissionId = SendingAdmissionId,
        SendingAttendingId = SendingAttendingId,
        SendingAttendingName = SendingAttendingName,
        ReceivingInstitutionId = ReceivingInstitutionId,
        ReceivingInstitutionName = ReceivingInstitutionName,
        RequestedLevelOfCare = RequestedLevelOfCare,
        RequestedBedType = RequestedBedType,
        IsolationRequired = IsolationRequired,
        Urgency = Urgency,
        ClinicalSummary = ClinicalSummary,
        ReasonForTransfer = ReasonForTransfer,
        Status = Status,
        DeclineReason = DeclineReason,
        CancelReason = CancelReason,
        Timeline = Timeline.Select(t => new TransferStatusEvent
        {
            Status = t.Status, Timestamp = t.Timestamp, ActorId = t.ActorId, ActorName = t.ActorName, Note = t.Note
        }).ToList(),
        ReservedUnitId = ReservedUnitId,
        ReservedBedId = ReservedBedId,
        DischargeAdtId = DischargeAdtId,
        AdmissionAdtId = AdmissionAdtId,
        ReceivingAttendingId = ReceivingAttendingId,
        ReceivingAttendingName = ReceivingAttendingName,
        RequestDateTime = RequestDateTime,
        AcceptedDateTime = AcceptedDateTime,
        CompletedDateTime = CompletedDateTime,
        CreatedDate = CreatedDate,
        LastModifiedDate = LastModifiedDate,
        RemoteClusterId = RemoteClusterId
    };
}

/// <summary>Compact transfer-center queue row (directory pattern).</summary>
[GenerateSerializer]
public class TransferRequestEntry
{
    [Id(0)] public string TransferId { get; set; } = string.Empty;
    [Id(1)] public string PatientId { get; set; } = string.Empty;
    [Id(2)] public string? PatientName { get; set; }
    [Id(3)] public string SendingInstitutionId { get; set; } = string.Empty;
    [Id(4)] public string? SendingInstitutionName { get; set; }
    [Id(5)] public string ReceivingInstitutionId { get; set; } = string.Empty;
    [Id(6)] public string? ReceivingInstitutionName { get; set; }
    [Id(7)] public string Urgency { get; set; } = "ROUTINE";
    [Id(8)] public string? RequestedLevelOfCare { get; set; }
    [Id(9)] public string Status { get; set; } = TransferRequestStatus.Requested;
    [Id(10)] public string? ReservedBedId { get; set; }
    [Id(11)] public DateTime? RequestDateTime { get; set; }
    [Id(12)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>
/// Per-institution transfer-center queue: every request where this institution is
/// the sender or the receiver. Grain key: "TRANSFER-CENTER:{institutionId}".
/// Written by the workflow layer on every transition (house index pattern).
/// </summary>
[GenerateSerializer]
public class TransferCenterState
{
    [Id(0)] public string InstitutionId { get; set; } = string.Empty;
    [Id(1)] public Dictionary<string, TransferRequestEntry> Requests { get; set; } = new();
    [Id(2)] public DateTime LastModifiedDate { get; set; }
}
