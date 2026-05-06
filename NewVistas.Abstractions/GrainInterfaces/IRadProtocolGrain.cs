// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Radiology imaging protocol definition.
/// Grain key: "RAD-PROTOCOL:{protocolId}"
/// </summary>
public interface IRadProtocolGrain : IGrainWithStringKey
{
    Task<RadProtocolState> GetAsync();
    Task CreateAsync(string protocolName, string imagingType, string? bodyPart, string? description, string? parameters);
    Task DeactivateAsync();
}
