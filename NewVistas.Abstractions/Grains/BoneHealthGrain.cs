// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Helpers;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Implements <see cref="IBoneHealthGrain"/>. The grain holds raw observations only;
/// every derived answer comes from <see cref="BoneDensityClassifier"/> so the snapshot,
/// the UI and any future reporting cannot drift apart.
/// </summary>
public class BoneHealthGrain : Grain, IBoneHealthGrain
{
    private const string KeyPrefix = "BONE:";

    private readonly IPersistentState<BoneHealthState> _state;

    public BoneHealthGrain(
        [PersistentState("boneHealthState", "boneHealthStore")]
        IPersistentState<BoneHealthState> state)
    {
        _state = state;
    }

    public Task<BoneHealthState> GetAsync() => Task.FromResult(_state.State);

    public Task<BoneHealthSnapshot> GetSnapshotAsync(string? sex, DateTime? dateOfBirth, bool? isPostmenopausal) =>
        Task.FromResult(BoneDensityClassifier.BuildSnapshot(
            _state.State, sex, dateOfBirth, isPostmenopausal, DateTime.UtcNow));

    public async Task EnrollAsync(string? primaryDiagnosis, DateTime enrollmentDate)
    {
        EnsureIcn();

        if (!_state.State.IsEnrolled)
        {
            _state.State.IsEnrolled = true;
            _state.State.EnrollmentDate = enrollmentDate;
        }

        if (!string.IsNullOrWhiteSpace(primaryDiagnosis))
            _state.State.PrimaryDiagnosis = primaryDiagnosis;

        await TouchAndSaveAsync();
    }

    public async Task<string> RecordDxaScanAsync(DxaScan scan)
    {
        ArgumentNullException.ThrowIfNull(scan);
        if (scan.Measurements.Count == 0)
            throw new ArgumentException("A DXA scan must carry at least one site measurement.", nameof(scan));

        foreach (DxaSiteMeasurement m in scan.Measurements)
        {
            // A historical scan is often known only through a later note quoting its T-scores —
            // the BMD in g/cm² never leaves the scanner's own report, which may not be in the
            // chart at all. Requiring BMD would make that scan unrecordable and push a real,
            // diagnostic T-score into free text. So a measurement must carry a BMD or a T-score,
            // and BMD is range-checked only when one is actually present: 0 means "not recorded",
            // not "zero bone". BoneDensityClassifier already treats 0 as absent when computing
            // interval change, so this is the validation catching up with the interpretation.
            if (m.BmdGramsPerCm2 == 0 && m.TScore is null)
                throw new ArgumentException(
                    "A DXA site measurement must carry a BMD or a T-score.", nameof(scan));
            if (m.BmdGramsPerCm2 != 0 && (m.BmdGramsPerCm2 < 0 || m.BmdGramsPerCm2 > 3.0m))
                throw new ArgumentOutOfRangeException(nameof(scan), m.BmdGramsPerCm2,
                    "BMD must be greater than 0 and no more than 3.0 g/cm².");
        }

        EnsureIcn();
        if (string.IsNullOrWhiteSpace(scan.ScanId))
            scan.ScanId = Guid.NewGuid().ToString();

        _state.State.DxaScans.Add(scan);
        _state.State.DxaScans.Sort((a, b) => a.ScanDate.CompareTo(b.ScanDate));

        await TouchAndSaveAsync();
        return scan.ScanId;
    }

    public async Task<string> RecordTurnoverMarkerAsync(BoneTurnoverMarkerResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(result), result.Value,
                "Turnover marker value cannot be negative.");
        if (result.MarkerType == BoneTurnoverMarkerType.Unknown)
            throw new ArgumentException("Marker type is required.", nameof(result));

        EnsureIcn();
        if (string.IsNullOrWhiteSpace(result.ResultId))
            result.ResultId = Guid.NewGuid().ToString();

        _state.State.TurnoverMarkers.Add(result);
        _state.State.TurnoverMarkers.Sort((a, b) => a.CollectedAt.CompareTo(b.CollectedAt));

