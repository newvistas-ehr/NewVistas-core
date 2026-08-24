// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Bone health / osteoporosis workflow methods. Feature-gated by <c>BONE_HEALTH</c>;
/// when the flag is off, reads return an empty snapshot and writes are rejected with a
/// clear error, so a site that does not want the module sees nothing of it.
///
/// The workflow grain is the only place that knows both the bone-health record and the
/// patient's demographics. That matters here more than usual: whether a DXA is read
/// against the T-score or the Z-score depends on the patient's sex and age, so the
/// snapshot cannot be computed correctly by the bone-health grain alone.
/// </summary>
public partial class PatientWorkflowGrain
{
    internal const string BoneHealthFeature = "BONE_HEALTH";

    private IBoneHealthGrain BoneHealth() =>
        GrainFactory.GetGrain<IBoneHealthGrain>($"BONE:{PatientId}");

    private IBoneHealthIndexGrain BoneHealthIndex() =>
        GrainFactory.GetGrain<IBoneHealthIndexGrain>("BONE-HEALTH-IDX");

    // ── Reads ───────────────────────────────────────────────────────────────

    public async Task<BoneHealthSnapshot> GetBoneHealthSnapshotAsync()
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(BoneHealthFeature);
        if (!enabled)
            return new BoneHealthSnapshot { Icn = PatientId };

        // Sex, date of birth and menopausal status select the diagnostic rule, so they are
        // read here and passed in rather than duplicated onto the bone-health record.
        PatientState patient = await GetPatientGrain().GetPatientAsync();

        return await BoneHealth().GetSnapshotAsync(
            patient.Sex,
            patient.DateOfBirth,
            // Menopausal status is not yet a discrete field on the patient record; passing
            // null makes the classifier fall back to age and say so in its rationale.
            isPostmenopausal: null);
    }

    public async Task<BoneHealthState> GetBoneHealthRecordAsync()
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(BoneHealthFeature);
        if (!enabled)
            return new BoneHealthState { Icn = PatientId };
        return await BoneHealth().GetAsync();
    }

    // ── Writes ──────────────────────────────────────────────────────────────

    public async Task EnrollInBoneHealthAsync(string? primaryDiagnosis, DateTime enrollmentDate)
    {
        await RequireBoneHealthFeatureAsync();
        await BoneHealth().EnrollAsync(primaryDiagnosis, enrollmentDate);
        // On re-enroll the bone grain deliberately KEEPS the original enrollment date, so the
        // index must mirror the date the grain actually kept — not this call's argument —
        // or the two drift apart.
        BoneHealthState enrolled = await BoneHealth().GetAsync();
        await BoneHealthIndex().AddOrUpdateAsync(PatientId, enrolled.EnrollmentDate ?? enrollmentDate);
    }

    public async Task<string> RecordDxaScanAsync(DxaScan scan)
    {
        await RequireBoneHealthFeatureAsync();
        await EnsureBoneHealthEnrolledAsync(scan.ScanDate);
        return await BoneHealth().RecordDxaScanAsync(scan);
    }

    public async Task<string> RecordBoneTurnoverMarkerAsync(BoneTurnoverMarkerResult result)
    {
        await RequireBoneHealthFeatureAsync();
        await EnsureBoneHealthEnrolledAsync(result.CollectedAt);
        return await BoneHealth().RecordTurnoverMarkerAsync(result);
    }

    public async Task<string> RecordBoneFractureAsync(BoneFracture fracture)
    {
        await RequireBoneHealthFeatureAsync();
        await EnsureBoneHealthEnrolledAsync(fracture.FractureDate);
        return await BoneHealth().RecordFractureAsync(fracture);
    }

    public async Task<string> StartOsteoporosisTherapyAsync(OsteoporosisTherapyCourse course)
    {
        await RequireBoneHealthFeatureAsync();
        await EnsureBoneHealthEnrolledAsync(course.StartDate);
        return await BoneHealth().StartTherapyAsync(course);
    }

    public async Task StopOsteoporosisTherapyAsync(
        string courseId, DateTime stopDate, string? stopReason, string? transitionedToAgent)
    {
        await RequireBoneHealthFeatureAsync();
        await BoneHealth().StopTherapyAsync(courseId, stopDate, stopReason, transitionedToAgent);
    }

    public async Task<string> RecordFraxAssessmentAsync(FraxAssessment assessment)
    {
        await RequireBoneHealthFeatureAsync();
        await EnsureBoneHealthEnrolledAsync(assessment.AssessmentDate);
        return await BoneHealth().RecordFraxAssessmentAsync(assessment);
    }

    public async Task<string> RecordBoneSecondaryWorkupAsync(SecondaryCauseWorkup workup)
    {
        await RequireBoneHealthFeatureAsync();
        await EnsureBoneHealthEnrolledAsync(workup.WorkupDate);
        return await BoneHealth().RecordSecondaryWorkupAsync(workup);
    }

    // ── Internals ───────────────────────────────────────────────────────────

    private async Task RequireBoneHealthFeatureAsync()
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(BoneHealthFeature);
        if (!enabled)
            throw new InvalidOperationException(
                "Bone health is not enabled at this site (feature flag BONE_HEALTH).");
    }

    /// <summary>
    /// Opens the record on first write so callers never have to remember to enroll first.
    /// The enrollment date is the date of the observation being recorded, which keeps the
    /// record's start honest when historical data is backfilled.
    /// </summary>
    private async Task EnsureBoneHealthEnrolledAsync(DateTime observationDate)
    {
        BoneHealthState state = await BoneHealth().GetAsync();
        if (state.IsEnrolled) return;

        await BoneHealth().EnrollAsync(null, observationDate);
        await BoneHealthIndex().AddOrUpdateAsync(PatientId, observationDate);
    }
}
