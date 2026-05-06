// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Grain representing a single Stanley-Brown Safety Plan for a patient.
/// Key pattern: "SP-PLAN:{guid}"
/// </summary>
public interface ISafetyPlanGrain : IGrainWithStringKey
{
    Task<SafetyPlanState> GetPlanAsync();

    Task CreatePlanAsync(string planId, string patientId, string patientName, string providerId, string providerName);

    Task UpdateWarningSigns(List<string> signs);

    Task UpdateCopingStrategies(List<string> strategies);

    Task UpdateContacts(
        List<string> distractionContacts,
        List<SupportContact> supportContacts,
        List<ProfessionalContact> professionalContacts,
        List<string> crisisLineNumbers);

    Task UpdateMeansRestriction(List<string> meansRemoved, string notes);

    Task UpdateReasonsForLiving(List<string> reasons);

    Task ReviewPlanAsync(DateTime reviewDate);

    Task ArchivePlanAsync();
}
