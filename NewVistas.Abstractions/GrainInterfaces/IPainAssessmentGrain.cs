// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Grain for a single structured pain assessment using a validated tool.
/// Grain key: "NURS-PAIN:{guid}"
/// </summary>
public interface IPainAssessmentGrain : IGrainWithStringKey
{
    Task<PainAssessmentState> GetAsync();

    Task<string> CreateAsync(PainAssessmentState initialState);

    Task SignAsync(string nurseId, string nurseName);

    Task RecordReassessmentAsync(
        int postInterventionScore, int minutesSinceIntervention, bool interventionEffective);
}
