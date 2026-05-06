// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class CatastrophicDisabilityIndexGrain : Grain, ICatastrophicDisabilityIndexGrain
{
    private readonly IPersistentState<CatastrophicDisabilityIndexState> _state;

    public CatastrophicDisabilityIndexGrain(
        [PersistentState("catastrophicDisabilityIndexState", "catastrophicDisabilityIndexStore")]
        IPersistentState<CatastrophicDisabilityIndexState> state)
    {
        _state = state;
    }

    public Task<List<CatastrophicDisabilityEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);

    public Task<List<CatastrophicDisabilityEntry>> SearchAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Task.FromResult(_state.State.Entries.Where(e => e.IsActive).ToList());

        string lower = text.ToLowerInvariant();
        return Task.FromResult(_state.State.Entries
            .Where(e => e.IsActive &&
                        (e.Description.ToLowerInvariant().Contains(lower) ||
                         e.Code.ToLowerInvariant().Contains(lower)))
            .ToList());
    }

    public async Task SeedDefaultsAsync()
    {
        if (_state.State.Entries.Count > 0)
            return; // already seeded

        _state.State.Entries = new List<CatastrophicDisabilityEntry>
        {
            new() { Code = "TBI",      Description = "Traumatic Brain Injury (TBI) — Total Disability", IsActive = true },
            new() { Code = "SCI",      Description = "Spinal Cord Injury (SCI) — Level C4 or above", IsActive = true },
            new() { Code = "SCI-C5",   Description = "Spinal Cord Injury — Level C5 to C8", IsActive = true },
            new() { Code = "SCI-T",    Description = "Spinal Cord Injury — Thoracic Level", IsActive = true },
            new() { Code = "BLIND",    Description = "Blindness — Both Eyes", IsActive = true },
            new() { Code = "BLIND-1",  Description = "Blindness — One Eye with Severe Visual Impairment Other Eye", IsActive = true },
            new() { Code = "VA-BLIND", Description = "Legal Blindness (Visual Acuity 20/200 or Less)", IsActive = true },
            new() { Code = "ALS",      Description = "Amyotrophic Lateral Sclerosis (ALS / Lou Gehrig's Disease)", IsActive = true },
            new() { Code = "AMPD-B",   Description = "Amputation — Both Upper Extremities at or above Elbow", IsActive = true },
            new() { Code = "AMPD-L",   Description = "Amputation — Loss of Use of Both Lower Extremities", IsActive = true },
            new() { Code = "AMPD-H",   Description = "Amputation — Loss of Use of One Hand and One Foot", IsActive = true },
            new() { Code = "PERM-BED", Description = "Permanent Bedridden Requiring Daily Nursing Home Care", IsActive = true },
            new() { Code = "COMA",     Description = "Persistent Vegetative State or Permanent Coma", IsActive = true },
            new() { Code = "MH-100",   Description = "100% Service-Connected Mental Health — Institutional Care", IsActive = true },
            new() { Code = "ORGAN",    Description = "Organ Transplant — Awaiting or Post-Transplant Disability", IsActive = true },
        };

        await _state.WriteStateAsync();
    }
}
