// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Lab QC tracking grain per instrument/test.
/// Applies Westgard rules for QC evaluation.
/// Grain key: "LAB-QC:{instrumentId}:{loincCode}"
/// </summary>
public interface ILabQcGrain : IGrainWithStringKey
{
    Task<LabQcState> GetAsync();

    Task<LabQcResult> RecordQcRunAsync(
        string qcLevel, string lotNumber, decimal measuredValue,
        decimal expectedMean, decimal standardDeviation,
        string techId, string techName, string? notes);

    Task RecordCorrectiveActionAsync(string qcResultId, string correctiveAction);

    Task UnblockTestingAsync();

    Task<List<LabQcResult>> GetRecentResultsAsync(int count);

    Task<bool> IsTestingAllowedAsync();
}
