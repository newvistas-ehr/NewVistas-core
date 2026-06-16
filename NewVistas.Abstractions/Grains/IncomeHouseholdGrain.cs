// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class IncomeHouseholdGrain : Grain, IIncomeHouseholdGrain
{
    private readonly IPersistentState<IncomeHouseholdState> _state;

    public IncomeHouseholdGrain(
        [PersistentState("incomeHouseholdState", "incomeHouseholdStore")]
        IPersistentState<IncomeHouseholdState> state)
    {
        _state = state;
    }

    public Task<IncomeHouseholdState> GetAsync()
        => Task.FromResult(_state.State);

    public async Task SetReportingYearAsync(int year)
    {
        _state.State.ReportingYear    = year;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task<string> AddOrUpdateMemberAsync(IncomePerson member)
    {
        string personId = string.IsNullOrEmpty(member.PersonId)
            ? Guid.NewGuid().ToString()
            : member.PersonId;

        IncomePerson entry = member with { PersonId = personId };

        int idx = _state.State.HouseholdMembers.FindIndex(m => m.PersonId == personId);
        if (idx >= 0)
            _state.State.HouseholdMembers[idx] = entry;
        else
            _state.State.HouseholdMembers.Add(entry);

        RecalculateTotals();
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        return personId;
    }

    public async Task RemoveMemberAsync(string personId)
    {
        int idx = _state.State.HouseholdMembers.FindIndex(m => m.PersonId == personId);
        if (idx >= 0)
        {
            _state.State.HouseholdMembers.RemoveAt(idx);
            RecalculateTotals();
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task RecordMeansTestDecisionAsync(string decision, DateTime decisionDate, decimal? threshold)
    {
        _state.State.MeansTestDecision     = decision;
        _state.State.MeansTestDecisionDate = decisionDate;
        _state.State.MeansTestDate         = decisionDate;
        _state.State.ThresholdApplied      = threshold;
        _state.State.LastModifiedDate      = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    private void RecalculateTotals()
    {
        _state.State.TotalHouseholdIncome = _state.State.HouseholdMembers
            .Sum(m => m.GrossAnnualIncome ?? 0m);
        _state.State.TotalNetWorth = _state.State.HouseholdMembers
            .Sum(m => m.NetWorth ?? 0m);
    }
}
