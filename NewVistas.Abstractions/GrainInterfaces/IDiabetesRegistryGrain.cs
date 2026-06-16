// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient diabetes registry grain. Tracks the disease-specific data
/// points a tribal clinic's diabetes program needs (HbA1c trending, foot
/// exam, eye exam, kidney function) and computes the standard
/// status/due-date views.
///
/// Grain key: <c>"DM-REG:{icn}"</c>. One activation per diabetic patient.
///
/// Maps to the per-patient subset of RPMS BDM (Diabetes Management).
/// Diabetes is the #1 chronic-disease focus at most IHS facilities; this
/// grain provides the operational state behind GPRA diabetes indicators
/// (HbA1c testing, poor control, foot/eye exam, nephropathy screening).
/// </summary>
public interface IDiabetesRegistryGrain : IGrainWithStringKey
{
    /// <summary>Returns the raw registry state.</summary>
    Task<DiabetesRegistryState> GetAsync();

    /// <summary>Returns a computed snapshot with status enums populated.</summary>
    Task<DiabetesRegistrySnapshot> GetSnapshotAsync();

    /// <summary>Generates a pre-visit plan listing what's due/overdue/up-to-date as of <paramref name="visitDate"/>.</summary>
    Task<DiabetesPreVisitPlan> GetPreVisitPlanAsync(DateTime visitDate);

    /// <summary>
    /// Enroll the patient in the diabetes registry. Idempotent: re-enrolling
    /// updates only the diabetes type if supplied.
    /// </summary>
    Task EnrollAsync(string diabetesType, DateTime enrollmentDate);

    /// <summary>Append an HbA1c lab result. History is kept oldest-first.</summary>
    Task RecordHbA1cAsync(decimal value, DateTime dateOfTest);

    /// <summary>Record an annual foot exam. Replaces the previous most-recent exam.</summary>
    Task RecordFootExamAsync(DateTime dateOfExam, string? providerName);

    /// <summary>Record an annual dilated retinal eye exam.</summary>
    Task RecordEyeExamAsync(DateTime dateOfExam, string? providerName);

    /// <summary>Record an eGFR result (kidney function).</summary>
    Task RecordEgfrAsync(decimal eGfrValue, DateTime dateOfTest);

    /// <summary>Record a urine albumin/creatinine ratio result (nephropathy screening).</summary>
    Task RecordAcrAsync(decimal acrValue, DateTime dateOfTest);
}

/// <summary>
/// Singleton index of all diabetic patients in the cluster. Grain key:
/// <c>"DM-REGISTRY-IDX"</c>. Used by population-health workflows and the
/// GPRA diabetes-indicator computation to enumerate the cohort.
/// </summary>
public interface IDiabetesRegistryIndexGrain : IGrainWithStringKey
{
    /// <summary>Records that a patient has been enrolled in the registry.</summary>
    Task AddOrUpdateAsync(string icn, DateTime enrollmentDate);

    /// <summary>Returns all enrolled diabetic ICNs.</summary>
    Task<List<string>> GetEnrolledIcnsAsync();

    /// <summary>Returns the count of enrolled diabetic patients.</summary>
    Task<int> GetCountAsync();
}
