// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.EventSourcing;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Represents the state of a Consult grain based on VistA REQUEST/CONSULTATION file (#123).
///
/// Inherits the clinical event-sourcing outbox from <see cref="EventSourcedStateBase"/>
/// so that causal events for this consult are persisted atomically with the
/// projection in a single <c>WriteStateAsync</c>.
/// </summary>
[GenerateSerializer]
public class ConsultState : EventSourcedStateBase
{
    [Id(0)]
    public string ConsultId { get; set; } = string.Empty;

    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>
    /// To Service (.01) � pointer to SERVICE/SECTION file (#49)
    /// </summary>
    [Id(2)]
    public string ToService { get; set; } = string.Empty;

    [Id(3)]
    public string? ToServiceId { get; set; }

    /// <summary>
    /// From Service
    /// </summary>
    [Id(4)]
    public string? FromService { get; set; }

    [Id(5)]
    public string? FromServiceId { get; set; }

    /// <summary>
    /// Status � PENDING, ACTIVE, SCHEDULED, COMPLETE, CANCELLED, DISCONTINUED
    /// </summary>
    [Id(6)]
    public string Status { get; set; } = "PENDING";

    /// <summary>
    /// Urgency � ROUTINE, URGENT, STAT
    /// </summary>
    [Id(7)]
    public string Urgency { get; set; } = "ROUTINE";

    [Id(8)]
    public DateTime RequestDateTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Requesting Provider
    /// </summary>
    [Id(9)]
    public string? RequestingProviderId { get; set; }

    [Id(10)]
    public string? RequestingProviderName { get; set; }

    /// <summary>
    /// Attention � provider being requested
    /// </summary>
    [Id(11)]
    public string? AttentionProviderId { get; set; }

    [Id(12)]
    public string? AttentionProviderName { get; set; }

    /// <summary>
    /// Reason for Request
    /// </summary>
    [Id(13)]
    public string? ReasonForRequest { get; set; }

    /// <summary>
    /// Provisional Diagnosis
    /// </summary>
    [Id(14)]
    public string? ProvisionalDiagnosis { get; set; }

    [Id(15)]
    public string? OrderId { get; set; }

    [Id(16)]
    public string? LocationId { get; set; }

    [Id(17)]
    public string? LocationName { get; set; }

    /// <summary>
    /// Completion Date/Time
    /// </summary>
    [Id(18)]
    public DateTime? CompletedDateTime { get; set; }

    /// <summary>
    /// Consult Report � the resulting note/document ID
    /// </summary>
    [Id(19)]
    public string? ResultDocumentId { get; set; }

    [Id(20)]
    public string? Comments { get; set; }

    [Id(21)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Id(22)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamped tracking comments history
    /// </summary>
    [Id(23)]
    public List<ConsultTrackingComment> TrackingComments { get; set; } = new();

    /// <summary>
    /// When consult was accepted
    /// </summary>
    [Id(24)]
    public DateTime? AcceptedDateTime { get; set; }

    [Id(25)]
    public string? AcceptedById { get; set; }

    [Id(26)]
    public string? AcceptedByName { get; set; }

    /// <summary>
    /// When the consult appointment is scheduled for
    /// </summary>
    [Id(27)]
    public DateTime? ScheduledDateTime { get; set; }

    /// <summary>
    /// Linked clinic ID for the scheduled consult
    /// </summary>
    [Id(28)]
    public string? ScheduledClinicId { get; set; }

    [Id(29)]
    public string? ScheduledClinicName { get; set; }

    /// <summary>
    /// Consult type — CONSULT, PROCEDURE, INTERFACILITY, COMMUNITY_CARE
    /// </summary>
    [Id(30)]
    public string? ConsultType { get; set; }

    /// <summary>
    /// Clinical history text
    /// </summary>
    [Id(31)]
    public string? ClinicalHistory { get; set; }

    /// <summary>
    /// Follow-up recommendation text from completing provider
    /// </summary>
    [Id(32)]
    public string? FollowUpRecommendation { get; set; }

    /// <summary>
    /// Cross-facility consult flag
    /// </summary>
    [Id(33)]
    public bool IsInterfacility { get; set; }

    /// <summary>
    /// External facility ID if interfacility
    /// </summary>
    [Id(34)]
    public string? ExternalFacilityId { get; set; }

    [Id(35)]
    public string? ExternalFacilityName { get; set; }

    /// <summary>
    /// The provider actually performing the consult
    /// </summary>
    [Id(36)]
    public string? ConsultingProviderId { get; set; }

    [Id(37)]
    public string? ConsultingProviderName { get; set; }

    /// <summary>
    /// Deep copy. Used at event/state boundaries so that mutating the live
    /// consult on the grain does not retroactively mutate the historical
    /// snapshot stored on a clinical event payload.
    /// </summary>
    public ConsultState Clone() => new()
    {
        ConsultId = ConsultId,
        PatientId = PatientId,
        ToService = ToService,
        ToServiceId = ToServiceId,
        FromService = FromService,
        FromServiceId = FromServiceId,
        Status = Status,
        Urgency = Urgency,
        RequestDateTime = RequestDateTime,
        RequestingProviderId = RequestingProviderId,
        RequestingProviderName = RequestingProviderName,
        AttentionProviderId = AttentionProviderId,
        AttentionProviderName = AttentionProviderName,
        ReasonForRequest = ReasonForRequest,
        ProvisionalDiagnosis = ProvisionalDiagnosis,
        OrderId = OrderId,
        LocationId = LocationId,
        LocationName = LocationName,
        CompletedDateTime = CompletedDateTime,
        ResultDocumentId = ResultDocumentId,
        Comments = Comments,
        CreatedDate = CreatedDate,
        LastModifiedDate = LastModifiedDate,
        TrackingComments = TrackingComments.Select(t => new ConsultTrackingComment
        {
            CommentDateTime = t.CommentDateTime,
            AuthorId = t.AuthorId,
            AuthorName = t.AuthorName,
            CommentText = t.CommentText,
            ActionTaken = t.ActionTaken
        }).ToList(),
        AcceptedDateTime = AcceptedDateTime,
        AcceptedById = AcceptedById,
        AcceptedByName = AcceptedByName,
        ScheduledDateTime = ScheduledDateTime,
        ScheduledClinicId = ScheduledClinicId,
        ScheduledClinicName = ScheduledClinicName,
        ConsultType = ConsultType,
        ClinicalHistory = ClinicalHistory,
        FollowUpRecommendation = FollowUpRecommendation,
        IsInterfacility = IsInterfacility,
        ExternalFacilityId = ExternalFacilityId,
        ExternalFacilityName = ExternalFacilityName,
        ConsultingProviderId = ConsultingProviderId,
        ConsultingProviderName = ConsultingProviderName
    };
}

/// <summary>
/// Tracking comment entry for consult activity log
/// </summary>
[GenerateSerializer]
public class ConsultTrackingComment
{
    [Id(0)]
    public DateTime CommentDateTime { get; set; }

    [Id(1)]
    public string AuthorId { get; set; } = string.Empty;

    [Id(2)]
    public string AuthorName { get; set; } = string.Empty;

    [Id(3)]
    public string CommentText { get; set; } = string.Empty;

    /// <summary>
    /// Action taken — FORWARDED, UPDATED, ACCEPTED, SCHEDULED, etc.
    /// </summary>
    [Id(4)]
    public string? ActionTaken { get; set; }
}
