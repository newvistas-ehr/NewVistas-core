// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Pharmacy POS Insurer configuration grain — RPMS ABSP INSURER (File #9002313.4).
/// Key: "POS-INSURER:{insurerId}"
/// </summary>
public interface IPharmacyPosInsurerGrain : IGrainWithStringKey
{
    Task<GrainStates.PharmacyPosInsurerState> GetAsync();

    Task SaveAsync(
        string insurerName, string bin, string pcn, string ncpdpVersion,
        string? pharmacyNcpdpId, string? serviceProviderIdQualifier,
        string? planName, string? helpDeskPhone, bool isActive);

    Task DeactivateAsync();
}
