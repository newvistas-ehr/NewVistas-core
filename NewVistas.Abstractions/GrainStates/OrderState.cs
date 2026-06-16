// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.EventSourcing;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Represents the state of an Order grain based on VistA ORDER file (#100).
///
/// Inherits the clinical event-sourcing outbox from <see cref="EventSourcedStateBase"/>
/// so that causal events for this order are persisted atomically with the
/// projection in a single <c>WriteStateAsync</c>.
/// </summary>

[GenerateSerializer]
public class OrderState : EventSourcedStateBase
{
    /// <summary>
    /// Order Internal Entry Number (IEN)
    /// </summary>
    [Id(0)]
    public string OrderId { get; set; } = string.Empty;

    /// <summary>
    /// Patient IEN - Reference to PATIENT file (#2)
    /// </summary>
    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>
    /// Order Type (e.g., Lab, Radiology, Pharmacy, Consult, Procedure)
    /// </summary>
    [Id(2)]
    public string OrderType { get; set; } = string.Empty;

    /// <summary>
    /// Orderable Item Name
    /// </summary>
    [Id(3)]
    public string OrderableItem { get; set; } = string.Empty;

    /// <summary>
    /// Orderable Item IEN - Reference to ORDERABLE ITEMS file (#101.43)
    /// </summary>
    [Id(4)]
    public string? OrderableItemId { get; set; }

    /// <summary>
    /// Order Status (e.g., Active, Pending, Discontinued, Completed, Expired, Cancelled)
    /// </summary>
    [Id(5)]
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// Start Date/Time
    /// </summary>
    [Id(6)]
    public DateTime StartDateTime { get; set; }

    /// <summary>
    /// Stop Date/Time
    /// </summary>
    [Id(7)]
    public DateTime? StopDateTime { get; set; }

