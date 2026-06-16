// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Manages a single GEC/MDS functional assessment.
/// Key pattern: "GEC-ASSESS:{guid}".
/// VistA GEC File #25.1 (GERIATRIC EVAL). MDS.m
/// </summary>
public interface IGECAssessmentGrain : IGrainWithStringKey
{
    Task CreateAssessmentAsync(
        string patientId,
        string patientName,
        GECAssessmentType assessmentType,
        DateTime assessmentDate,
        DateTime periodStart,
        DateTime periodEnd,
        GECLevelOfCare levelOfCare,
        string completedBy,
        string completedByTitle);

    Task RecordADLScoresAsync(
        int bedMobility,
        int transfer,
        int walking,
        int dressing,
        int eating,
        int toiletUse,
        int personalHygiene);

    Task RecordCognitiveMoodAsync(int? bimsScore, int? phq9Score);

    Task RecordClinicalIndicatorsAsync(
        bool painPresent,
        string painFrequency,
        int pressureUlcerCount,
        int fallsLast30Days,
        bool nutritionConcern,
        bool behaviorSymptoms);

    Task SetRUGCategoryAsync(GECRUGCategory rugCategory);
    Task AddNotesAsync(string notes);
    Task SubmitAssessmentAsync(string submittedBy);
    Task<GECAssessmentState> GetAssessmentAsync();
}
