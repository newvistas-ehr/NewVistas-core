// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Order Set Grain — manages a single order set template.
/// Maps to VistA ORDER DIALOG file (#101.41) and QUICK ORDER (#101.44).
/// Key: "ORDSET-{orderSetId}"
/// </summary>
public interface IOrderSetGrain : IGrainWithStringKey
{
    /// <summary>Gets the complete order set state.</summary>
    Task<GrainStates.OrderSetState> GetOrderSetAsync();

    /// <summary>Creates or updates the order set definition.</summary>
    Task UpdateOrderSetAsync(
        string name, string description, string category,
        string serviceSection, string createdBy);

    /// <summary>Adds an order template to the set.</summary>
    Task AddTemplateAsync(GrainStates.OrderTemplate template);

    /// <summary>Removes an order template by ID.</summary>
    Task RemoveTemplateAsync(string templateId);

    /// <summary>Gets all order templates in this set.</summary>
    Task<List<GrainStates.OrderTemplate>> GetTemplatesAsync();

    /// <summary>Enables the order set.</summary>
    Task EnableAsync();

    /// <summary>Disables the order set.</summary>
    Task DisableAsync();
}

/// <summary>
/// Order Set Index Grain — singleton index for searching and listing order sets.
/// Key: "ORDSET-INDEX"
/// Self-seeds demo order sets on first activation.
/// </summary>
public interface IOrderSetIndexGrain : IGrainWithStringKey
{
    /// <summary>Gets all order sets.</summary>
    Task<List<GrainStates.OrderSetEntry>> GetAllOrderSetsAsync();

    /// <summary>Searches order sets by name or category.</summary>
    Task<List<GrainStates.OrderSetEntry>> SearchOrderSetsAsync(string query);

    /// <summary>Registers or updates an order set in the index.</summary>
    Task AddOrUpdateAsync(GrainStates.OrderSetEntry entry);

    /// <summary>Removes an order set from the index.</summary>
    Task RemoveAsync(string orderSetId);
}
