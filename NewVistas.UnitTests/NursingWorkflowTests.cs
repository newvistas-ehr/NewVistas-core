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

    /// <summary>Fresh inpatient unit (unit-owns-beds model) in its own institution.</summary>
    private async Task<(string Inst, string UnitId, IInpatientUnitGrain Grain)> NewUnitAsync(
        string name, string? unitType, int beds)
    {
        string inst = $"INST-{Guid.NewGuid():N}";
        string unitId = $"U-{Guid.NewGuid():N}";
        var unit = _cluster.GrainFactory.GetGrain<IInpatientUnitGrain>($"UNIT:{inst}:{unitId}");
        await unit.ConfigureUnitAsync(name, unitType, null);
        for (int i = 1; i <= beds; i++)
            await unit.AddBedAsync($"B{i}", null, BedType.Regular);
        return (inst, unitId, unit);
    }

    private IBedCapacityGrain Capacity(string institutionId)
        => _cluster.GrainFactory.GetGrain<IBedCapacityGrain>($"BED-CAPACITY:{institutionId}");

    private static UnitAdmissionRequest Admission(string patientId, string patientName, string bedId,
        string? nurseId = null, string? nurseName = null) => new()
    {
        PatientId = patientId,
        PatientName = patientName,
        MovementId = $"ADT-{Guid.NewGuid()}",
        BedId = bedId,
        AdmitDate = DateTime.UtcNow,
        AttendingNurseId = nurseId,
        AttendingNurseName = nurseName
    };

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

    // ── InpatientUnitGrain (nursing surface — replaces the retired NursingUnitGrain) ──

    [Test]
    public async Task InpatientUnitGrain_Configure_SetsUnitNameAndBeds()
    {
        // Arrange + Act
        var (_, _, unit) = await NewUnitAsync("4-North MedSurg", "MedSurg", 30);
        InpatientUnitState state = await unit.GetAsync();
        UnitCapacitySummary summary = await unit.GetCapacitySummaryAsync();

        // Assert
        Assert.That(state.Name, Is.EqualTo("4-North MedSurg"));
        Assert.That(state.UnitType, Is.EqualTo("MedSurg"));
        Assert.That(summary.TotalBeds, Is.EqualTo(30));
        Assert.That(summary.Available, Is.EqualTo(30));
    }

    [Test]
    public async Task InpatientUnitGrain_Reconfigure_UpdatesProfile_KeepsBeds()
    {
        // Arrange
        var (_, _, unit) = await NewUnitAsync("ICU-A", "ICU", 8);

        // Act — ConfigureUnitAsync is an idempotent create-or-update
        await unit.ConfigureUnitAsync("ICU-A (Renovated)", "ICU", null);
        InpatientUnitState state = await unit.GetAsync();

        // Assert — profile updated, structure untouched
        Assert.That(state.Name, Is.EqualTo("ICU-A (Renovated)"));
        Assert.That(state.Beds, Has.Count.EqualTo(8));
    }

    [Test]
    public async Task InpatientUnitGrain_AdmitPatient_PopulatesBed()
    {
        // Arrange
        var (_, _, unit) = await NewUnitAsync("3-East", "MedSurg", 20);

        // Act
        await unit.AdmitPatientAsync(Admission("PAT-001", "John Smith", "B1", "RN-001", "Jane Nurse RN"));
        InpatientUnitState state = await unit.GetAsync();

        // Assert
        InpatientBed? bed = state.Beds.FirstOrDefault(b => b.BedId == "B1");
        Assert.That(bed, Is.Not.Null);
        Assert.That(bed!.State, Is.EqualTo(BedLifecycleState.Occupied));
        Assert.That(bed.PatientId, Is.EqualTo("PAT-001"));
        Assert.That(bed.PatientName, Is.EqualTo("John Smith"));
        Assert.That(bed.AttendingNurseId, Is.EqualTo("RN-001"));
    }

    [Test]
    public async Task InpatientUnitGrain_ReleasePatient_ClearsOccupancy_BedGoesDirty()
    {
        // Arrange
        var (_, _, unit) = await NewUnitAsync("2-West", "PCU", 16);
        await unit.AdmitPatientAsync(Admission("PAT-002", "Mary Jones", "B1"));

        // Act
        string? vacated = await unit.ReleasePatientAsync("PAT-002", $"ADT-{Guid.NewGuid()}");
        InpatientUnitState state = await unit.GetAsync();

        // Assert
        Assert.That(vacated, Is.EqualTo("B1"));
        InpatientBed bed = state.Beds.First(b => b.BedId == "B1");
        Assert.That(bed.State, Is.EqualTo(BedLifecycleState.Dirty));
        Assert.That(bed.PatientId, Is.Null);
    }

    [Test]
    public async Task InpatientUnitGrain_UpdateBedAcuity_ReflectsInState()
    {
        // Arrange
        var (_, _, unit) = await NewUnitAsync("ICU-B", "ICU", 6);
        await unit.AdmitPatientAsync(Admission("PAT-003", "Critical Pat", "B1", "RN-003", "Alice Nurse RN"));

        // Act
        await unit.UpdateBedAcuityAsync("B1", AcuityLevel.CriticalCare);
        InpatientUnitState state = await unit.GetAsync();

        // Assert
        Assert.That(state.Beds.First(b => b.BedId == "B1").AcuityLevel,
            Is.EqualTo(AcuityLevel.CriticalCare));
    }

    [Test]
    public async Task InpatientUnitGrain_AssignBedNurse_ShowsOnCensus()
    {
        // Arrange
        var (_, _, unit) = await NewUnitAsync("5-South", "MedSurg", 4);
        await unit.AdmitPatientAsync(Admission("PAT-004", "Covered Pat", "B2"));

        // Act
        await unit.AssignBedNurseAsync("B2", "RN-010", "Cover Nurse RN");
        List<UnitCensusEntry> census = await unit.GetCensusAsync();

        // Assert
        Assert.That(census.First(e => e.PatientId == "PAT-004").AttendingNurseName,
            Is.EqualTo("Cover Nurse RN"));
    }

    // ── BedCapacityGrain (unit directory — replaces the retired NursingUnitIndexGrain) ──

    [Test]
    public async Task BedCapacityGrain_ConfiguredUnit_AppearsInDirectory()
    {
        // Arrange + Act — configuring a unit pushes its rollup to the capacity directory
        var (inst, unitId, _) = await NewUnitAsync("Oncology", "Oncology", 22);
        List<UnitCapacitySummary> units = await Capacity(inst).GetUnitsAsync();

        // Assert
        UnitCapacitySummary? entry = units.FirstOrDefault(u => u.UnitId == unitId);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.Name, Is.EqualTo("Oncology"));
        Assert.That(entry.UnitType, Is.EqualTo("Oncology"));
        Assert.That(entry.TotalBeds, Is.EqualTo(22));
    }

    [Test]
    public async Task BedCapacityGrain_OccupancyRollsUpToDirectory()
    {
        // Arrange
        var (inst, unitId, unit) = await NewUnitAsync("Telemetry", "PCU", 10);

        // Act
        await unit.AdmitPatientAsync(Admission("PAT-005", "Tele Pat", "B1"));
        UnitCapacitySummary? entry = await Capacity(inst).GetUnitAsync(unitId);

        // Assert
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.Occupied, Is.EqualTo(1));
        Assert.That(entry.Available, Is.EqualTo(9));
    }

    [Test]
    public async Task BedCapacityGrain_DeactivatedUnit_NoLongerInDirectory()
    {
        // Arrange
        var (inst, unitId, unit) = await NewUnitAsync("Temp Unit", null, 5);

        // Act
        await unit.DeactivateUnitAsync();
        List<UnitCapacitySummary> units = await Capacity(inst).GetUnitsAsync();

        // Assert
        Assert.That(units.All(u => u.UnitId != unitId), Is.True);
    }
}
