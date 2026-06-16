// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
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
