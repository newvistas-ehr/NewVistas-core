// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.EventSourcing;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Represents the state of a Vitals grain based on VistA GMRV VITAL MEASUREMENT file (#120.5).
///
/// Inherits the clinical event-sourcing outbox from <see cref="EventSourcedStateBase"/>
/// so that causal events for this vital measurement are persisted atomically
/// with the projection in a single <c>WriteStateAsync</c>.
/// </summary>
[GenerateSerializer]
public class VitalState : EventSourcedStateBase
{
    [Id(0)]
    public string VitalId { get; set; } = string.Empty;

    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>
    /// Vital Type (.01) � TEMPERATURE, PULSE, RESPIRATION, BLOOD PRESSURE, HEIGHT, WEIGHT, PAIN, PULSE OXIMETRY, CVP, CG
    /// </summary>
    [Id(2)]
    public string VitalType { get; set; } = string.Empty;

    /// <summary>
    /// Rate/Value (.03)
    /// </summary>
    [Id(3)]
    public string Value { get; set; } = string.Empty;

    [Id(4)]
    public string? Units { get; set; }

    /// <summary>
    /// Date/Time Vitals Taken (.01)
    /// </summary>
    [Id(5)]
    public DateTime DateTimeTaken { get; set; }

    /// <summary>
    /// Hospital Location � where measured
    /// </summary>
    [Id(6)]
    public string? LocationId { get; set; }

    [Id(7)]
    public string? LocationName { get; set; }

    /// <summary>
    /// Entered By (.04)
    /// </summary>
    [Id(8)]
    public string? EnteredById { get; set; }

    [Id(9)]
    public string? EnteredByName { get; set; }

    /// <summary>
    /// Qualifier � e.g., STANDING, SITTING, LYING, etc.
    /// </summary>
    [Id(10)]
    public List<string> Qualifiers { get; set; } = new();

    /// <summary>
    /// Abnormal flag � HIGH, LOW, CRITICAL HIGH, CRITICAL LOW
    /// </summary>
    [Id(11)]
    public string? AbnormalFlag { get; set; }

    [Id(12)]
    public string? Comments { get; set; }

    /// <summary>
    /// Entered In Error flag
    /// </summary>
    [Id(13)]
    public bool IsEnteredInError { get; set; }

    [Id(14)]
    public string? EnteredInErrorReason { get; set; }

    [Id(15)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Id(16)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    // ── GAP 12: Vital Range Validation ───────────────────────────────────────

    /// <summary>
    /// True when the recorded value is below the low-normal threshold.
    /// Auto-set on record per VistA mGMV_VitalHiLo.pas TGMV_VitalHiLoDefinition ranges:
    /// TEMPERATURE &lt; 86°F, PULSE &lt; 20, RESPIRATION &lt; 2, SYSTOLIC &lt; 40,
    /// DIASTOLIC &lt; 0, CVP &lt; 0, O2SAT &lt; 50
    /// </summary>
    [Id(17)]
    public bool IsAbnormalLow { get; set; }

    /// <summary>
    /// True when the recorded value exceeds the high-normal threshold.
    /// Auto-set on record per VistA mGMV_VitalHiLo.pas TGMV_VitalHiLoDefinition ranges:
    /// TEMPERATURE &gt; 106°F, PULSE &gt; 300, RESPIRATION &gt; 100, SYSTOLIC &gt; 300,
    /// DIASTOLIC &gt; 200, CVP &gt; 60, O2SAT &gt; 100
    /// </summary>
    [Id(18)]
    public bool IsAbnormalHigh { get; set; }

    /// <summary>
    /// True when the value is critically low (below absolute minimum for life).
    /// </summary>
    [Id(19)]
    public bool IsCriticalLow { get; set; }

    /// <summary>
    /// True when the value is critically high (above absolute maximum for life).
    /// </summary>
    [Id(20)]
    public bool IsCriticalHigh { get; set; }

    /// <summary>
    /// True when the recorded value falls outside the acceptable valid range
    /// and was rejected. Mirrors VistA TUCUMInfo.Validate() from uPCE.pas.
    /// </summary>
    [Id(21)]
    public bool IsOutOfRange { get; set; }

    /// <summary>
    /// Reason the value is out of range (e.g., "Value 999 exceeds maximum 300 for SYSTOLIC")
    /// </summary>
    [Id(22)]
    public string? RangeValidationMessage { get; set; }

    /// <summary>
    /// Deep copy. Used at event/state boundaries so that mutating the live
    /// vital on the grain does not retroactively mutate the historical
    /// snapshot stored on a clinical event payload.
    /// </summary>
    public VitalState Clone() => new()
    {
        VitalId = VitalId,
        PatientId = PatientId,
        VitalType = VitalType,
        Value = Value,
        Units = Units,
        DateTimeTaken = DateTimeTaken,
        LocationId = LocationId,
        LocationName = LocationName,
        EnteredById = EnteredById,
        EnteredByName = EnteredByName,
        Qualifiers = new List<string>(Qualifiers),
        AbnormalFlag = AbnormalFlag,
        Comments = Comments,
        IsEnteredInError = IsEnteredInError,
        EnteredInErrorReason = EnteredInErrorReason,
        CreatedDate = CreatedDate,
        LastModifiedDate = LastModifiedDate,
        IsAbnormalLow = IsAbnormalLow,
        IsAbnormalHigh = IsAbnormalHigh,
        IsCriticalLow = IsCriticalLow,
        IsCriticalHigh = IsCriticalHigh,
        IsOutOfRange = IsOutOfRange,
        RangeValidationMessage = RangeValidationMessage
    };
}
