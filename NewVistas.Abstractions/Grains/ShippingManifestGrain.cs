// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Lab Shipping Manifest Grain — VistA LAB SHIPPING MANIFEST file (#62.8).
/// Mirrors LA7SM.m routines for specimen batch management.
/// </summary>
public class ShippingManifestGrain : Grain, IShippingManifestGrain
{
    private readonly IPersistentState<ShippingManifestState> _state;

    public ShippingManifestGrain(
        [PersistentState("shippingManifest", "shippingManifestStore")]
        IPersistentState<ShippingManifestState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.ManifestId))
        {
            _state.State.ManifestId = this.GetPrimaryKeyString();
        }

        return base.OnActivateAsync(cancellationToken);
    }

    public Task<ShippingManifestState> GetManifestAsync()
        => Task.FromResult(_state.State);

    public async Task CreateManifestAsync(
        string shippingConfigId, string destinationLab,
        string? shippingMethod, string? shippingConditions,
        string? containerType, string? createdBy)
    {
        _state.State.ShippingConfigId = shippingConfigId;
        _state.State.DestinationLab = destinationLab;
        _state.State.ShippingMethod = shippingMethod;
        _state.State.ShippingConditions = shippingConditions;
        _state.State.ContainerType = containerType;
        _state.State.CreatedBy = createdBy;
        _state.State.Status = ShippingManifestStatus.OPEN;
        _state.State.CreatedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddSpecimenAsync(ManifestSpecimen specimen)
    {
        if (_state.State.Status != ShippingManifestStatus.OPEN)
            throw new InvalidOperationException("Cannot add specimens to a non-open manifest.");

        if (!_state.State.Specimens.Any(s => s.SpecimenId == specimen.SpecimenId))
        {
            _state.State.Specimens.Add(specimen);
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task RemoveSpecimenAsync(string specimenId)
    {
        if (_state.State.Status != ShippingManifestStatus.OPEN)
            throw new InvalidOperationException("Cannot modify a non-open manifest.");

        ManifestSpecimen? spec = _state.State.Specimens
            .FirstOrDefault(s => s.SpecimenId == specimenId);
        if (spec != null)
        {
            spec.IsRemoved = true;
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task ShipManifestAsync(string? trackingNumber)
    {
        if (_state.State.Status != ShippingManifestStatus.OPEN)
            throw new InvalidOperationException("Only open manifests can be shipped.");

        _state.State.Status = ShippingManifestStatus.SHIPPED;
        _state.State.TrackingNumber = trackingNumber;
        _state.State.ShippedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task MarkReceivedAsync()
    {
        if (_state.State.Status != ShippingManifestStatus.SHIPPED)
            throw new InvalidOperationException("Only shipped manifests can be marked as received.");

        _state.State.Status = ShippingManifestStatus.RECEIVED;
        _state.State.ReceivedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CancelManifestAsync()
    {
        _state.State.Status = ShippingManifestStatus.CANCELLED;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}

/// <summary>
/// Lab Shipping Configuration Grain — VistA LAB SHIPPING CONFIGURATION file (#62.9).
/// </summary>
public class ShippingConfigGrain : Grain, IShippingConfigGrain
{
    private readonly IPersistentState<ShippingConfigState> _state;

    public ShippingConfigGrain(
        [PersistentState("shippingConfig", "shippingConfigStore")]
        IPersistentState<ShippingConfigState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.ConfigId))
        {
            _state.State.ConfigId = this.GetPrimaryKeyString();
        }

        return base.OnActivateAsync(cancellationToken);
    }

    public Task<ShippingConfigState> GetConfigAsync()
        => Task.FromResult(_state.State);

    public async Task UpdateConfigAsync(
        string labName, string? address, string? phone,
        string? defaultShippingMethod, string? defaultConditions,
        string? defaultContainerType, bool isActive, List<string> acceptedTests)
    {
        _state.State.LabName = labName;
        _state.State.Address = address;
        _state.State.Phone = phone;
        _state.State.DefaultShippingMethod = defaultShippingMethod;
        _state.State.DefaultConditions = defaultConditions;
        _state.State.DefaultContainerType = defaultContainerType;
        _state.State.IsActive = isActive;
        _state.State.AcceptedTests = acceptedTests;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
