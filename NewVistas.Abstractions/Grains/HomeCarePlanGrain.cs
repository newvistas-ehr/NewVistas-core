// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class HomeCarePlanGrain : Grain, IHomeCarePlanGrain
{
    private readonly IPersistentState<HomeCarePlanState> _state;

    public HomeCarePlanGrain(
        [PersistentState("homeCarePlanState", "homeCarePlanStore")] IPersistentState<HomeCarePlanState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.PlanId))
        {
            _state.State.PlanId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task CreateAsync(string episodeId, string patientId, string establishedById, string establishedByName)
    {
        _state.State.EpisodeId = episodeId;
        _state.State.PatientId = patientId;
        _state.State.EstablishedById = establishedById;
        _state.State.EstablishedByName = establishedByName;
        _state.State.EstablishedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddProblemAsync(CarePlanProblem problem)
    {
        if (string.IsNullOrEmpty(problem.ProblemId))
            problem.ProblemId = Guid.NewGuid().ToString();
        _state.State.Problems.Add(problem);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateProblemAsync(CarePlanProblem problem)
    {
        int idx = _state.State.Problems.FindIndex(p => p.ProblemId == problem.ProblemId);
        if (idx < 0) return;
        _state.State.Problems[idx] = problem;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ResolveProblemAsync(string problemId)
    {
        CarePlanProblem? p = _state.State.Problems.FirstOrDefault(x => x.ProblemId == problemId);
        if (p is null) return;
        p.Status = CarePlanProblemStatus.Resolved;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ReviewAsync(DateTime reviewDate, DateTime? nextReviewDue)
    {
        _state.State.LastReviewDate = reviewDate;
        _state.State.NextReviewDue = nextReviewDue;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<HomeCarePlanState> GetPlanAsync() => Task.FromResult(_state.State);
}
