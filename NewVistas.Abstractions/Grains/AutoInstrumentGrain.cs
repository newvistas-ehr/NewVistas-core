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
/// Automated Lab Instrument Grain — VistA AUTO INSTRUMENT file (#62.4).
/// One grain per physical lab analyzer or POC device.
/// </summary>
public class AutoInstrumentGrain : Grain, IAutoInstrumentGrain
{
    private readonly IPersistentState<AutoInstrumentState> _state;

    public AutoInstrumentGrain(
        [PersistentState("autoInstrument", "autoInstrumentStore")]
        IPersistentState<AutoInstrumentState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.InstrumentId))
        {
            _state.State.InstrumentId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }

        return base.OnActivateAsync(cancellationToken);
    }

    public Task<AutoInstrumentState> GetConfigAsync()
        => Task.FromResult(_state.State);

    public async Task UpdateConfigAsync(
        string name,
        InstrumentType instrumentType,
        string? hl7Version,
        string? tcpHost,
        int? tcpPort,
        int? connectionTimeoutSeconds,
        bool autoDownload,
        bool autoRelease,
        string? manufacturer,
        string? model,
        string? serialNumber,
        string? labSection,
        string? location,
        string? sendingApplication,
        string? sendingFacility)
    {
        _state.State.Name = name;
        _state.State.InstrumentType = instrumentType;
        _state.State.HL7Version = hl7Version ?? "2.5.1";
        _state.State.TcpHost = tcpHost ?? string.Empty;
        _state.State.TcpPort = tcpPort ?? 0;
        _state.State.ConnectionTimeoutSeconds = connectionTimeoutSeconds ?? 30;
        _state.State.AutoDownload = autoDownload;
        _state.State.AutoRelease = autoRelease;
        _state.State.Manufacturer = manufacturer;
        _state.State.Model = model;
        _state.State.SerialNumber = serialNumber;
        _state.State.LabSection = labSection;
        _state.State.Location = location;
        _state.State.SendingApplication = sendingApplication;
        _state.State.SendingFacility = sendingFacility;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public async Task EnableAsync()
    {
        _state.State.Status = InstrumentStatus.ACTIVE;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DisableAsync()
    {
        _state.State.Status = InstrumentStatus.INACTIVE;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetMaintenanceModeAsync()
    {
        _state.State.Status = InstrumentStatus.MAINTENANCE;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<List<InstrumentTestMapping>> GetTestMappingsAsync()
        => Task.FromResult(_state.State.TestMappings);

    public async Task AddTestMappingAsync(InstrumentTestMapping mapping)
    {
        int idx = _state.State.TestMappings.FindIndex(
            m => m.ExternalTestCode == mapping.ExternalTestCode);
        if (idx >= 0)
            _state.State.TestMappings[idx] = mapping;
        else
            _state.State.TestMappings.Add(mapping);

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RemoveTestMappingAsync(string externalTestCode)
    {
        _state.State.TestMappings.RemoveAll(m => m.ExternalTestCode == externalTestCode);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<InstrumentTestMapping?> ResolveTestMappingAsync(string externalTestCode)
    {
        InstrumentTestMapping? mapping = _state.State.TestMappings
            .FirstOrDefault(m => m.ExternalTestCode == externalTestCode);
        return Task.FromResult(mapping);
    }

    public async Task RecordMessageReceivedAsync()
    {
        _state.State.TotalMessagesProcessed++;
        _state.State.LastMessageReceivedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordErrorAsync()
    {
        _state.State.TotalErrors++;
        _state.State.Status = InstrumentStatus.ERROR;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordConnectionAsync()
    {
        _state.State.LastConnectionDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
