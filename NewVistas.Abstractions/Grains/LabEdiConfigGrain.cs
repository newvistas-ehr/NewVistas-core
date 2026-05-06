// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Lab EDI Configuration Grain — stores connection and automation settings
/// for a single external reference laboratory partner.
///
/// Key: "LAB-EDI-CFG:{referenceLabId}"
/// Store: labEdiConfigStore
/// </summary>
public class LabEdiConfigGrain : Grain, ILabEdiConfigGrain
{
    private readonly IPersistentState<LabEdiConfigState> _state;

    public LabEdiConfigGrain(
        [PersistentState("labEdiConfig", "labEdiConfigStore")]
        IPersistentState<LabEdiConfigState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.ReferenceLabId))
        {
            _state.State.ReferenceLabId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }

        return base.OnActivateAsync(cancellationToken);
    }

    public Task<LabEdiConfigState> GetConfigAsync()
        => Task.FromResult(_state.State);

    public async Task ConfigureAsync(
        string labName,
        string cliaNumber,
        string connectionType,
        string? host,
        int? port,
        string? ftpPath,
        string? hl7Version,
        string sendingFacilityId,
        string sendingFacilityName,
        bool autoAcknowledge,
        bool autoFileResults)
    {
        _state.State.LabName = labName;
        _state.State.CliaNumber = cliaNumber;
        _state.State.ConnectionType = connectionType;
        _state.State.Host = host;
        _state.State.Port = port;
        _state.State.FtpPath = ftpPath;
        _state.State.Hl7Version = hl7Version ?? "2.5.1";
        _state.State.SendingFacilityId = sendingFacilityId;
        _state.State.SendingFacilityName = sendingFacilityName;
        _state.State.AutoAcknowledge = autoAcknowledge;
        _state.State.AutoFileResults = autoFileResults;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public async Task ActivateAsync()
    {
        _state.State.IsActive = true;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DeactivateAsync()
    {
        _state.State.IsActive = false;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<bool> IsActiveAsync()
        => Task.FromResult(_state.State.IsActive);

    public async Task UpdateTestMappingAsync(
        string localTestCode,
        string referenceLabTestCode,
        string? loincCode)
    {
        _state.State.TestMappings[localTestCode] = new LabEdiTestMapping
        {
            LocalTestCode = localTestCode,
            ReferenceLabTestCode = referenceLabTestCode,
            LoincCode = loincCode
        };
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }
}
