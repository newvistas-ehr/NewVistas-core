// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.Events;
using NewVistas.Abstractions.Events.Clinical.Consults;
using NewVistas.Abstractions.EventSourcing;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Security;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Consult Grain implementation based on VistA REQUEST/CONSULTATION file (#123)
/// </summary>
public class ConsultGrain : Grain, IConsultGrain
{
    private readonly IPersistentState<ConsultState> _state;

    public ConsultGrain(
        [PersistentState("consultState", "consultStore")] IPersistentState<ConsultState> state)
    {
        _state = state;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.ConsultId))
        {
            _state.State.ConsultId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        await base.OnActivateAsync(cancellationToken);

        // Drain anything left in the clinical-event outbox from a prior crash.
        if (_state.State.PendingEvents.Count > 0)
            await this.DrainOutboxAsync(_state, GrainFactory);
    }

    private string? CurrentUserId => RequestContext.Get(RequestContextKeys.UserId) as string;
    private string? CurrentUserName => RequestContext.Get(RequestContextKeys.UserName) as string;

    public Task<ConsultState> GetConsultAsync() => Task.FromResult(_state.State);

    public async Task RequestConsultAsync(
        string patientId, string toService, string? toServiceId,
        string? fromService, string? fromServiceId, string urgency,
        string? requestingProviderId, string? requestingProviderName,
        string? attentionProviderId, string? attentionProviderName,
        string? reasonForRequest, string? provisionalDiagnosis,
        string? orderId, string? locationId, string? locationName)
    {
        // Idempotent: re-issued request on the same grain key is a no-op.
        if (!string.IsNullOrEmpty(_state.State.PatientId))
            return;

        _state.State.PatientId = patientId;
        _state.State.ToService = toService;
        _state.State.ToServiceId = toServiceId;
        _state.State.FromService = fromService;
        _state.State.FromServiceId = fromServiceId;
        _state.State.Urgency = urgency;
        _state.State.RequestingProviderId = requestingProviderId;
        _state.State.RequestingProviderName = requestingProviderName;
        _state.State.AttentionProviderId = attentionProviderId;
        _state.State.AttentionProviderName = attentionProviderName;
        _state.State.ReasonForRequest = reasonForRequest;
        _state.State.ProvisionalDiagnosis = provisionalDiagnosis;
        _state.State.OrderId = orderId;
        _state.State.LocationId = locationId;
        _state.State.LocationName = locationName;
        _state.State.RequestDateTime = DateTime.UtcNow;
        _state.State.Status = "PENDING";
        _state.State.LastModifiedDate = DateTime.UtcNow;

        var evt = new ConsultRequestedV1
        {
            EventId = $"CEV-{Guid.NewGuid()}",
            PatientId = patientId,
            OccurredUtc = DateTime.UtcNow,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            ConsultId = _state.State.ConsultId,
            Snapshot = _state.State.Clone()
        };
        _state.State.PendingEvents.Add(EventEnvelope.Wrap(evt));

        await _state.WriteStateAsync();
        await this.DrainOutboxAsync(_state, GrainFactory);
    }

    public async Task AcceptAsync()
    {
        _state.State.Status = "ACTIVE";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ScheduleAsync()
    {
        _state.State.Status = "SCHEDULED";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CompleteAsync(DateTime completedDateTime, string? resultDocumentId)
    {
        if (string.IsNullOrEmpty(_state.State.PatientId)) return;
        if (_state.State.Status == "COMPLETE") return;

        _state.State.Status = "COMPLETE";
        _state.State.CompletedDateTime = completedDateTime;
        _state.State.ResultDocumentId = resultDocumentId;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        var evt = new ConsultCompletedV1
        {
            EventId = $"CEV-{Guid.NewGuid()}",
            PatientId = _state.State.PatientId,
            OccurredUtc = DateTime.UtcNow,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            ConsultId = _state.State.ConsultId,
            CompletedDateTime = completedDateTime,
            ResultDocumentId = resultDocumentId
        };
        _state.State.PendingEvents.Add(EventEnvelope.Wrap(evt));

        await _state.WriteStateAsync();
        await this.DrainOutboxAsync(_state, GrainFactory);
    }

    public async Task CancelAsync(string? comments)
    {
        _state.State.Status = "CANCELLED";
        _state.State.Comments = comments;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DiscontinueAsync(string? comments)
    {
        _state.State.Status = "DISCONTINUED";
        _state.State.Comments = comments;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddTrackingCommentAsync(string authorId, string authorName, string commentText, string? actionTaken)
    {
        _state.State.TrackingComments.Add(new ConsultTrackingComment
        {
            CommentDateTime = DateTime.UtcNow,
            AuthorId = authorId,
            AuthorName = authorName,
            CommentText = commentText,
            ActionTaken = actionTaken
        });
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AcceptWithDetailsAsync(string acceptedById, string acceptedByName)
    {
        await AcceptAsync();
        _state.State.AcceptedDateTime = DateTime.UtcNow;
        _state.State.AcceptedById = acceptedById;
        _state.State.AcceptedByName = acceptedByName;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ScheduleWithDetailsAsync(DateTime scheduledDateTime, string? clinicId, string? clinicName)
    {
        _state.State.Status = "SCHEDULED";
        _state.State.ScheduledDateTime = scheduledDateTime;
        _state.State.ScheduledClinicId = clinicId;
        _state.State.ScheduledClinicName = clinicName;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetConsultTypeAsync(string consultType)
    {
        _state.State.ConsultType = consultType;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetClinicalHistoryAsync(string clinicalHistory)
    {
        _state.State.ClinicalHistory = clinicalHistory;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetFollowUpRecommendationAsync(string recommendation)
    {
        _state.State.FollowUpRecommendation = recommendation;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetConsultingProviderAsync(string providerId, string providerName)
    {
        _state.State.ConsultingProviderId = providerId;
        _state.State.ConsultingProviderName = providerName;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task MarkInterfacilityAsync(string externalFacilityId, string externalFacilityName)
    {
        _state.State.IsInterfacility = true;
        _state.State.ExternalFacilityId = externalFacilityId;
        _state.State.ExternalFacilityName = externalFacilityName;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<List<ConsultTrackingComment>> GetTrackingCommentsAsync() =>
        Task.FromResult(_state.State.TrackingComments);
}
