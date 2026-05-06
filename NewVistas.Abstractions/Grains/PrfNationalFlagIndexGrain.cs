// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class PrfNationalFlagIndexGrain : Grain, IPrfNationalFlagIndexGrain
{
    private readonly IPersistentState<PrfNationalFlagIndexState> _state;

    public PrfNationalFlagIndexGrain(
        [PersistentState("prfNationalFlagIndexState", "prfNationalFlagIndexStore")]
        IPersistentState<PrfNationalFlagIndexState> state)
    {
        _state = state;
    }

    public Task<List<PrfNationalFlagEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);

    public async Task SeedDefaultsAsync()
    {
        if (_state.State.Entries.Count > 0)
            return; // already seeded

        _state.State.Entries = new List<PrfNationalFlagEntry>
        {
            new()
            {
                FlagId      = "PRF-NAT-1",
                FlagName    = "BEHAVIORAL",
                FlagType    = "NATIONAL",
                Description = "Patient presents risk of violence or aggressive behavior toward staff or other patients. Requires specific care protocols and safety planning.",
                IsActive    = true,
            },
            new()
            {
                FlagId      = "PRF-NAT-2",
                FlagName    = "HIGH RISK FOR SUICIDE",
                FlagType    = "NATIONAL",
                Description = "Patient has been clinically assessed as high risk for suicide. Triggers enhanced safety protocols and mandatory follow-up.",
                IsActive    = true,
            },
            new()
            {
                FlagId      = "PRF-NAT-3",
                FlagName    = "URGENT ADDRESS AS FEMALE",
                FlagType    = "NATIONAL",
                Description = "Patient requests to be addressed and referred to using female pronouns and titles in all clinical settings.",
                IsActive    = true,
            },
            new()
            {
                FlagId      = "PRF-NAT-4",
                FlagName    = "MISSING PATIENT",
                FlagType    = "NATIONAL",
                Description = "Patient has been reported missing from a VA facility. Requires immediate notification of police and clinical staff.",
                IsActive    = true,
            },
        };

        await _state.WriteStateAsync();
    }
}
