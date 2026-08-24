// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// One patient's longitudinal bone-health record. Grain key: <c>"BONE:{icn}"</c>.
///
/// Osteoporosis is a decades-long problem whose data is currently scattered across
/// radiology narratives, lab results and medication history, which makes it impossible
/// to trend. This grain holds the observations in structured form — serial DXA studies,
/// serial bone turnover markers, fractures, therapy courses, fracture-risk assessments
/// and secondary-cause workups — and leaves every derived answer to
/// <c>BoneDensityClassifier</c>, so the diagnostic rules exist in exactly one place.
///
/// The grain deliberately stores <i>only</i> what was observed. Sex, age and menopausal
/// status live on the patient record and are passed in when a snapshot is computed,
/// because they change the diagnostic rule that applies.
/// </summary>
public interface IBoneHealthGrain : IGrainWithStringKey
{
    /// <summary>Returns the raw record.</summary>
    Task<BoneHealthState> GetAsync();

    /// <summary>
    /// Returns the computed view: classified densities, serial change with comparability
    /// caveats, turnover markers with interpretability and trend, active therapy, and the
    /// overall diagnostic category.
    /// </summary>
    /// <param name="sex">Patient sex ("M"/"F") — determines whether T- or Z-score applies.</param>
    /// <param name="dateOfBirth">Used to age the patient at each scan date.</param>
    /// <param name="isPostmenopausal">Menopausal status where known; null falls back to age.</param>
    Task<BoneHealthSnapshot> GetSnapshotAsync(string? sex, DateTime? dateOfBirth, bool? isPostmenopausal);

    /// <summary>Opens a bone-health record for the patient. Idempotent.</summary>
    Task EnrollAsync(string? primaryDiagnosis, DateTime enrollmentDate);

    /// <summary>Records a DXA study. Returns the scan id.</summary>
    Task<string> RecordDxaScanAsync(DxaScan scan);

    /// <summary>Records a bone turnover marker result (CTX, P1NP, …). Returns the result id.</summary>
    Task<string> RecordTurnoverMarkerAsync(BoneTurnoverMarkerResult result);

    /// <summary>Records a fracture. Returns the fracture id.</summary>
    Task<string> RecordFractureAsync(BoneFracture fracture);

    /// <summary>Starts a therapy course. Returns the course id.</summary>
    Task<string> StartTherapyAsync(OsteoporosisTherapyCourse course);

    /// <summary>
    /// Stops a therapy course. <paramref name="transitionedToAgent"/> records the follow-on
    /// agent, which matters because stopping a RANK-ligand inhibitor without transitioning
    /// to an antiresorptive causes rebound bone loss and multiple vertebral fractures.
    /// </summary>
    Task StopTherapyAsync(string courseId, DateTime stopDate, string? stopReason, string? transitionedToAgent);

    /// <summary>Records a FRAX fracture-risk assessment. Returns the assessment id.</summary>
    Task<string> RecordFraxAssessmentAsync(FraxAssessment assessment);

    /// <summary>Records a secondary-cause workup. Returns the workup id.</summary>
    Task<string> RecordSecondaryWorkupAsync(SecondaryCauseWorkup workup);
}

/// <summary>
/// Site-wide index of patients with an open bone-health record. Grain key:
/// <c>"BONE-HEALTH-IDX"</c>. Supports population views ("who is overdue for a DXA")
/// without fanning out across every patient grain.
/// </summary>
public interface IBoneHealthIndexGrain : IGrainWithStringKey
{
    /// <summary>Records that a patient has an open bone-health record.</summary>
    Task AddOrUpdateAsync(string icn, DateTime enrollmentDate);

    /// <summary>Returns all enrolled patient ICNs.</summary>
    Task<List<string>> GetEnrolledAsync();

    /// <summary>Returns the number of enrolled patients.</summary>
    Task<int> GetEnrolledCountAsync();
}
