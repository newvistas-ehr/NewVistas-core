// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Health Summary Grain — stores a single generated health summary report for a patient.
/// Corresponds to a rendered instance of a HEALTH SUMMARY TYPE template.
/// VistA HEALTH SUMMARY TYPE file (#142) / patient report generation.
/// </summary>
/// <remarks>Grain key: "HS-REPORT:{guid}"</remarks>
public interface IHealthSummaryGrain : IGrainWithStringKey
{
    /// <summary>Get the generated summary report.</summary>
    Task<HealthSummaryState> GetAsync();

    /// <summary>Persist a newly generated summary report.</summary>
    Task SaveAsync(HealthSummaryState report);
}
