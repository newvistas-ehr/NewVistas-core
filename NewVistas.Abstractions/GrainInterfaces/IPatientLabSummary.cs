// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Answers "what are the patient's current labs?" with a single grain read.
/// Push-updated on the write path — must be immediately consistent.
///
/// Grain Key: PatientLabSummary/{patientIcn}
/// </summary>
public interface IPatientLabSummary : IGrainWithStringKey
{
    /// <summary>
    /// Called on the write path when a new lab result is stored.
    /// Updates the current value for this test type and maintains the 3-value trend.
    /// </summary>
    Task RecordNewResult(LabResultEvent labResult);

    /// <summary>
    /// Returns the most recent result for every test type for this patient.
    /// </summary>
    Task<IReadOnlyDictionary<string, LabTestSummaryEntry>> GetCurrentLabs();

    /// <summary>
    /// Returns the most recent result for a specific test type.
    /// </summary>
    Task<LabTestSummaryEntry?> GetCurrentResult(string loincCode);

    /// <summary>
    /// Returns all test types where the most recent result is flagged abnormal.
    /// </summary>
    Task<IReadOnlyList<LabTestSummaryEntry>> GetAbnormalResults();
}
