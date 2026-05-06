// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton index grain for Dose Units (VistA File #51.24).
/// Grain key: "DOSE-UNIT-INDEX"
///
/// Self-seeding: on first activation, the grain loads all 54 canonical
/// dose units from embedded VistA ZWR data. No admin load API is needed;
/// dose units behave like an enumeration seeded from VistA's national data.
///
/// Examples: MILLIGRAM(S)/MG, TABLET(S)/TAB, MILLILITER(S)/ML,
///           MICROGRAM(S)/MCG, CAPSULE(S)/CAP, INHALATION(S)/INH
/// </summary>
public interface IDoseUnitIndexGrain : IGrainWithStringKey
{
    /// <summary>
    /// Returns all 54 standard dose units.
    /// </summary>
    Task<List<DoseUnit>> GetAllUnitsAsync();

    /// <summary>
    /// Returns the dose unit matching the given name (case-insensitive), or null.
    /// e.g., GetUnitByNameAsync("MILLIGRAM(S)") → DoseUnit {Ien="19", Abbrev="MG"}
    /// </summary>
    Task<DoseUnit?> GetUnitByNameAsync(string name);

    /// <summary>
    /// Returns dose units whose name or abbreviation contains the search text
    /// (case-insensitive).
    /// </summary>
    Task<List<DoseUnit>> SearchAsync(string searchText);

    /// <summary>
    /// Returns true once the grain has been seeded from VistA data.
    /// Should be true immediately after first activation.
    /// </summary>
    Task<bool> IsLoadedAsync();
}
