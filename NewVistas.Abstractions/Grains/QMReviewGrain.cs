// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class QMReviewGrain : Grain, IQMReviewGrain
{
    private readonly IPersistentState<QMReviewState> _state;

    public QMReviewGrain(
        [PersistentState("qmReviewState", "qmReviewStore")] IPersistentState<QMReviewState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.ReviewId))
        {
            _state.State.ReviewId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task AssignReviewAsync(
        string incidentId,
        QMReviewType reviewType,
        string assignedTo,
        string reviewerName,
        string reviewerTitle,
        DateTime dueDate,
        bool confidential)
    {
        _state.State.IncidentId = incidentId;
        _state.State.ReviewType = reviewType;
        _state.State.AssignedTo = assignedTo;
        _state.State.ReviewerName = reviewerName;
        _state.State.ReviewerTitle = reviewerTitle;
        _state.State.DueDate = dueDate;
        _state.State.Confidential = confidential;
        _state.State.AssignedDate = DateTime.UtcNow;
        _state.State.Status = QMReviewStatus.Pending;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task StartReviewAsync()
    {
        _state.State.Status = QMReviewStatus.InProgress;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordFindingsAsync(
        string summary,
        ReviewFinding primaryFinding,
        List<string> contributingFactors,
        string rootCause,
        List<string> systemFailures,
        string humanFactors,
        string environmentalFactors)
    {
        _state.State.Summary = summary;
        _state.State.PrimaryFinding = primaryFinding;
        _state.State.ContributingFactors = contributingFactors;
        _state.State.RootCause = rootCause;
        _state.State.SystemFailures = systemFailures;
        _state.State.HumanFactors = humanFactors;
        _state.State.EnvironmentalFactors = environmentalFactors;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddRecommendationAsync(string recommendation)
    {
        if (!_state.State.Recommendations.Contains(recommendation))
        {
            _state.State.Recommendations.Add(recommendation);
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task AddActionItemAsync(string description, string assignedTo, DateTime dueDate)
    {
        _state.State.ActionItems.Add(new QMActionItem
        {
            ActionId = Guid.NewGuid().ToString(),
            Description = description,
            AssignedTo = assignedTo,
            DueDate = dueDate,
            Status = ActionItemStatus.Pending
        });
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CompleteReviewAsync(string finalConclusion, string lessonsLearned)
    {
        _state.State.FinalConclusion = finalConclusion;
        _state.State.LessonsLearned = lessonsLearned;
        _state.State.Status = QMReviewStatus.Completed;
        _state.State.CompletedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ApproveReviewAsync()
    {
        _state.State.Status = QMReviewStatus.Approved;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<QMReviewState> GetReviewAsync() => Task.FromResult(_state.State);
}
