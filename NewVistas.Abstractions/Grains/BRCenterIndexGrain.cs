// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Blind Rehabilitation Center Index Grain — grain key: "BR-CENTER-IDX"
/// </summary>
public class BRCenterIndexGrain : Grain, IBRCenterIndexGrain
{
    private readonly IPersistentState<BRCenterIndexState> _state;

    public BRCenterIndexGrain(
        [PersistentState("brCenterIndexState", "brCenterIndexStore")]
        IPersistentState<BRCenterIndexState> state)
    {
        _state = state;
    }

    public Task<List<BRCenterIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Centers);

    public Task<List<BRCenterIndexEntry>> GetAcceptingAsync()
        => Task.FromResult(_state.State.Centers.Where(c => c.AcceptingPatients).ToList());

    public async Task UpsertAsync(BRCenterIndexEntry entry)
    {
        BRCenterIndexEntry? existing = _state.State.Centers.FirstOrDefault(c => c.CenterId == entry.CenterId);
        if (existing is not null)
            _state.State.Centers.Remove(existing);
        _state.State.Centers.Add(entry);
        await _state.WriteStateAsync();
    }

    public async Task SeedDefaultsAsync()
    {
        if (_state.State.Seeded) return;

        List<BRCenterIndexEntry> defaults = new()
        {
            new() { CenterId = "BR-CTR-PALO-ALTO",   Name = "VA Palo Alto Blind Rehabilitation Center",      City = "Palo Alto",   State = "CA", CenterType = BRCenterType.Comprehensive,   AcceptingPatients = true },
            new() { CenterId = "BR-CTR-WEST-HAVEN",   Name = "VA West Haven Blind Rehabilitation Center",     City = "West Haven",  State = "CT", CenterType = BRCenterType.Comprehensive,   AcceptingPatients = true },
            new() { CenterId = "BR-CTR-HINES",        Name = "Hines VA Blind Rehabilitation Center",          City = "Hines",       State = "IL", CenterType = BRCenterType.Comprehensive,   AcceptingPatients = true },
            new() { CenterId = "BR-CTR-WESTERN-BLIND", Name = "Western Blind Rehabilitation Center (Palo Alto)", City = "Palo Alto",  State = "CA", CenterType = BRCenterType.Comprehensive,   AcceptingPatients = true },
            new() { CenterId = "BR-CTR-BILOXI",       Name = "VA Biloxi Blind Rehabilitation Center",         City = "Biloxi",      State = "MS", CenterType = BRCenterType.Comprehensive,   AcceptingPatients = true },
            new() { CenterId = "BR-CTR-TUCSON",       Name = "Southern Arizona VA Blind Rehabilitation Center",City = "Tucson",     State = "AZ", CenterType = BRCenterType.Comprehensive,   AcceptingPatients = true },
            new() { CenterId = "BR-CTR-ATLANTA-VIST", Name = "Atlanta VA VIST Program",                       City = "Atlanta",     State = "GA", CenterType = BRCenterType.Vist,            AcceptingPatients = true },
            new() { CenterId = "BR-CTR-BOSTON-ALV",   Name = "VA Boston Advanced Low Vision Clinic",          City = "Boston",      State = "MA", CenterType = BRCenterType.AdvancedLowVision,AcceptingPatients = true },
        };

        foreach (BRCenterIndexEntry center in defaults)
        {
            if (!_state.State.Centers.Any(c => c.CenterId == center.CenterId))
                _state.State.Centers.Add(center);
        }

        _state.State.Seeded = true;
        await _state.WriteStateAsync();
    }
}
