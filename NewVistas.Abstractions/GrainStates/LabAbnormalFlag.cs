// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Abnormal flag for lab results.
/// Maps to VistA Lab abnormality codes from File 63.
/// </summary>
[GenerateSerializer]
public enum LabAbnormalFlag
{
    Normal,
    Low,
    High,
    Critical,
    CriticalLow,
    CriticalHigh,
    Abnormal
}
