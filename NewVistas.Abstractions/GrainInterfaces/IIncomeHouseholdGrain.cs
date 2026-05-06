// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Manages household income data for means test determination (VistA File #408.13 INCOME PERSON).
/// Key: <c>"INCOME-HOUSEHOLD:{patientId}"</c>
/// MUMPS references: DGMTU.m, DGMTEE1.m
/// </summary>
public interface IIncomeHouseholdGrain : IGrainWithStringKey
{
    /// <summary>Returns the current household income record.</summary>
    Task<IncomeHouseholdState> GetAsync();

    /// <summary>Sets the reporting year for this income record.</summary>
    Task SetReportingYearAsync(int year);

    /// <summary>
    /// Adds or updates a household member income record.
    /// Returns the person ID (generates a new Guid if PersonId is empty).
    /// Also recalculates TotalHouseholdIncome and TotalNetWorth.
    /// </summary>
    Task<string> AddOrUpdateMemberAsync(IncomePerson member);

    /// <summary>Removes a household member by PersonId.</summary>
    Task RemoveMemberAsync(string personId);

    /// <summary>Records the means test decision and associated threshold.</summary>
    Task RecordMeansTestDecisionAsync(string decision, DateTime decisionDate, decimal? threshold);
}
