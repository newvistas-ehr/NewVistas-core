// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Manages an AR debtor record — the aggregate financial summary for a single patient
/// across all their AR accounts (VistA File #340 AR DEBTOR).
/// Grain key: "AR-DEBTOR:{patientId}"
/// Managed by RCDP*.m MUMPS routines.
/// </summary>
public interface IARDebtorGrain : IGrainWithStringKey
{
    /// <summary>Returns the current debtor state.</summary>
    Task<ARDebtorState> GetAsync();

    /// <summary>
    /// Ensures a debtor record exists for the given patient, creating it if absent.
    /// Safe to call multiple times (idempotent).
    /// </summary>
    Task EnsureInitializedAsync(string patientId, string name);

    /// <summary>
    /// Recalculates and stores the aggregate balance totals derived from the
    /// patient's AR account index.
    /// </summary>
    Task UpdateBalanceSummaryAsync(decimal totalAmountOwed, decimal totalAmountPaid, decimal currentBalance);

    /// <summary>Marks the debtor account as delinquent effective <paramref name="delinquentSince"/>.</summary>
    Task MarkDelinquentAsync(DateTime delinquentSince);

    /// <summary>Records a referral to a collection agency effective <paramref name="referralDate"/>.</summary>
    Task ReferToCollectionAsync(DateTime referralDate);

    /// <summary>Clears the delinquency and in-collection flags (e.g., after balance paid).</summary>
    Task ClearDelinquencyAsync();
}