        await TouchAndSaveAsync();
        return result.ResultId;
    }

    public async Task<string> RecordFractureAsync(BoneFracture fracture)
    {
        ArgumentNullException.ThrowIfNull(fracture);
        if (string.IsNullOrWhiteSpace(fracture.Site))
            throw new ArgumentException("Fracture site is required.", nameof(fracture));

        EnsureIcn();
        if (string.IsNullOrWhiteSpace(fracture.FractureId))
            fracture.FractureId = Guid.NewGuid().ToString();

        _state.State.Fractures.Add(fracture);
        _state.State.Fractures.Sort((a, b) => a.FractureDate.CompareTo(b.FractureDate));

        await TouchAndSaveAsync();
        return fracture.FractureId;
    }

    public async Task<string> StartTherapyAsync(OsteoporosisTherapyCourse course)
    {
        ArgumentNullException.ThrowIfNull(course);
        if (string.IsNullOrWhiteSpace(course.AgentName))
            throw new ArgumentException("Agent name is required.", nameof(course));

        EnsureIcn();
        if (string.IsNullOrWhiteSpace(course.CourseId))
            course.CourseId = Guid.NewGuid().ToString();

        // Derive the next dose date for interval-dosed agents so the due/overdue view
        // has something to work from without the caller having to compute it.
        if (course.NextDoseDue is null && course.DosingIntervalDays is > 0)
            course.NextDoseDue = course.StartDate.AddDays(course.DosingIntervalDays.Value);

        _state.State.Therapies.Add(course);
        _state.State.Therapies.Sort((a, b) => a.StartDate.CompareTo(b.StartDate));

        await TouchAndSaveAsync();
        return course.CourseId;
    }

    public async Task StopTherapyAsync(string courseId, DateTime stopDate, string? stopReason, string? transitionedToAgent)
    {
        OsteoporosisTherapyCourse? course = _state.State.Therapies
            .FirstOrDefault(t => string.Equals(t.CourseId, courseId, StringComparison.Ordinal));

        if (course is null)
            throw new ArgumentException($"No therapy course with id '{courseId}'.", nameof(courseId));

        course.StopDate = stopDate;
        course.StopReason = stopReason;
        course.TransitionedToAgent = transitionedToAgent;
        course.NextDoseDue = null;

        await TouchAndSaveAsync();
    }

    public async Task<string> RecordFraxAssessmentAsync(FraxAssessment assessment)
    {
        ArgumentNullException.ThrowIfNull(assessment);

        EnsureIcn();
        if (string.IsNullOrWhiteSpace(assessment.AssessmentId))
            assessment.AssessmentId = Guid.NewGuid().ToString();

        _state.State.FraxAssessments.Add(assessment);
        _state.State.FraxAssessments.Sort((a, b) => a.AssessmentDate.CompareTo(b.AssessmentDate));

        await TouchAndSaveAsync();
        return assessment.AssessmentId;
    }

    public async Task<string> RecordSecondaryWorkupAsync(SecondaryCauseWorkup workup)
    {
        ArgumentNullException.ThrowIfNull(workup);

        EnsureIcn();
        if (string.IsNullOrWhiteSpace(workup.WorkupId))
            workup.WorkupId = Guid.NewGuid().ToString();

        _state.State.SecondaryWorkups.Add(workup);
        _state.State.SecondaryWorkups.Sort((a, b) => a.WorkupDate.CompareTo(b.WorkupDate));

        await TouchAndSaveAsync();
        return workup.WorkupId;
    }

    // ── Internals ───────────────────────────────────────────────────────────

    /// <summary>Populates the ICN from the grain key on first write (key format "BONE:{icn}").</summary>
    private void EnsureIcn()
    {
        if (!string.IsNullOrEmpty(_state.State.Icn)) return;

        string key = this.GetPrimaryKeyString();
        _state.State.Icn = key.StartsWith(KeyPrefix, StringComparison.Ordinal)
            ? key[KeyPrefix.Length..]
            : key;
    }

    private async Task TouchAndSaveAsync()
    {
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}

/// <summary>
/// Implements <see cref="IBoneHealthIndexGrain"/> — the site-wide enrollment index.
/// </summary>
public class BoneHealthIndexGrain : Grain, IBoneHealthIndexGrain
{
    private readonly IPersistentState<BoneHealthIndexState> _state;

    public BoneHealthIndexGrain(
        [PersistentState("boneHealthIndexState", "boneHealthIndexStore")]
        IPersistentState<BoneHealthIndexState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateAsync(string icn, DateTime enrollmentDate)
    {
        if (string.IsNullOrWhiteSpace(icn))
            throw new ArgumentException("icn is required.", nameof(icn));

        _state.State.EnrolledIcns[icn] = enrollmentDate;
        await _state.WriteStateAsync();
    }

    public Task<List<string>> GetEnrolledAsync() =>
        Task.FromResult(_state.State.EnrolledIcns.Keys.ToList());

    public Task<int> GetEnrolledCountAsync() =>
        Task.FromResult(_state.State.EnrolledIcns.Count);
}
