// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton index grain for Standard Medication Routes (VistA File #51.23).
/// Grain key: "MED-ROUTE-INDEX"
///
/// Self-seeding: on first activation, the grain loads all 55 canonical
/// medication routes from embedded VistA ZWR data. No admin load API is needed;
/// routes behave like an enumeration seeded from VistA's national data.
///
/// Routes are stable — they change only through VA terminology server updates.
/// Once seeded, this grain provides read-only access to the route reference table.
/// </summary>
public interface IMedicationRouteIndexGrain : IGrainWithStringKey
{
    /// <summary>
    /// Returns all 55 standard medication routes.
    /// </summary>
    Task<List<MedicationRoute>> GetAllRoutesAsync();

    /// <summary>
    /// Returns the route matching the given name (case-insensitive), or null.
    /// e.g., GetRouteByNameAsync("ORAL") → MedicationRoute {Ien="22", Name="ORAL", Abbrev="PO"}
    /// </summary>
    Task<MedicationRoute?> GetRouteByNameAsync(string name);

    /// <summary>
    /// Returns routes whose name or abbreviation contains the search text (case-insensitive).
    /// </summary>
    Task<List<MedicationRoute>> SearchAsync(string searchText);

    /// <summary>
    /// Returns true once the grain has been seeded from VistA data.
    /// Should be true immediately after first activation.
    /// </summary>
    Task<bool> IsLoadedAsync();
}
