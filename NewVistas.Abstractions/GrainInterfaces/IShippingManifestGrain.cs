// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Lab Shipping Manifest Grain Interface based on VistA LAB SHIPPING MANIFEST
/// file (#62.8). Manages batched specimens shipped to reference laboratories.
///
/// Grain Key: "LA-SHIP:{manifestId}"
///
/// Mirrors VistA routines: LA7SM.m (CLSHIP, SMET, ADDTEST, REMVTST, CANC).
/// </summary>
public interface IShippingManifestGrain : IGrainWithStringKey
{
    /// <summary>
    /// Gets the complete manifest state.
    /// </summary>
    Task<GrainStates.ShippingManifestState> GetManifestAsync();

    /// <summary>
    /// Creates/initializes the manifest.
    /// </summary>
    Task CreateManifestAsync(
        string shippingConfigId,
        string destinationLab,
        string? shippingMethod,
        string? shippingConditions,
        string? containerType,
        string? createdBy);

    /// <summary>
    /// Adds a specimen to the manifest.
    /// Mirrors VistA's ADDTEST^LA7SM.
    /// </summary>
    Task AddSpecimenAsync(GrainStates.ManifestSpecimen specimen);

    /// <summary>
    /// Removes a specimen from the manifest (marks as removed, not deleted).
    /// Mirrors VistA's REMVTST^LA7SM — sets field .08 = 0.
    /// </summary>
    Task RemoveSpecimenAsync(string specimenId);

    /// <summary>
    /// Ships the manifest — transitions to SHIPPED status.
    /// Mirrors VistA's CLSHIP^LA7SM.
    /// </summary>
    Task ShipManifestAsync(string? trackingNumber);

    /// <summary>
    /// Records that the reference lab received the specimens.
    /// </summary>
    Task MarkReceivedAsync();

    /// <summary>
    /// Cancels the manifest.
    /// Mirrors VistA's CANC^LA7SM.
    /// </summary>
    Task CancelManifestAsync();
}

/// <summary>
/// Lab Shipping Configuration Grain Interface based on VistA LAB SHIPPING
/// CONFIGURATION file (#62.9). Defines reference lab destinations and
/// default shipping parameters.
///
/// Grain Key: "LA-SHIPCFG:{configId}"
/// </summary>
public interface IShippingConfigGrain : IGrainWithStringKey
{
    /// <summary>
    /// Gets the shipping configuration.
    /// </summary>
    Task<GrainStates.ShippingConfigState> GetConfigAsync();

    /// <summary>
    /// Updates the shipping configuration.
    /// </summary>
    Task UpdateConfigAsync(
        string labName,
        string? address,
        string? phone,
        string? defaultShippingMethod,
        string? defaultConditions,
        string? defaultContainerType,
        bool isActive,
        List<string> acceptedTests);
}
