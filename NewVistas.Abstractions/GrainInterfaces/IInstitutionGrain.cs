// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Security;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Institution — VistA INSTITUTION file (#4). One grain per facility.
/// Grain key: "INST:{institutionId}". Store: institutionStore.
/// Register/update sync the INSTITUTION-INDEX directory (directory-grain pattern).
/// NOTE: named "Institution" because IFacilityGrain is taken by Engineering (#6914).
/// </summary>
public interface IInstitutionGrain : IGrainWithStringKey
{
    Task<InstitutionState> GetAsync();

    /// <summary>Create the institution. Idempotent — a no-op when the name is already set.</summary>
    [RequiresSecurityKey(SecurityKeys.XUMGR)]
    Task RegisterAsync(string name, InstitutionType type, string? stationNumber,
        string? healthSystemId, string? healthSystemName,
        string? streetAddress, string? city, string? state, string? zip, string? phone,
        IEnumerable<string>? capabilities, IEnumerable<string>? legacyAliases);

    [RequiresSecurityKey(SecurityKeys.XUMGR)]
    Task UpdateAsync(string? name, InstitutionType? type, string? stationNumber,
        string? healthSystemId, string? healthSystemName,
        string? streetAddress, string? city, string? state, string? zip, string? phone);

    [RequiresSecurityKey(SecurityKeys.XUMGR)]
    Task SetActiveAsync(bool isActive);

    [RequiresSecurityKey(SecurityKeys.XUMGR)]
    Task SetCapabilitiesAsync(HashSet<string> capabilities);

    /// <summary>Transfer-center operational switch — bed control may flip it (surge, diversion).</summary>
    [RequiresSecurityKey(SecurityKeys.DG_BED_CONTROL)]
    Task SetAcceptsInboundTransfersAsync(bool accepts);
}
