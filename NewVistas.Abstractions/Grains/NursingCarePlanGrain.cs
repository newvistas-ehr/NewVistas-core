// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Nursing Care Plan Grain — VistA File #212 (NURSING PATIENT).
/// Manages the complete set of nursing diagnoses, goals, interventions, and evaluations
/// for a single patient.
/// </summary>
public class NursingCarePlanGrain : Grain, INursingCarePlanGrain
{
    private readonly IPersistentState<NursingCarePlanState> _state;

    public NursingCarePlanGrain(
        [PersistentState("nursingCarePlanState", "nursingCarePlanStore")]
        IPersistentState<NursingCarePlanState> state)
    {
        _state = state;
    }

    public Task<NursingCarePlanState> GetAsync()
        => Task.FromResult(_state.State);

    public async Task<string> AddDiagnosisAsync(
        string nursingDiagnosis,
        string? relatedTo,
        string? evidencedBy,
        int? priority,
        string? createdById,
        string? createdByName)
    {
        string problemId = $"NDP-{Guid.NewGuid():N}";

        _state.State.Problems.Add(new NursingCarePlanProblem
        {
            ProblemId       = problemId,
            NursingDiagnosis = nursingDiagnosis,
            RelatedTo       = relatedTo,
            EvidencedBy     = evidencedBy,
            Priority        = priority,
            Status          = NursingCarePlanStatus.Active,
            CreatedDateTime = DateTime.UtcNow,
            CreatedById     = createdById,
            CreatedByName   = createdByName
        });

        _state.State.LastModifiedDate   = DateTime.UtcNow;
        _state.State.LastModifiedById   = createdById;
        _state.State.LastModifiedByName = createdByName;
        await _state.WriteStateAsync();
        return problemId;
    }

    public async Task AddGoalAsync(string problemId, string goalText, DateTime? targetDate)
    {
        NursingCarePlanProblem? problem = FindProblem(problemId);
        if (problem is null) return;

        problem.Goals.Add(new NursingGoal
        {
            GoalId     = $"NGL-{Guid.NewGuid():N}",
            GoalText   = goalText,
            TargetDate = targetDate,
            Status     = NursingGoalStatus.Pending
        });

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddInterventionAsync(
        string problemId,
        string interventionText,
        string? frequency,
        string? createdById,
        string? createdByName)
    {
        NursingCarePlanProblem? problem = FindProblem(problemId);
        if (problem is null) return;

        problem.Interventions.Add(new NursingIntervention
        {
            InterventionId   = $"NIV-{Guid.NewGuid():N}",
            InterventionText = interventionText,
            Frequency        = frequency,
            IsActive         = true,
            CreatedDateTime  = DateTime.UtcNow,
            CreatedById      = createdById,
            CreatedByName    = createdByName
        });

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordOutcomeEvaluationAsync(
        string problemId,
        NursingOutcomeRating rating,
        string evaluatedById,
        string evaluatedByName,
        string? notes)
    {
        NursingCarePlanProblem? problem = FindProblem(problemId);
        if (problem is null) return;

        problem.OutcomeEvaluations.Add(new NursingOutcomeEvaluation
        {
            EvaluationId      = $"NOE-{Guid.NewGuid():N}",
            EvaluationDateTime = DateTime.UtcNow,
            EvaluatedById     = evaluatedById,
            EvaluatedByName   = evaluatedByName,
            OutcomeRating     = rating,
            Notes             = notes,
            ProblemId         = problemId
        });

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateGoalStatusAsync(
        string problemId, string goalId, NursingGoalStatus status)
    {
        NursingCarePlanProblem? problem = FindProblem(problemId);
        NursingGoal? goal = problem?.Goals.FirstOrDefault(g => g.GoalId == goalId);
        if (goal is null) return;

        goal.Status = status;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ResolveDiagnosisAsync(string problemId, string? resolutionNotes)
    {
        NursingCarePlanProblem? problem = FindProblem(problemId);
        if (problem is null) return;

        problem.Status          = NursingCarePlanStatus.Resolved;
        problem.ResolvedDateTime = DateTime.UtcNow;
        problem.ResolutionNotes = resolutionNotes;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DeactivateInterventionAsync(string problemId, string interventionId)
    {
        NursingCarePlanProblem? problem = FindProblem(problemId);
        NursingIntervention? intervention =
            problem?.Interventions.FirstOrDefault(i => i.InterventionId == interventionId);
        if (intervention is null) return;

        intervention.IsActive = false;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private NursingCarePlanProblem? FindProblem(string problemId)
        => _state.State.Problems.FirstOrDefault(p => p.ProblemId == problemId);
}
