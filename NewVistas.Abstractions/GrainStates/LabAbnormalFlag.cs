// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
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
