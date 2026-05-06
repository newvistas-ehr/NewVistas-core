// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for VistA Nursing — Files #210-212.
/// Tests end-to-end workflows via <see cref="IPatientWorkflowGrain"/>.
/// </summary>
[TestFixture]
public class NursingFunctionalTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain NewWorkflow()
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>($"PATIENT-{Guid.NewGuid():N}");

    // ─── Assessment workflow ───────────────────────────────────────────────────

    [Test]
    public async Task NursingWorkflow_CreateAssessment_ReturnsNonEmptyId()
    {
        // Arrange
        IPatientWorkflowGrain wf = NewWorkflow();

        // Act
        string id = await wf.CreateNursingAssessmentAsync(
            assessmentDateTime    : DateTime.UtcNow,
            assessmentType        : "Initial",
            nurseId               : "RN-001",
            nurseName             : "Jane Nurse RN",
            locationId            : null,
            locationName          : "4-North",
            levelOfConsciousness  : "Alert",
            orientation           : new List<string> { "Person", "Place", "Time" },
            breathSounds          : "Clear",
            oxygenTherapy         : "RoomAir",
            spO2                  : 97m,
            heartRhythm           : "Regular",
            edema                 : "None",
            skinIntegrity         : "Intact",
            bradenScore           : 19,
            painScore             : 3,
            painLocation          : "Abdomen",
            bowelSounds           : "Active",
            appetiteAssessment    : "Good",
            urineOutput           : 250m,
            hasFoley              : false,
            anxietyLevel          : "Mild",
            mood                  : "Calm",
            morseScore            : 25,
            fallRiskLevel         : "Moderate",
            fallPrecautions       : new List<string> { "Non-slip socks", "Call light within reach" },
            adlMobility           : "Independent",
            narrativeNotes        : "Patient alert and oriented ×3. No acute distress.");

        // Assert
        Assert.That(id, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task NursingWorkflow_CreateAssessment_AppearsInIndex()
    {
        // Arrange
        IPatientWorkflowGrain wf = NewWorkflow();

        // Act
        string id = await wf.CreateNursingAssessmentAsync(
            DateTime.UtcNow, "Shift", "RN-001", "Jane Nurse RN",
            null, null, "Alert", null,
            "Clear", "RoomAir", 98m, "Regular", "None",
            "Intact", 20, 2, null, "Active", "Good",
            200m, false, "None", "Calm", 15, "Low", null,
            "Independent", null);

        List<NursingAssessmentIndexEntry> index = await wf.GetNursingAssessmentsAsync();

        // Assert
        Assert.That(index, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(index.Any(a => a.AssessmentId == id), Is.True);
    }

    [Test]
    public async Task NursingWorkflow_GetNursingAssessment_ReturnsFullState()
    {
        // Arrange
        IPatientWorkflowGrain wf = NewWorkflow();
        string id = await wf.CreateNursingAssessmentAsync(
            DateTime.UtcNow, "Initial", "RN-001", "Jane Nurse RN",
            null, "ICU-B", "Alert", new List<string> { "Person", "Place" },
            "Diminished", "NasalCannula", 92m, "Irregular", "2+",
            "Impaired", 14, 8, "Bilateral lower extremities", "Hypoactive", "Poor",
            80m, true, "Moderate", "Anxious", 55, "High",
            new List<string> { "Bed alarm", "1:1 sitter" }, "Bedrest", "Critical care assessment.");

        // Act
        NursingAssessmentState state = await wf.GetNursingAssessmentAsync(id);

        // Assert
        Assert.That(state.AssessmentId, Is.EqualTo(id));
        Assert.That(state.BreathSounds, Is.EqualTo("Diminished"));
        Assert.That(state.SpO2, Is.EqualTo(92m));
        Assert.That(state.PainScore, Is.EqualTo(8));
        Assert.That(state.BradenScore, Is.EqualTo(14));
        Assert.That(state.MorseScoreTotal, Is.EqualTo(55));
        Assert.That(state.FallRiskLevel, Is.EqualTo("High"));
        Assert.That(state.HasFoley, Is.True);
        Assert.That(state.FallPrecautions, Contains.Item("Bed alarm"));
    }

    [Test]
    public async Task NursingWorkflow_SignAssessment_UpdatesIndexEntryStatus()
    {
        // Arrange
        IPatientWorkflowGrain wf = NewWorkflow();
        string id = await wf.CreateNursingAssessmentAsync(
            DateTime.UtcNow, "Shift", "RN-001", "Jane Nurse RN",
            null, null, null, null, null, null, null, null, null, null, null,
            0, null, null, null, null, false, null, null, null, null, null,
            null, null);

        // Act
        await wf.SignNursingAssessmentAsync(id, "RN-001", "Jane Nurse RN");

        // Assert — full state
        NursingAssessmentState state = await wf.GetNursingAssessmentAsync(id);
        Assert.That(state.Status, Is.EqualTo(NursingAssessmentStatus.Signed));

        // Assert — index entry also updated
        List<NursingAssessmentIndexEntry> index = await wf.GetNursingAssessmentsAsync();
        NursingAssessmentIndexEntry? entry = index.FirstOrDefault(a => a.AssessmentId == id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.Status, Is.EqualTo(NursingAssessmentStatus.Signed));
    }

    // ─── Care Plan workflow ────────────────────────────────────────────────────

    [Test]
    public async Task NursingWorkflow_AddNursingDiagnosis_ReturnsId()
    {
        // Arrange
        IPatientWorkflowGrain wf = NewWorkflow();

        // Act
        string problemId = await wf.AddNursingDiagnosisAsync(
            "Acute Pain",
            "post-operative tissue damage",
            "patient reports 7/10 pain at incision site",
            1, "RN-001", "Jane Nurse RN");

        // Assert
        Assert.That(problemId, Does.StartWith("NDP-"));
    }

    [Test]
    public async Task NursingWorkflow_FullCarePlanLifecycle_GoalsInterventionsOutcomes()
    {
        // Arrange
        IPatientWorkflowGrain wf = NewWorkflow();

        // Act — add diagnosis
        string problemId = await wf.AddNursingDiagnosisAsync(
            "Impaired Physical Mobility", "pain with movement", null, 2, "RN-001", "Jane Nurse RN");

        // Act — add goal
        await wf.AddCarePlanGoalAsync(
            problemId, "Patient will ambulate 20 ft with assistance by end of shift", DateTime.Today.AddDays(1));

        // Act — add intervention
        await wf.AddCarePlanInterventionAsync(
            problemId, "Assist patient with ambulation and PT exercises", "BID", "RN-001", "Jane Nurse RN");

        // Act — record outcome
        await wf.RecordCarePlanOutcomeAsync(
            problemId, NursingOutcomeRating.GoalPartiallyMet, "RN-001", "Jane Nurse RN",
            "Patient ambulated 10 ft, fatigued easily.");

        // Assert
        NursingCarePlanState carePlan = await wf.GetNursingCarePlanAsync();
        NursingCarePlanProblem? problem = carePlan.Problems.FirstOrDefault(p => p.ProblemId == problemId);
        Assert.That(problem, Is.Not.Null);
        Assert.That(problem!.Goals, Has.Count.EqualTo(1));
        Assert.That(problem.Interventions, Has.Count.EqualTo(1));
        Assert.That(problem.OutcomeEvaluations, Has.Count.EqualTo(1));
        Assert.That(problem.OutcomeEvaluations[0].OutcomeRating, Is.EqualTo(NursingOutcomeRating.GoalPartiallyMet));
    }

    [Test]
    public async Task NursingWorkflow_ResolveNursingDiagnosis_StatusChanges()
    {
        // Arrange
        IPatientWorkflowGrain wf = NewWorkflow();
        string pid = await wf.AddNursingDiagnosisAsync(
            "Acute Pain", null, null, 1, "RN-001", "Jane Nurse RN");

        // Act
        await wf.ResolveNursingDiagnosisAsync(pid, "Pain adequately controlled. Patient discharged.");
        NursingCarePlanState state = await wf.GetNursingCarePlanAsync();

        // Assert
        NursingCarePlanProblem? problem = state.Problems.FirstOrDefault(p => p.ProblemId == pid);
        Assert.That(problem, Is.Not.Null);
        Assert.That(problem!.Status, Is.EqualTo(NursingCarePlanStatus.Resolved));
        Assert.That(problem.ResolutionNotes, Does.Contain("discharged"));
    }

    [Test]
    public async Task NursingWorkflow_UpdateCarePlanGoalStatus_ReflectsAchieved()
    {
        // Arrange
        IPatientWorkflowGrain wf = NewWorkflow();
        string pid = await wf.AddNursingDiagnosisAsync("Acute Pain", null, null, null, null, null);
        await wf.AddCarePlanGoalAsync(pid, "Pain ≤3/10 by EOD", null);
        NursingCarePlanState state = await wf.GetNursingCarePlanAsync();
        string goalId = state.Problems.First(p => p.ProblemId == pid).Goals[0].GoalId;

        // Act
        await wf.UpdateCarePlanGoalStatusAsync(pid, goalId, NursingGoalStatus.Achieved);
        state = await wf.GetNursingCarePlanAsync();

        // Assert
        Assert.That(state.Problems.First(p => p.ProblemId == pid).Goals[0].Status,
            Is.EqualTo(NursingGoalStatus.Achieved));
    }

    // ─── Acuity workflow ───────────────────────────────────────────────────────

    [Test]
    public async Task NursingWorkflow_RecordAcuity_UpdatesCurrentLevel()
    {
        // Arrange
        IPatientWorkflowGrain wf = NewWorkflow();

        // Act
        await wf.RecordNursingAcuityAsync(AcuityLevel.AboveAverageCare, 45, "RN-001", "Jane Nurse RN", "Days", null);
        NursingAcuityState state = await wf.GetNursingAcuityAsync();

        // Assert
        Assert.That(state.CurrentAcuityLevel, Is.EqualTo(AcuityLevel.AboveAverageCare));
        Assert.That(state.CurrentAcuityScore, Is.EqualTo(45));
        Assert.That(state.CurrentShift, Is.EqualTo("Days"));
    }

    [Test]
    public async Task NursingWorkflow_RecordMultipleAcuities_HistoryGrows()
    {
        // Arrange
        IPatientWorkflowGrain wf = NewWorkflow();

        // Act
        await wf.RecordNursingAcuityAsync(AcuityLevel.AverageCare, 28, "RN-001", "Jane Nurse RN", "Days", null);
        await wf.RecordNursingAcuityAsync(AcuityLevel.IntensiveCare, 72, "RN-002", "Bob Nurse RN", "Nights", "Condition deteriorated");
        NursingAcuityState state = await wf.GetNursingAcuityAsync();

        // Assert
        Assert.That(state.AcuityHistory, Has.Count.EqualTo(2));
        Assert.That(state.CurrentAcuityLevel, Is.EqualTo(AcuityLevel.IntensiveCare));
        Assert.That(state.AcuityHistory[1].Notes, Is.EqualTo("Condition deteriorated"));
    }

    // ─── Combined: Assessment + Acuity ────────────────────────────────────────

    [Test]
    public async Task NursingWorkflow_MultipleAssessments_IndexSortedNewestFirst()
    {
        // Arrange
        IPatientWorkflowGrain wf = NewWorkflow();

        DateTime older = DateTime.UtcNow.AddHours(-6);
        DateTime newer = DateTime.UtcNow;

        // Act — older assessment first
        string idOlder = await wf.CreateNursingAssessmentAsync(
            older, "Shift", "RN-001", "Jane Nurse RN", null, null,
            null, null, null, null, null, null, null, null, null,
            5, null, null, null, null, false, null, null, null, null, null, null, null);

        string idNewer = await wf.CreateNursingAssessmentAsync(
            newer, "Shift", "RN-002", "Bob Nurse RN", null, null,
            null, null, null, null, null, null, null, null, null,
            2, null, null, null, null, false, null, null, null, null, null, null, null);

        List<NursingAssessmentIndexEntry> index = await wf.GetNursingAssessmentsAsync();

        // Assert — newest first
        Assert.That(index[0].AssessmentId, Is.EqualTo(idNewer));
        Assert.That(index[1].AssessmentId, Is.EqualTo(idOlder));
    }
}
