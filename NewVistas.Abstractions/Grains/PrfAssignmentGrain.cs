// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class PrfAssignmentGrain : Grain, IPrfAssignmentGrain
{
    private readonly IPersistentState<PrfAssignmentState> _state;

    public PrfAssignmentGrain(
        [PersistentState("prfAssignmentState", "prfAssignmentStore")]
        IPersistentState<PrfAssignmentState> state)
    {
        _state = state;
    }

    public Task<PrfAssignmentState> GetAsync()
        => Task.FromResult(_state.State);

    public Task<List<PrfFlagAssignment>> GetActiveFlagsAsync()
        => Task.FromResult(_state.State.Assignments.Where(a => a.IsActive).ToList());

    public async Task AssignFlagAsync(
        string flagId,
        string flagName,
        string flagType,
        bool isNational,
        string assignedByUserId,
        string assignedByUserName,
        string? narrative)
    {
        // Deactivate any existing active assignment for the same flag
        List<PrfFlagAssignment> updated = _state.State.Assignments
            .Select(a => a.FlagId == flagId && a.IsActive
                ? a with { IsActive = false, DeactivatedDate = DateTime.UtcNow, DeactivatedReason = "Replaced by new assignment" }
                : a)
            .ToList();

        updated.Add(new PrfFlagAssignment
        {
            FlagId           = flagId,
            FlagName         = flagName,
            FlagType         = flagType,
            IsNational       = isNational,
            AssignedDate     = DateTime.UtcNow,
            AssignedByUserId = assignedByUserId,
            AssignedByUserName = assignedByUserName,
            Narrative        = narrative,
            IsActive         = true,
        });

        _state.State.Assignments    = updated;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DeactivateFlagAsync(string flagId, string deactivatedReason, string deactivatedByUserId)
    {
        _state.State.Assignments = _state.State.Assignments
            .Select(a => a.FlagId == flagId && a.IsActive
                ? a with
                {
                    IsActive          = false,
                    DeactivatedDate   = DateTime.UtcNow,
                    DeactivatedReason = deactivatedReason,
                }
                : a)
            .ToList();

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordReviewAsync(
        string flagId,
        string reviewedByUserId,
        string reviewedByUserName,
        DateTime reviewDate,
        string? narrative)
    {
        _state.State.Assignments = _state.State.Assignments
            .Select(a => a.FlagId == flagId && a.IsActive
                ? a with
                {
                    ReviewDate         = reviewDate,
                    ReviewedByUserId   = reviewedByUserId,
                    ReviewedByUserName = reviewedByUserName,
                    Narrative          = narrative ?? a.Narrative,
                }
                : a)
            .ToList();

        _state.State.LastReviewDate   = reviewDate;
        _state.State.ReviewDueDate    = reviewDate.AddYears(1);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
