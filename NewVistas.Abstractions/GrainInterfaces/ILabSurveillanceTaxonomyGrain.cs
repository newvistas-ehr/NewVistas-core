// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Lab Surveillance Taxonomy Grain — RPMS Activity Taxonomy (File #9999999.05).
/// Key: "LAB-SURV-TAX:{taxonomyId}"
///
/// Groups trigger codes by reportable condition for efficient screening.
/// Example taxonomies: "Chlamydia Tests", "Rapid Flu LOINC", "TB Culture".
/// </summary>
public interface ILabSurveillanceTaxonomyGrain : IGrainWithStringKey
{
    Task<GrainStates.LabSurveillanceTaxonomyState> GetAsync();

    Task SaveAsync(
        string taxonomyName,
        string conditionName,
        string? conditionCode,
        string category,
        List<string>? jurisdictions,
        string reportingTimeframe,
        bool isActive);

    /// <summary>Adds a code to this taxonomy.</summary>
    Task AddCodeAsync(GrainStates.LabSurveillanceTaxonomyCode code);

    /// <summary>Removes a code from this taxonomy.</summary>
    Task RemoveCodeAsync(string code, string codeSystem);

    /// <summary>Activates or deactivates the taxonomy.</summary>
    Task SetActiveAsync(bool isActive);
}
