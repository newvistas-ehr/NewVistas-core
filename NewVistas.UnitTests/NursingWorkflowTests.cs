// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for VistA Nursing — Files #210-212 (NURSING UNIT / NURSING PATIENT).
/// Tests the individual grains directly via TestCluster (not via the workflow grain).
/// </summary>
[TestFixture]
public class NursingWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private INursingAssessmentGrain NewAssessment()
        => _cluster.GrainFactory.GetGrain<INursingAssessmentGrain>(
            $"NURS-ASSESS:{Guid.NewGuid():N}");

    private INursingAssessmentIndexGrain NewAssessmentIndex()
        => _cluster.GrainFactory.GetGrain<INursingAssessmentIndexGrain>(
            $"NURS-ASSESS-IDX:{Guid.NewGuid():N}");

    private INursingCarePlanGrain NewCarePlan()
        => _cluster.GrainFactory.GetGrain<INursingCarePlanGrain>(
            $"NURS-CAREPLAN:{Guid.NewGuid():N}");

    private INursingAcuityGrain NewAcuity()
        => _cluster.GrainFactory.GetGrain<INursingAcuityGrain>(
            $"NURS-ACUITY:{Guid.NewGuid():N}");

    private INursingUnitGrain NewUnit()
        => _cluster.GrainFactory.GetGrain<INursingUnitGrain>(
            $"NURS-UNIT:{Guid.NewGuid():N}");

    private INursingUnitIndexGrain GetUnitIndex()
        => _cluster.GrainFactory.GetGrain<INursingUnitIndexGrain>("NURS-UNIT-IDX");

    private static NursingAssessmentState MakeAssessmentState(string id, string patientId) =>
        new()
        {
            AssessmentId       = id,
            PatientId          = patientId,
            AssessmentDateTime = DateTime.UtcNow,
            AssessmentType     = "Shift",
            NurseId            = "RN-001",
            NurseName          = "Jane Nurse RN",
            Status             = NursingAssessmentStatus.Draft,
            LevelOfConsciousness = "Alert",
            PainScore          = 4,
            BradenScore        = 16,
            MorseScoreTotal    = 30,
            FallRiskLevel      = "Moderate",
            BreathSounds       = "Clear",
            HeartRhythm        = "Regular",
            SkinIntegrity      = "Intact"
        };

    // ── NursingAssessmentGrain ─────────────────────────────────────────────────

    [Test]
    public async Task NursingAssessmentGrain_Create_SetsAssessmentId()
    {
        // Arrange
        INursingAssessmentGrain grain = NewAssessment();
        string id = Guid.NewGuid().ToString("N");
        NursingAssessmentState state = MakeAssessmentState(id, "PAT-001");

        // Act
        await grain.CreateAsync(state);
        NursingAssessmentState result = await grain.GetAsync();

        // Assert
        Assert.That(result.AssessmentId, Is.EqualTo(id));
        Assert.That(result.PatientId, Is.EqualTo("PAT-001"));
        Assert.That(result.Status, Is.EqualTo(NursingAssessmentStatus.Draft));
        Assert.That(result.PainScore, Is.EqualTo(4));
        Assert.That(result.BradenScore, Is.EqualTo(16));
    }

    [Test]
    public async Task NursingAssessmentGrain_Create_IsIdempotent()
    {
        // Arrange
        INursingAssessmentGrain grain = NewAssessment();
        string id = Guid.NewGuid().ToString("N");
        NursingAssessmentState first = MakeAssessmentState(id, "PAT-002");

        // Act — call Create twice
        await grain.CreateAsync(first);
        NursingAssessmentState overwrite = MakeAssessmentState(id, "DIFFERENT-PATIENT");
        await grain.CreateAsync(overwrite);
        NursingAssessmentState result = await grain.GetAsync();

        // Assert — second call ignored
        Assert.That(result.PatientId, Is.EqualTo("PAT-002"));
    }

    [Test]
    public async Task NursingAssessmentGrain_Sign_SetsStatusAndSignedBy()
    {
        // Arrange
        INursingAssessmentGrain grain = NewAssessment();
        string id = Guid.NewGuid().ToString("N");
        await grain.CreateAsync(MakeAssessmentState(id, "PAT-003"));

        // Act
        await grain.SignAsync("RN-001", "Jane Nurse RN", DateTime.UtcNow);
        NursingAssessmentState result = await grain.GetAsync();

        // Assert
        Assert.That(result.Status, Is.EqualTo(NursingAssessmentStatus.Signed));
        Assert.That(result.SignedById, Is.EqualTo("RN-001"));
        Assert.That(result.SignedByName, Is.EqualTo("Jane Nurse RN"));
        Assert.That(result.SignedDateTime, Is.Not.Null);
    }

    [Test]
    public async Task NursingAssessmentGrain_Amend_SetsStatusAmended()
    {
        // Arrange
        INursingAssessmentGrain grain = NewAssessment();
        string id = Guid.NewGuid().ToString("N");
        await grain.CreateAsync(MakeAssessmentState(id, "PAT-004"));
        await grain.SignAsync("RN-001", "Jane Nurse RN", DateTime.UtcNow);

        // Act
        await grain.AmendAsync("Amended narrative notes.", "RN-001", "Jane Nurse RN");
        NursingAssessmentState result = await grain.GetAsync();

        // Assert
        Assert.That(result.Status, Is.EqualTo(NursingAssessmentStatus.Amended));
        Assert.That(result.NarrativeNotes, Is.EqualTo("Amended narrative notes."));
    }

    // ── NursingAssessmentIndexGrain ────────────────────────────────────────────

    [Test]
    public async Task NursingAssessmentIndexGrain_AddEntry_AppearsInList()
    {
        // Arrange
        INursingAssessmentIndexGrain index = NewAssessmentIndex();
        NursingAssessmentIndexEntry entry = new()
        {
            AssessmentId       = "ASSESS-001",
            AssessmentDateTime = DateTime.UtcNow,
            AssessmentType     = "Initial",
            NurseId            = "RN-001",
            NurseName          = "Jane Nurse RN",
            Status             = NursingAssessmentStatus.Draft,
            PainScore          = 3
        };

        // Act
        await index.AddEntryAsync(entry);
        NursingAssessmentIndexState state = await index.GetAsync();

        // Assert
        Assert.That(state.Assessments, Has.Count.EqualTo(1));
        Assert.That(state.Assessments[0].AssessmentId, Is.EqualTo("ASSESS-001"));
        Assert.That(state.Assessments[0].PainScore, Is.EqualTo(3));
    }

    [Test]
    public async Task NursingAssessmentIndexGrain_UpdateStatus_ChangesToSigned()
    {
        // Arrange
        INursingAssessmentIndexGrain index = NewAssessmentIndex();
        await index.AddEntryAsync(new NursingAssessmentIndexEntry
        {
            AssessmentId = "ASSESS-002",
            AssessmentDateTime = DateTime.UtcNow,
            AssessmentType = "Shift",
            NurseId = "RN-002",
            NurseName = "Bob Nurse RN",
            Status = NursingAssessmentStatus.Draft
        });

        // Act
        await index.UpdateEntryStatusAsync("ASSESS-002", NursingAssessmentStatus.Signed);
        NursingAssessmentIndexState state = await index.GetAsync();

        // Assert
        Assert.That(state.Assessments[0].Status, Is.EqualTo(NursingAssessmentStatus.Signed));
    }

    // ── NursingCarePlanGrain ───────────────────────────────────────────────────

    [Test]
    public async Task NursingCarePlanGrain_AddDiagnosis_ReturnsNdpPrefixedId()
    {
        // Arrange
        INursingCarePlanGrain plan = NewCarePlan();

        // Act
        string problemId = await plan.AddDiagnosisAsync(
            "Acute Pain", "surgical incision", "patient reports 7/10 pain",
            1, "RN-001", "Jane Nurse RN");

        // Assert
        Assert.That(problemId, Does.StartWith("NDP-"));
    }

    [Test]
    public async Task NursingCarePlanGrain_AddDiagnosis_AppearsInProblems()
    {
        // Arrange
        INursingCarePlanGrain plan = NewCarePlan();

        // Act
        await plan.AddDiagnosisAsync("Impaired Physical Mobility", "pain with movement", null, 2, null, null);
        NursingCarePlanState state = await plan.GetAsync();

        // Assert
        Assert.That(state.Problems, Has.Count.EqualTo(1));
        Assert.That(state.Problems[0].NursingDiagnosis, Is.EqualTo("Impaired Physical Mobility"));
        Assert.That(state.Problems[0].Status, Is.EqualTo(NursingCarePlanStatus.Active));
    }

    [Test]
    public async Task NursingCarePlanGrain_AddGoal_AppearsUnderProblem()
    {
        // Arrange
        INursingCarePlanGrain plan = NewCarePlan();
        string pid = await plan.AddDiagnosisAsync("Acute Pain", null, null, 1, null, null);

        // Act
        await plan.AddGoalAsync(pid, "Patient will report pain ≤3/10 by EOD", DateTime.Today.AddDays(1));
        NursingCarePlanState state = await plan.GetAsync();

        // Assert
        NursingCarePlanProblem problem = state.Problems.First(p => p.ProblemId == pid);
        Assert.That(problem.Goals, Has.Count.EqualTo(1));
        Assert.That(problem.Goals[0].GoalText, Does.Contain("pain ≤3/10"));
        Assert.That(problem.Goals[0].GoalId, Does.StartWith("NGL-"));
        Assert.That(problem.Goals[0].Status, Is.EqualTo(NursingGoalStatus.Pending));
    }

    [Test]
    public async Task NursingCarePlanGrain_AddIntervention_AppearsUnderProblem()
    {
        // Arrange
        INursingCarePlanGrain plan = NewCarePlan();
        string pid = await plan.AddDiagnosisAsync("Acute Pain", null, null, 1, "RN-001", "Jane Nurse RN");

        // Act
        await plan.AddInterventionAsync(pid, "Administer scheduled analgesic", "Q4H", "RN-001", "Jane Nurse RN");
        NursingCarePlanState state = await plan.GetAsync();

        // Assert
        NursingCarePlanProblem problem = state.Problems.First(p => p.ProblemId == pid);
        Assert.That(problem.Interventions, Has.Count.EqualTo(1));
        Assert.That(problem.Interventions[0].InterventionId, Does.StartWith("NIV-"));
        Assert.That(problem.Interventions[0].Frequency, Is.EqualTo("Q4H"));
        Assert.That(problem.Interventions[0].IsActive, Is.True);
    }

    [Test]
    public async Task NursingCarePlanGrain_RecordOutcome_AppearsInEvaluations()
    {
        // Arrange
        INursingCarePlanGrain plan = NewCarePlan();
        string pid = await plan.AddDiagnosisAsync("Acute Pain", null, null, null, null, null);

        // Act
        await plan.RecordOutcomeEvaluationAsync(
            pid, NursingOutcomeRating.GoalPartiallyMet, "RN-001", "Jane Nurse RN", "Pain improved to 5/10");
        NursingCarePlanState state = await plan.GetAsync();

        // Assert
        NursingCarePlanProblem problem = state.Problems.First(p => p.ProblemId == pid);
        Assert.That(problem.OutcomeEvaluations, Has.Count.EqualTo(1));
        Assert.That(problem.OutcomeEvaluations[0].OutcomeRating, Is.EqualTo(NursingOutcomeRating.GoalPartiallyMet));
        Assert.That(problem.OutcomeEvaluations[0].EvaluationId, Does.StartWith("NOE-"));
    }

    [Test]
    public async Task NursingCarePlanGrain_ResolveDiagnosis_SetsResolvedStatus()
    {
        // Arrange
        INursingCarePlanGrain plan = NewCarePlan();
        string pid = await plan.AddDiagnosisAsync("Acute Pain", null, null, null, null, null);

        // Act
        await plan.ResolveDiagnosisAsync(pid, "Pain resolved post-operatively.");
        NursingCarePlanState state = await plan.GetAsync();

        // Assert
        NursingCarePlanProblem problem = state.Problems.First(p => p.ProblemId == pid);
        Assert.That(problem.Status, Is.EqualTo(NursingCarePlanStatus.Resolved));
        Assert.That(problem.ResolutionNotes, Is.EqualTo("Pain resolved post-operatively."));
        Assert.That(problem.ResolvedDateTime, Is.Not.Null);
    }

    [Test]
    public async Task NursingCarePlanGrain_DeactivateIntervention_IsActiveBecomesFalse()
    {
        // Arrange
        INursingCarePlanGrain plan = NewCarePlan();
        string pid = await plan.AddDiagnosisAsync("Acute Pain", null, null, null, null, null);
        await plan.AddInterventionAsync(pid, "Ice pack to incision", "Q2H", null, null);
        NursingCarePlanState state = await plan.GetAsync();
        string iid = state.Problems.First(p => p.ProblemId == pid).Interventions[0].InterventionId;

        // Act
        await plan.DeactivateInterventionAsync(pid, iid);
        state = await plan.GetAsync();

        // Assert
        Assert.That(state.Problems.First(p => p.ProblemId == pid).Interventions[0].IsActive, Is.False);
    }

    // ── NursingAcuityGrain ─────────────────────────────────────────────────────

    [Test]
    public async Task NursingAcuityGrain_RecordAcuity_UpdatesCurrentLevel()
    {
        // Arrange
        INursingAcuityGrain acuity = NewAcuity();

        // Act
        await acuity.RecordAcuityAsync(AcuityLevel.IntensiveCare, 65, "RN-001", "Jane Nurse RN", "Days", null);
        NursingAcuityState state = await acuity.GetAsync();

        // Assert
        Assert.That(state.CurrentAcuityLevel, Is.EqualTo(AcuityLevel.IntensiveCare));
        Assert.That(state.CurrentAcuityScore, Is.EqualTo(65));
        Assert.That(state.CurrentAcuityNurseId, Is.EqualTo("RN-001"));
        Assert.That(state.CurrentShift, Is.EqualTo("Days"));
    }

    [Test]
    public async Task NursingAcuityGrain_RecordMultiple_AllAppendedToHistory()
    {
        // Arrange
        INursingAcuityGrain acuity = NewAcuity();

        // Act
        await acuity.RecordAcuityAsync(AcuityLevel.AverageCare, 30, "RN-001", "Jane Nurse RN", "Days", null);
        await acuity.RecordAcuityAsync(AcuityLevel.IntensiveCare, 70, "RN-002", "Bob Nurse RN", "Nights", "Condition deteriorated");
        NursingAcuityState state = await acuity.GetAsync();

        // Assert
        Assert.That(state.AcuityHistory, Has.Count.EqualTo(2));
        Assert.That(state.CurrentAcuityLevel, Is.EqualTo(AcuityLevel.IntensiveCare));
        Assert.That(state.AcuityHistory[1].Notes, Is.EqualTo("Condition deteriorated"));
    }

    // ── NursingUnitGrain ───────────────────────────────────────────────────────

    [Test]
    public async Task NursingUnitGrain_Initialize_SetsUnitName()
    {
        // Arrange
        INursingUnitGrain unit = NewUnit();

        // Act
        await unit.InitializeAsync("4-North MedSurg", "MedSurg", 30);
        NursingUnitState state = await unit.GetAsync();

        // Assert
        Assert.That(state.UnitName, Is.EqualTo("4-North MedSurg"));
        Assert.That(state.UnitType, Is.EqualTo("MedSurg"));
        Assert.That(state.TotalBeds, Is.EqualTo(30));
    }

    [Test]
    public async Task NursingUnitGrain_Initialize_IsIdempotent()
    {
        // Arrange
        INursingUnitGrain unit = NewUnit();
        await unit.InitializeAsync("ICU-A", "ICU", 8);

        // Act — second init call should not overwrite
        await unit.InitializeAsync("DIFFERENT-NAME", "PACU", 12);
        NursingUnitState state = await unit.GetAsync();

        // Assert
        Assert.That(state.UnitName, Is.EqualTo("ICU-A"));
    }

    [Test]
    public async Task NursingUnitGrain_AssignPatient_PopulatesBed()
    {
        // Arrange
        INursingUnitGrain unit = NewUnit();
        await unit.InitializeAsync("3-East", "MedSurg", 20);

        // Act
        await unit.AssignPatientAsync("301A", "PAT-001", "John Smith", DateTime.UtcNow, "RN-001", "Jane Nurse RN");
        NursingUnitState state = await unit.GetAsync();

        // Assert
        NursingBedAssignment? bed = state.BedAssignments.FirstOrDefault(b => b.Bed == "301A");
        Assert.That(bed, Is.Not.Null);
        Assert.That(bed!.IsOccupied, Is.True);
        Assert.That(bed.PatientId, Is.EqualTo("PAT-001"));
        Assert.That(bed.PatientName, Is.EqualTo("John Smith"));
    }

    [Test]
    public async Task NursingUnitGrain_DischargeFromBed_ClearsOccupancy()
    {
        // Arrange
        INursingUnitGrain unit = NewUnit();
        await unit.InitializeAsync("2-West", "PCU", 16);
        await unit.AssignPatientAsync("201B", "PAT-002", "Mary Jones", DateTime.UtcNow, null, null);

        // Act
        await unit.DischargeFromBedAsync("201B");
        NursingUnitState state = await unit.GetAsync();

        // Assert
        NursingBedAssignment? bed = state.BedAssignments.FirstOrDefault(b => b.Bed == "201B");
        Assert.That(bed, Is.Not.Null);
        Assert.That(bed!.IsOccupied, Is.False);
        Assert.That(bed.PatientId, Is.Null);
    }

    [Test]
    public async Task NursingUnitGrain_UpdateBedAcuity_ReflectsInState()
    {
        // Arrange
        INursingUnitGrain unit = NewUnit();
        await unit.InitializeAsync("ICU-B", "ICU", 6);
        await unit.AssignPatientAsync("ICU-1", "PAT-003", "Critical Pat", DateTime.UtcNow, "RN-003", "Alice Nurse RN");

        // Act
        await unit.UpdateBedAcuityAsync("ICU-1", AcuityLevel.CriticalCare);
        NursingUnitState state = await unit.GetAsync();

        // Assert
        Assert.That(state.BedAssignments.First(b => b.Bed == "ICU-1").AcuityLevel,
            Is.EqualTo(AcuityLevel.CriticalCare));
    }

    // ── NursingUnitIndexGrain ──────────────────────────────────────────────────

    [Test]
    public async Task NursingUnitIndexGrain_UpsertUnit_AppearsInDirectory()
    {
        // Arrange
        INursingUnitIndexGrain idx = GetUnitIndex();
        NursingUnitEntry entry = new()
        {
            UnitId       = $"UNIT-{Guid.NewGuid():N}",
            UnitName     = "Oncology",
            UnitType     = "Oncology",
            TotalBeds    = 22,
            OccupiedBeds = 10
        };

        // Act
        await idx.UpsertUnitAsync(entry);
        NursingUnitIndexState state = await idx.GetAsync();

        // Assert
        Assert.That(state.Units.Any(u => u.UnitId == entry.UnitId), Is.True);
    }

    [Test]
    public async Task NursingUnitIndexGrain_RemoveUnit_NoLongerInDirectory()
    {
        // Arrange
        INursingUnitIndexGrain idx = GetUnitIndex();
        string uid = $"UNIT-{Guid.NewGuid():N}";
        await idx.UpsertUnitAsync(new NursingUnitEntry
        {
            UnitId = uid, UnitName = "Temp Unit", TotalBeds = 5
        });

        // Act
        await idx.RemoveUnitAsync(uid);
        NursingUnitIndexState state = await idx.GetAsync();

        // Assert
        Assert.That(state.Units.All(u => u.UnitId != uid), Is.True);
    }
}
