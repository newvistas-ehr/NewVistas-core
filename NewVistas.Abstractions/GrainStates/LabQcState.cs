// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// QC result status.
/// </summary>
[GenerateSerializer]
public enum QcResultStatus
{
    Pass = 0,
    Fail = 1,
    Warning = 2,
}

/// <summary>
/// A single QC run result.
/// </summary>
[GenerateSerializer]
public record LabQcResult
{
    /// <summary>Unique QC result ID.</summary>
    [Id(0)] public string QcResultId { get; init; } = string.Empty;

    /// <summary>QC material/level (e.g., "Level 1 Normal", "Level 2 Abnormal", "Level 3 Critical").</summary>
    [Id(1)] public string QcLevel { get; init; } = string.Empty;

    /// <summary>Lot number of the QC material.</summary>
    [Id(2)] public string LotNumber { get; init; } = string.Empty;

    /// <summary>Measured value.</summary>
    [Id(3)] public decimal MeasuredValue { get; init; }

    /// <summary>Expected mean value.</summary>
    [Id(4)] public decimal ExpectedMean { get; init; }

    /// <summary>Standard deviation.</summary>
    [Id(5)] public decimal StandardDeviation { get; init; }

    /// <summary>Number of SDs from mean (z-score).</summary>
    [Id(6)] public decimal ZScore { get; init; }

    /// <summary>QC result status.</summary>
    [Id(7)] public QcResultStatus Status { get; init; }

    /// <summary>Westgard rule violation (if any): "1-2s", "1-3s", "2-2s", "R-4s", "4-1s", "10-x".</summary>
    [Id(8)] public string? WestgardViolation { get; init; }

    /// <summary>Date/time of QC run.</summary>
    [Id(9)] public DateTime RunDateTime { get; init; }

    /// <summary>Technologist who ran QC.</summary>
    [Id(10)] public string TechId { get; init; } = string.Empty;

    /// <summary>Technologist name.</summary>
    [Id(11)] public string TechName { get; init; } = string.Empty;

    /// <summary>Corrective action taken (if fail).</summary>
    [Id(12)] public string? CorrectiveAction { get; init; }

    /// <summary>Notes.</summary>
    [Id(13)] public string? Notes { get; init; }
}

/// <summary>
/// State for lab QC tracking per instrument/test combination.
/// Grain key: "LAB-QC:{instrumentId}:{loincCode}"
/// </summary>
[GenerateSerializer]
public class LabQcState
{
    /// <summary>Instrument ID.</summary>
    [Id(0)] public string InstrumentId { get; set; } = string.Empty;

    /// <summary>LOINC code for the test being QC'd.</summary>
    [Id(1)] public string LoincCode { get; set; } = string.Empty;

    /// <summary>Test name.</summary>
    [Id(2)] public string TestName { get; set; } = string.Empty;

    /// <summary>QC results history (newest first).</summary>
    [Id(3)] public List<LabQcResult> Results { get; set; } = new();

    /// <summary>Whether QC is currently passing (last result passed).</summary>
    [Id(4)] public bool IsCurrentlyPassing { get; set; }

    /// <summary>Whether patient testing is blocked due to QC failure.</summary>
    [Id(5)] public bool IsTestingBlocked { get; set; }

    /// <summary>Date of last successful QC.</summary>
    [Id(6)] public DateTime? LastPassingQcDate { get; set; }

    /// <summary>Date of last QC failure.</summary>
    [Id(7)] public DateTime? LastFailureDate { get; set; }

    /// <summary>Consecutive pass count (for trend monitoring).</summary>
    [Id(8)] public int ConsecutivePassCount { get; set; }

    /// <summary>When last modified.</summary>
    [Id(9)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>
/// Delta check result — comparison of current vs previous result.
/// </summary>
[GenerateSerializer]
public record DeltaCheckResult
{
    /// <summary>Lab test ID for the current result.</summary>
    [Id(0)] public string LabTestId { get; init; } = string.Empty;

    /// <summary>LOINC code.</summary>
    [Id(1)] public string LoincCode { get; init; } = string.Empty;

    /// <summary>Test name.</summary>
    [Id(2)] public string TestName { get; init; } = string.Empty;

    /// <summary>Current result value.</summary>
    [Id(3)] public decimal CurrentValue { get; init; }

    /// <summary>Previous result value.</summary>
    [Id(4)] public decimal? PreviousValue { get; init; }

    /// <summary>Absolute difference.</summary>
    [Id(5)] public decimal? AbsoluteDelta { get; init; }

    /// <summary>Percent change from previous.</summary>
    [Id(6)] public decimal? PercentDelta { get; init; }

    /// <summary>Whether the delta check passed.</summary>
    [Id(7)] public bool Passed { get; init; }

    /// <summary>Delta check threshold percent that was applied.</summary>
    [Id(8)] public decimal? ThresholdPercent { get; init; }

    /// <summary>Delta check threshold absolute that was applied.</summary>
    [Id(9)] public decimal? ThresholdAbsolute { get; init; }

    /// <summary>Reason for failure (if failed).</summary>
    [Id(10)] public string? FailureReason { get; init; }

    /// <summary>Date of current result.</summary>
    [Id(11)] public DateTime ResultDate { get; init; }

    /// <summary>Date of previous result.</summary>
    [Id(12)] public DateTime? PreviousResultDate { get; init; }
}
