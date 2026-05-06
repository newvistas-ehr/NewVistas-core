// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Lab EDI Index Grain Interface — singleton index of all reference lab
/// configurations and EDI order tracking.
///
/// Grain Key: "LAB-EDI-INDEX"
///
/// Provides aggregate views: list of reference labs, orders by patient,
/// and dashboard statistics. Self-seeds with 3 demo reference labs.
/// </summary>
public interface ILabEdiIndexGrain : IGrainWithStringKey
{
    /// <summary>
    /// Gets all registered reference laboratory summaries.
    /// </summary>
    Task<List<LabEdiLabSummary>> GetReferenceLabsAsync();

    /// <summary>
    /// Adds or updates a reference lab summary in the index.
    /// </summary>
    Task AddOrUpdateLabAsync(LabEdiLabSummary summary);

    /// <summary>
    /// Gets EDI orders for a specific patient, most recent first.
    /// </summary>
    Task<List<LabEdiOrderSummary>> GetOrdersByPatientAsync(string patientId, int maxResults);

    /// <summary>
    /// Adds or updates an order summary in the patient index.
    /// </summary>
    Task AddOrUpdateOrderAsync(LabEdiOrderSummary summary);

    /// <summary>
    /// Gets aggregate dashboard statistics across all reference labs.
    /// </summary>
    Task<LabEdiDashboard> GetDashboardAsync();

    /// <summary>
    /// Seeds demo reference lab configurations (Quest, LabCorp, ARUP).
    /// </summary>
    Task SeedDemoDataAsync();
}
