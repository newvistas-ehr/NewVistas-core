// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Manages the lifecycle and budget accounting of an IFCAP Control Point.
/// A control point is the organizational unit that holds appropriated funds
/// and sponsors VA Form 2237 purchase requests.
/// Grain key: "IFCAP-CP:{controlPointId}"
/// </summary>
public interface IControlPointGrain : IGrainWithStringKey
{
    /// <summary>Returns the current control point state.</summary>
    Task<ControlPointState> GetAsync();

    /// <summary>Creates the control point with its initial allocation.</summary>
    Task CreateAsync(
        string name,
        string facilityId,
        string serviceId,
        int fiscalYear,
        string budgetCode,
        decimal allocatedAmount,
        string officerId,
        string officerName);

    /// <summary>
    /// Adds funds to the control point's AllocatedAmount and RemainingBalance.
    /// Used for supplemental allocations during the fiscal year.
    /// </summary>
    Task AllocateFundsAsync(decimal amount, string authorizedByUserId);

    /// <summary>
    /// Moves funds from RemainingBalance into ObligatedAmount when a purchase
    /// request is approved. Adds the request ID to the RequestIds list.
    /// </summary>
    Task ObligateFundsAsync(decimal amount, string requestId);

    /// <summary>
    /// Moves funds from ObligatedAmount into ExpendedAmount when a purchase
    /// order is issued against an approved request.
    /// </summary>
    Task ExpenditureAsync(decimal amount, string poId);

    /// <summary>Updates the control point's active/inactive/suspended status.</summary>
    Task UpdateStatusAsync(ControlPointStatus status);
}
