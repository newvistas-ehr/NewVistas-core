// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Instrument Index Grain — singleton catalog of all registered lab instruments.
/// Self-seeds with demo instruments representing common VA lab analyzers.
/// </summary>
public class InstrumentIndexGrain : Grain, IInstrumentIndexGrain
{
    private readonly IPersistentState<InstrumentIndexState> _state;

    public InstrumentIndexGrain(
        [PersistentState("instrumentIndex", "instrumentIndexStore")]
        IPersistentState<InstrumentIndexState> state)
    {
        _state = state;
    }

    public async Task<List<InstrumentEntry>> GetAllInstrumentsAsync()
    {
        if (_state.State.Instruments.Count == 0)
            await SeedDemoInstrumentsAsync();

        return _state.State.Instruments;
    }

    public async Task AddOrUpdateInstrumentAsync(InstrumentEntry entry)
    {
        int idx = _state.State.Instruments.FindIndex(i => i.InstrumentId == entry.InstrumentId);
        if (idx >= 0)
            _state.State.Instruments[idx] = entry;
        else
            _state.State.Instruments.Add(entry);

        await _state.WriteStateAsync();
    }

    public async Task RemoveInstrumentAsync(string instrumentId)
    {
        _state.State.Instruments.RemoveAll(i => i.InstrumentId == instrumentId);
        await _state.WriteStateAsync();
    }

    public Task<List<InstrumentEntry>> SearchInstrumentsAsync(string term)
    {
        string lower = term.ToLowerInvariant();
        List<InstrumentEntry> results = _state.State.Instruments
            .Where(i => i.Name.ToLowerInvariant().Contains(lower)
                     || (i.LabSection != null && i.LabSection.ToLowerInvariant().Contains(lower))
                     || (i.Manufacturer != null && i.Manufacturer.ToLowerInvariant().Contains(lower))
                     || (i.Model != null && i.Model.ToLowerInvariant().Contains(lower)))
            .ToList();
        return Task.FromResult(results);
    }

    public Task<List<InstrumentEntry>> GetActiveInstrumentsAsync()
    {
        List<InstrumentEntry> active = _state.State.Instruments
            .Where(i => i.Status == InstrumentStatus.ACTIVE)
            .ToList();
        return Task.FromResult(active);
    }

    public async Task SeedDemoInstrumentsAsync()
    {
        if (_state.State.Instruments.Count > 0) return;

        _state.State.Instruments.AddRange(new[]
        {
            new InstrumentEntry
            {
                InstrumentId = "LA-INST:CHEM-001", Name = "Beckman AU5800",
                InstrumentType = InstrumentType.UNIVERSAL, Status = InstrumentStatus.ACTIVE,
                LabSection = "CHEMISTRY", Manufacturer = "Beckman Coulter", Model = "AU5800", TcpPort = 5001
            },
            new InstrumentEntry
            {
                InstrumentId = "LA-INST:HEM-001", Name = "Sysmex XN-1000",
                InstrumentType = InstrumentType.UNIVERSAL, Status = InstrumentStatus.ACTIVE,
                LabSection = "HEMATOLOGY", Manufacturer = "Sysmex", Model = "XN-1000", TcpPort = 5002
            },
            new InstrumentEntry
            {
                InstrumentId = "LA-INST:COAG-001", Name = "Stago STA-R Max",
                InstrumentType = InstrumentType.UNIVERSAL, Status = InstrumentStatus.ACTIVE,
                LabSection = "COAGULATION", Manufacturer = "Stago", Model = "STA-R Max", TcpPort = 5003
            },
            new InstrumentEntry
            {
                InstrumentId = "LA-INST:UA-001", Name = "Siemens Clinitek Novus",
                InstrumentType = InstrumentType.UNIVERSAL, Status = InstrumentStatus.ACTIVE,
                LabSection = "URINALYSIS", Manufacturer = "Siemens", Model = "Clinitek Novus", TcpPort = 5004
            },
            new InstrumentEntry
            {
                InstrumentId = "LA-INST:POC-001", Name = "Abbott i-STAT 1",
                InstrumentType = InstrumentType.POINT_OF_CARE, Status = InstrumentStatus.ACTIVE,
                LabSection = "POINT OF CARE", Manufacturer = "Abbott", Model = "i-STAT 1", TcpPort = 5010
            },
            new InstrumentEntry
            {
                InstrumentId = "LA-INST:POC-002", Name = "Nova StatStrip Glucose",
                InstrumentType = InstrumentType.POINT_OF_CARE, Status = InstrumentStatus.ACTIVE,
                LabSection = "POINT OF CARE", Manufacturer = "Nova Biomedical", Model = "StatStrip", TcpPort = 5011
            },
        });

        await _state.WriteStateAsync();
    }
}
