// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Vital Grain Interface based on VistA GMRV VITAL MEASUREMENT file (#120.5)
/// </summary>
public interface IVitalGrain : IGrainWithStringKey
{
    Task<GrainStates.VitalState> GetVitalAsync();

    Task RecordVitalAsync(
        string patientId,
        string vitalType,
        string value,
        string? units,
        DateTime dateTimeTaken,
        string? locationId,
        string? locationName,
        string? enteredById,
        string? enteredByName,
        List<string>? qualifiers,
        string? comments);

    Task MarkAbnormalAsync(string abnormalFlag);
    Task MarkEnteredInErrorAsync(string reason);

    // ── GAP 12: Range Validation ──────────────────────────────────────────────

    /// <summary>
    /// Validates the vital value against VistA mGMV_VitalHiLo.pas TGMV_VitalHiLoDefinition ranges
    /// and sets IsAbnormalLow/High, IsCriticalLow/High, IsOutOfRange accordingly.
    /// Ranges per vital type:
    ///   TEMPERATURE: valid 60–120°F, abnormal &lt;86 / &gt;106
    ///   PULSE: valid 0–300, abnormal &lt;20 / &gt;300
    ///   RESPIRATION: valid 0–100, abnormal &lt;2 / &gt;100
    ///   BLOOD PRESSURE SYSTOLIC: valid 0–300, abnormal &lt;40
    ///   BLOOD PRESSURE DIASTOLIC: valid 0–200
    ///   CVP: valid 0–60
    ///   PULSE OXIMETRY (O2SAT): valid 50–100, abnormal &lt;50
    /// </summary>
    Task ValidateRangeAsync();
}
