// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
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
