// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Concurrency;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Order Check Grain — StatelessWorker that performs clinical order checking.
/// Mirrors VistA order checking (ORCHECK.m / ORWDXC.m):
///   - Drug-drug interaction checking (via IDrugInteractionCheckerGrain)
///   - Drug-allergy checking (cross-references patient allergies)
///   - Duplicate order detection (checks active orders)
///
/// Key: "ORDER-CHECK" (key is ignored by StatelessWorker; use any constant)
///
/// This grain carries no persistent state. It reads patient data from
/// other grains to evaluate the proposed order against the patient's
/// existing medications, allergies, and active orders.
/// </summary>
public interface IOrderCheckGrain : IGrainWithStringKey
{
    /// <summary>
    /// Checks a proposed order against the patient's current clinical state.
    /// Returns a list of warnings/alerts. An empty list means no issues found.
    ///
    /// Checks performed:
    /// 1. Drug-allergy: cross-references orderText against patient's allergen list
    /// 2. Duplicate order: checks for active orders of same type with same orderableItem
    /// 3. Drug-drug interaction: for Pharmacy orders, checks against active medications
    /// </summary>
    Task<List<GrainStates.OrderCheckResult>> CheckOrderAsync(
        string patientId,
        string orderType,
        string orderText,
        string? orderableItemId);
}