    /// <summary>
    /// Order Date/Time
    /// </summary>
    [Id(8)]
    public DateTime OrderDateTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Provider IEN - Reference to NEW PERSON file (#200)
    /// </summary>
    [Id(9)]
    public string ProviderId { get; set; } = string.Empty;

    /// <summary>
    /// Provider Name
    /// </summary>
    [Id(10)]
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    /// Ordering Location IEN
    /// </summary>
    [Id(11)]
    public string? LocationId { get; set; }

    /// <summary>
    /// Ordering Location Name
    /// </summary>
    [Id(12)]
    public string? LocationName { get; set; }

    /// <summary>
    /// Order Urgency (e.g., ROUTINE, URGENT, STAT)
    /// </summary>
    [Id(13)]
    public string Urgency { get; set; } = "ROUTINE";

    /// <summary>
    /// Order Instructions/Details
    /// </summary>
    [Id(14)]
    public string? Instructions { get; set; }

    /// <summary>
    /// Order Comments
    /// </summary>
    [Id(15)]
    public string? Comments { get; set; }

    /// <summary>
    /// Reason for Order
    /// </summary>
    [Id(16)]
    public string? Reason { get; set; }

    /// <summary>
    /// Nature of Order (e.g., NEW, RENEW, CHANGE)
    /// </summary>
    [Id(17)]
    public string? Nature { get; set; }

    /// <summary>
    /// Discontinue/Cancel Date/Time
    /// </summary>
    [Id(18)]
    public DateTime? DiscontinuedDateTime { get; set; }

    /// <summary>
    /// Discontinue/Cancel Reason
    /// </summary>
    [Id(19)]
    public string? DiscontinuedReason { get; set; }

    /// <summary>
    /// Discontinuing Provider IEN
    /// </summary>
    [Id(20)]
    public string? DiscontinuedByProviderId { get; set; }

    /// <summary>
    /// Completed Date/Time
    /// </summary>
    [Id(21)]
    public DateTime? CompletedDateTime { get; set; }

    /// <summary>
    /// Electronic Signature
    /// </summary>
    [Id(22)]
    public string? ElectronicSignature { get; set; }

    /// <summary>
    /// Signature Date/Time
    /// </summary>
    [Id(23)]
    public DateTime? SignatureDateTime { get; set; }

    /// <summary>
    /// Release Date/Time (when order becomes active)
    /// </summary>
    [Id(24)]
    public DateTime? ReleaseDateTime { get; set; }

    /// <summary>
    /// Flag to indicate if order is flagged
    /// </summary>
    [Id(25)]
    public bool IsFlagged { get; set; }

    /// <summary>
    /// Date Record Created
    /// </summary>
    [Id(26)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date Record Last Modified
    /// </summary>
    [Id(27)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User who created the order
    /// </summary>
    [Id(28)]
    public string? CreatedBy { get; set; }

    /// <summary>
    /// User who last modified the order
    /// </summary>
    [Id(29)]
    public string? ModifiedBy { get; set; }

    // ── Signature Status (GAP 2) ──────────────────────────────────────────────

    /// <summary>
    /// Signature status — mirrors VistA uConst.pas SS_ constants:
    /// SS_ONCHART = "ON CHART", SS_ESIGNED = "E-SIGNED", SS_UNSIGNED = "UNSIGNED",
    /// SS_NOTREQD = "NOT REQUIRED", SS_DIGSIG = "DIGITALLY SIGNED"
    /// </summary>
    [Id(30)]
    public string SignatureStatus { get; set; } = "UNSIGNED";

    /// <summary>
    /// Nurse who verified the order (OA_VERIFY action)
    /// </summary>
    [Id(31)]
    public string? NurseVerifiedBy { get; set; }

    /// <summary>
    /// Date/time of nurse verification
    /// </summary>
    [Id(32)]
    public DateTime? NurseVerifiedDateTime { get; set; }

    /// <summary>
    /// User who performed chart review
    /// </summary>
    [Id(33)]
    public string? ChartReviewedBy { get; set; }

    /// <summary>
    /// Date/time of chart review
    /// </summary>
    [Id(34)]
    public DateTime? ChartReviewedDateTime { get; set; }

    // ── Order Copy / Renew Tracking (GAP 2) ──────────────────────────────────

    /// <summary>
    /// Order ID this was copied from (OA_COPY action — mirrors rOrders.pas)
    /// </summary>
    [Id(35)]
    public string? CopiedFromOrderId { get; set; }

    // ── Park / Unpark (GAP 2) ────────────────────────────────────────────────

    /// <summary>
    /// True when order is parked (OA_PARK) — saved but not released to service
    /// </summary>
    [Id(36)]
    public bool IsParked { get; set; }

    /// <summary>
    /// Date/time order was parked
    /// </summary>
    [Id(37)]
    public DateTime? ParkedDateTime { get; set; }

    // ── Event-Delayed Orders (GAP 11) ────────────────────────────────────────

    /// <summary>
    /// True when this is an event-delayed order (e.g., "on admission", "on transfer")
    /// Mirrors VistA TOrderDelayEvent in rOrders.pas
    /// </summary>
    [Id(38)]
    public bool IsDelayedOrder { get; set; }

    /// <summary>
    /// Delay event type:
    /// A = Admission, T = Transfer, D = Discharge, C = Cancel Event
    /// </summary>
    [Id(39)]
    public string? EventType { get; set; }

    /// <summary>
    /// IEN of the delay event definition (OE/RR LIST file #100.8)
    /// </summary>
    [Id(40)]
    public string? EventIen { get; set; }

    /// <summary>
    /// Human-readable name of the delay event
    /// </summary>
    [Id(41)]
    public string? EventName { get; set; }

    /// <summary>
    /// IEN of the patient-specific event instance
    /// </summary>
    [Id(42)]
    public string? PatientEventIen { get; set; }

    /// <summary>
    /// Effective date/time of the triggering clinical event
    /// </summary>
    [Id(43)]
    public DateTime? EventEffectiveDateTime { get; set; }

    /// <summary>
    /// Date/time the delayed order was released (event occurred)
    /// </summary>
    [Id(44)]
    public DateTime? DelayedOrderReleasedDateTime { get; set; }

    // ── Billing Treatment Factors / CIDC (GAP 7) ────────────────────────────

    /// <summary>
    /// Treatment factor: Service Connected (SC) — from VistA UBACore.pas
    /// </summary>
    [Id(45)]
    public bool TfServiceConnected { get; set; }

    /// <summary>
    /// Treatment factor: Agent Orange (AO)
    /// </summary>
    [Id(46)]
    public bool TfAgentOrange { get; set; }

    /// <summary>
    /// Treatment factor: Ionizing Radiation (IR)
    /// </summary>
    [Id(47)]
    public bool TfIonizingRadiation { get; set; }

    /// <summary>
    /// Treatment factor: Southwest Asia Conditions (SWAC)
    /// </summary>
    [Id(48)]
    public bool TfSouthwestAsia { get; set; }

    /// <summary>
    /// Treatment factor: Military Sexual Trauma (MST)
    /// </summary>
    [Id(49)]
    public bool TfMst { get; set; }

    /// <summary>
    /// Treatment factor: Head and/or Neck Cancer (HNC)
    /// </summary>
    [Id(50)]
    public bool TfHeadNeckCancer { get; set; }

    /// <summary>
    /// Treatment factor: Combat Veteran (CV)
    /// </summary>
    [Id(51)]
    public bool TfCombatVeteran { get; set; }

    /// <summary>
    /// Treatment factor: Shipboard Hazard and Defense (SHAD)
    /// </summary>
    [Id(52)]
    public bool TfShad { get; set; }

    /// <summary>
    /// Treatment factor: Camp Lejeune (CL)
    /// </summary>
    [Id(53)]
    public bool TfCampLejeune { get; set; }

    /// <summary>
    /// ICD-10 diagnosis codes associated with this order for CIDC billing
    /// </summary>
    [Id(54)]
    public List<string> AssociatedDiagnosisCodes { get; set; } = new();

    // ── Controlled Substance (GAP 3) ─────────────────────────────────────────

    /// <summary>
    /// True when the orderable item is a controlled substance
    /// Mirrors VistA rOrders.pas TOrder.IsControlledSubstance
    /// </summary>
    [Id(55)]
    public bool IsControlledSubstance { get; set; }

    /// <summary>
    /// True when this is a detox/maintenance order for controlled substance
    /// Mirrors VistA rOrders.pas TOrder.IsDetox
    /// </summary>
    [Id(56)]
    public bool IsDetox { get; set; }

    /// <summary>
    /// Deep copy. Used at event/state boundaries so that mutating the live
    /// order on the grain does not retroactively mutate the historical
    /// snapshot stored on a clinical event payload (which would break the
    /// hash chain).
    /// </summary>
    public OrderState Clone() => new()
    {
        OrderId = OrderId,
        PatientId = PatientId,
        OrderType = OrderType,
        OrderableItem = OrderableItem,
        OrderableItemId = OrderableItemId,
        Status = Status,
        StartDateTime = StartDateTime,
        StopDateTime = StopDateTime,
        OrderDateTime = OrderDateTime,
        ProviderId = ProviderId,
        ProviderName = ProviderName,
        LocationId = LocationId,
        LocationName = LocationName,
        Urgency = Urgency,
        Instructions = Instructions,
        Comments = Comments,
        Reason = Reason,
        Nature = Nature,
        DiscontinuedDateTime = DiscontinuedDateTime,
        DiscontinuedReason = DiscontinuedReason,
        DiscontinuedByProviderId = DiscontinuedByProviderId,
        CompletedDateTime = CompletedDateTime,
        ElectronicSignature = ElectronicSignature,
        SignatureDateTime = SignatureDateTime,
        ReleaseDateTime = ReleaseDateTime,
        IsFlagged = IsFlagged,
        CreatedDate = CreatedDate,
        LastModifiedDate = LastModifiedDate,
        CreatedBy = CreatedBy,
        ModifiedBy = ModifiedBy,
        SignatureStatus = SignatureStatus,
        NurseVerifiedBy = NurseVerifiedBy,
        NurseVerifiedDateTime = NurseVerifiedDateTime,
        ChartReviewedBy = ChartReviewedBy,
        ChartReviewedDateTime = ChartReviewedDateTime,
        CopiedFromOrderId = CopiedFromOrderId,
        IsParked = IsParked,
        ParkedDateTime = ParkedDateTime,
        IsDelayedOrder = IsDelayedOrder,
        EventType = EventType,
        EventIen = EventIen,
        EventName = EventName,
        PatientEventIen = PatientEventIen,
        EventEffectiveDateTime = EventEffectiveDateTime,
        DelayedOrderReleasedDateTime = DelayedOrderReleasedDateTime,
        TfServiceConnected = TfServiceConnected,
        TfAgentOrange = TfAgentOrange,
        TfIonizingRadiation = TfIonizingRadiation,
        TfSouthwestAsia = TfSouthwestAsia,
        TfMst = TfMst,
        TfHeadNeckCancer = TfHeadNeckCancer,
        TfCombatVeteran = TfCombatVeteran,
        TfShad = TfShad,
        TfCampLejeune = TfCampLejeune,
        AssociatedDiagnosisCodes = new List<string>(AssociatedDiagnosisCodes),
        IsControlledSubstance = IsControlledSubstance,
        IsDetox = IsDetox
    };
}
