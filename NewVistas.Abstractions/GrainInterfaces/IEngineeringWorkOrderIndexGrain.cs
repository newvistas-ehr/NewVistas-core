// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Engineering Work Order Index grain — singleton cross-facility search index.
/// Key: "ENG-WO-IDX".
/// Maintains a lightweight list of all work orders for fast search and reporting.
/// Corresponds to VistA ENWLIS.m workload list functionality.
/// </summary>
public interface IEngineeringWorkOrderIndexGrain : IGrainWithStringKey
{
    /// <summary>
    /// Add or update a work order entry in the index.
    /// Replaces any existing entry with the same WorkOrderId.
    /// </summary>
    Task AddOrUpdateAsync(WorkOrderIndexEntry entry);

    /// <summary>
    /// Search work orders with optional filters.
    /// Results are returned newest-first (by CreatedDate).
    /// </summary>
    /// <param name="facilityId">Filter by facility grain key.</param>
    /// <param name="shop">Filter by engineering shop.</param>
    /// <param name="status">Filter by work order status.</param>
    /// <param name="priority">Filter by priority level.</param>
    /// <param name="workOrderType">Filter by work order type.</param>
    /// <param name="assignedToId">Filter by assigned technician ID.</param>
    /// <param name="fromDate">Include work orders created on or after this date.</param>
    /// <param name="toDate">Include work orders created before this date.</param>
    /// <param name="maxResults">Maximum number of results to return.</param>
    Task<List<WorkOrderIndexEntry>> SearchAsync(
        string? facilityId,
        EngineeringShop? shop,
        WorkOrderStatus? status,
        WorkOrderPriority? priority,
        WorkOrderType? workOrderType,
        string? assignedToId,
        DateTime? fromDate,
        DateTime? toDate,
        int maxResults);

    /// <summary>
    /// Get all open and in-progress work orders (active workload).
    /// </summary>
    Task<List<WorkOrderIndexEntry>> GetActiveAsync(int maxResults);

    /// <summary>
    /// Get all work orders for a specific facility.
    /// </summary>
    Task<List<WorkOrderIndexEntry>> GetByFacilityAsync(string facilityId, int maxResults);
}
