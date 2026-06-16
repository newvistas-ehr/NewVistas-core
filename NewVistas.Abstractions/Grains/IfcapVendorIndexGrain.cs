// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class IfcapVendorIndexGrain : Grain, IIfcapVendorIndexGrain
{
    private readonly IPersistentState<IfcapVendorIndexState> _state;

    public IfcapVendorIndexGrain(
        [PersistentState("ifcapVendorIndexState", "ifcapVendorIndexStore")]
        IPersistentState<IfcapVendorIndexState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateAsync(IfcapVendorIndexEntry entry)
    {
        _state.State.Entries.RemoveAll(e => e.VendorId == entry.VendorId);
        _state.State.Entries.Add(entry);
        await _state.WriteStateAsync();
    }

    public Task<List<IfcapVendorIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);

    public Task<List<IfcapVendorIndexEntry>> GetActiveAsync()
        => Task.FromResult(_state.State.Entries
            .Where(e => e.IsActive)
            .ToList());

    public Task<List<IfcapVendorIndexEntry>> SearchAsync(string text)
    {
        string lower = text.ToLowerInvariant();
        return Task.FromResult(_state.State.Entries
            .Where(e => e.Name.ToLowerInvariant().Contains(lower)
                     || e.VendorNumber.ToLowerInvariant().Contains(lower))
            .ToList());
    }
}
