// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Patient Submission Grain — stores a single patient-submitted health information packet.
/// §170.315(e)(3) — Patient Health Information Capture.
/// </summary>
public class PatientSubmissionGrain : Grain, IPatientSubmissionGrain
{
    private readonly IPersistentState<PatientSubmissionState> _state;

    public PatientSubmissionGrain(
        [PersistentState("patientSubmissionState", "patientSubmissionStore")] IPersistentState<PatientSubmissionState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.SubmissionId))
            _state.State.SubmissionId = this.GetPrimaryKeyString();
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task CreateSubmissionAsync(PatientSubmissionState submission)
    {
        _state.State.SubmissionId = submission.SubmissionId;
        _state.State.PatientId = submission.PatientId;
        _state.State.PatientName = submission.PatientName;
        _state.State.SubmittedDate = submission.SubmittedDate;
        _state.State.Status = "submitted";
        _state.State.Demographics = submission.Demographics;
        _state.State.HealthConcerns = submission.HealthConcerns;
        _state.State.Medications = submission.Medications;
        _state.State.Allergies = submission.Allergies;
        _state.State.SocialHistory = submission.SocialHistory;
        _state.State.FamilyHistory = submission.FamilyHistory;
        _state.State.AdvanceDirective = submission.AdvanceDirective;
        _state.State.HealthGoals = submission.HealthGoals;
        _state.State.PatientNotes = submission.PatientNotes;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public Task<PatientSubmissionState> GetSubmissionAsync() => Task.FromResult(_state.State);

    public async Task MarkUnderReviewAsync(string reviewerId)
    {
        _state.State.Status = "under-review";
        _state.State.ReviewedBy = reviewerId;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CompleteReviewAsync(
        string status,
        string reviewerId,
        string? reviewNotes,
        List<string> acceptedSections,
        List<string> rejectedSections)
    {
        _state.State.Status = status;
        _state.State.ReviewedBy = reviewerId;
        _state.State.ReviewedDate = DateTime.UtcNow;
        _state.State.ReviewNotes = reviewNotes;
        _state.State.AcceptedSections = acceptedSections;
        _state.State.RejectedSections = rejectedSections;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}

/// <summary>
/// Per-patient submission index — tracks all submissions for one patient.
/// </summary>
public class PatientSubmissionIndexGrain : Grain, IPatientSubmissionIndexGrain
{
    private readonly IPersistentState<PatientSubmissionIndexState> _state;

    public PatientSubmissionIndexGrain(
        [PersistentState("patientSubmissionIndexState", "patientSubmissionIndexStore")] IPersistentState<PatientSubmissionIndexState> state)
    {
        _state = state;
    }

    public async Task AddSubmissionAsync(PatientSubmissionSummary summary)
    {
        _state.State.Submissions.RemoveAll(s => s.SubmissionId == summary.SubmissionId);
        _state.State.Submissions.Add(summary);
        await _state.WriteStateAsync();
    }

    public async Task UpdateStatusAsync(string submissionId, string status)
    {
        PatientSubmissionSummary? existing = _state.State.Submissions
            .FirstOrDefault(s => s.SubmissionId == submissionId);
        if (existing != null)
        {
            existing.Status = status;
            await _state.WriteStateAsync();
        }
    }

    public Task<List<PatientSubmissionSummary>> GetAllSubmissionsAsync()
        => Task.FromResult(_state.State.Submissions.OrderByDescending(s => s.SubmittedDate).ToList());

    public Task<List<PatientSubmissionSummary>> GetSubmissionsByStatusAsync(string status)
        => Task.FromResult(_state.State.Submissions
            .Where(s => s.Status.Equals(status, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(s => s.SubmittedDate).ToList());
}

/// <summary>
/// System-wide submission review queue — all pending submissions across patients.
/// </summary>
public class PatientSubmissionQueueGrain : Grain, IPatientSubmissionQueueGrain
{
    private readonly IPersistentState<PatientSubmissionQueueState> _state;

    public PatientSubmissionQueueGrain(
        [PersistentState("patientSubmissionQueueState", "patientSubmissionQueueStore")] IPersistentState<PatientSubmissionQueueState> state)
    {
        _state = state;
    }

    public async Task AddSubmissionAsync(PatientSubmissionSummary summary)
    {
        _state.State.Submissions.RemoveAll(s => s.SubmissionId == summary.SubmissionId);
        _state.State.Submissions.Add(summary);
        await _state.WriteStateAsync();
    }

    public async Task UpdateStatusAsync(string submissionId, string status)
    {
        PatientSubmissionSummary? existing = _state.State.Submissions
            .FirstOrDefault(s => s.SubmissionId == submissionId);
        if (existing != null)
        {
            existing.Status = status;
            await _state.WriteStateAsync();
        }
    }

    public async Task RemoveSubmissionAsync(string submissionId)
    {
        _state.State.Submissions.RemoveAll(s => s.SubmissionId == submissionId);
        await _state.WriteStateAsync();
    }

    public Task<List<PatientSubmissionSummary>> GetPendingSubmissionsAsync()
        => Task.FromResult(_state.State.Submissions
            .Where(s => s.Status == "submitted" || s.Status == "under-review")
            .OrderBy(s => s.SubmittedDate).ToList());

    public Task<List<PatientSubmissionSummary>> GetAllSubmissionsAsync()
        => Task.FromResult(_state.State.Submissions.OrderByDescending(s => s.SubmittedDate).ToList());
}
