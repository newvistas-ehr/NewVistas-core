// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for VistA Mental Health Instruments — YS MH INSTRUMENT (#601.71).
/// Tests instrument recording, scoring, risk assessment, follow-up, and trending
/// via <see cref="IMentalHealthGrain"/> methods directly. Includes Phase 4 depth
/// enhancements: item responses, auto-scoring, score interpretation, and trending.
/// </summary>
[TestFixture]
public class MentalHealthWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IMentalHealthGrain NewInstrument()
        => _cluster.GrainFactory.GetGrain<IMentalHealthGrain>($"MH-{Guid.NewGuid():N}");

    private async Task RecordPhq9Async(IMentalHealthGrain grain, string patientId = "PAT-001")
    {
        await grain.RecordInstrumentAsync(
            patientId, "PHQ-9", "INST-PHQ9",
            DateTime.UtcNow, null, null, null,
            null,
            "PROV-001", "Dr. Adams",
            "PROV-002", "Dr. Rivera",
            "LOC-001", "Mental Health Clinic",
            null, null);
    }

    /// <summary>
    /// Add PHQ-9 item responses (items 1-9) with specified per-item values.
    /// </summary>
    private async Task AddPhq9ResponsesAsync(IMentalHealthGrain grain, int[] values)
    {
        string[] questions = new[]
        {
            "Little interest or pleasure in doing things",
            "Feeling down, depressed, or hopeless",
            "Trouble falling or staying asleep, or sleeping too much",
            "Feeling tired or having little energy",
            "Poor appetite or overeating",
            "Feeling bad about yourself or that you are a failure",
            "Trouble concentrating on things",
            "Moving or speaking slowly or being fidgety/restless",
            "Thoughts that you would be better off dead or of hurting yourself"
        };

        for (int i = 0; i < values.Length && i < 9; i++)
        {
            await grain.AddItemResponseAsync(i + 1, questions[i], values[i], null);
        }
    }

    // ─── 1. Record instrument ───────────────────────────────────────────────

    [Test]
    public async Task MentalHealth_CanRecordInstrument()
    {
        // Arrange
        IMentalHealthGrain grain = NewInstrument();

        // Act
        await RecordPhq9Async(grain);
        MentalHealthState state = await grain.GetInstrumentAsync();

        // Assert
        Assert.That(state.InstrumentId, Is.Not.Null.And.Not.Empty);
        Assert.That(state.InstrumentName, Is.EqualTo("PHQ-9"));
        Assert.That(state.PatientId, Is.EqualTo("PAT-001"));
        Assert.That(state.Status, Is.EqualTo("COMPLETED"));
        Assert.That(state.AdministeredByName, Is.EqualTo("Dr. Adams"));
    }

    // ─── 2. Get instrument ──────────────────────────────────────────────────

    [Test]
    public async Task MentalHealth_CanGetInstrument()
    {
        // Arrange
        IMentalHealthGrain grain = NewInstrument();
        await RecordPhq9Async(grain);

        // Act
        MentalHealthState state = await grain.GetInstrumentAsync();

        // Assert
        Assert.That(state.InstrumentName, Is.EqualTo("PHQ-9"));
        Assert.That(state.InstrumentDefId, Is.EqualTo("INST-PHQ9"));
        Assert.That(state.LocationName, Is.EqualTo("Mental Health Clinic"));
        Assert.That(state.OrderingProviderName, Is.EqualTo("Dr. Rivera"));
    }

    // ─── 3. Cancel ──────────────────────────────────────────────────────────

    [Test]
    public async Task MentalHealth_CanCancel()
    {
        // Arrange
        IMentalHealthGrain grain = NewInstrument();
        await RecordPhq9Async(grain);

        // Act
        await grain.CancelAsync();
        MentalHealthState state = await grain.GetInstrumentAsync();

        // Assert
        Assert.That(state.Status, Is.EqualTo("CANCELLED"));
    }

    // ─── 4. Record risk assessment ──────────────────────────────────────────

    [Test]
    public async Task MentalHealth_CanRecordRiskAssessment()
    {
        // Arrange
        IMentalHealthGrain grain = NewInstrument();
        await RecordPhq9Async(grain);

        // Act
        await grain.RecordRiskAssessmentAsync(3, "Patient reports suicidal ideation without plan");
        MentalHealthState state = await grain.GetInstrumentAsync();

        // Assert
        Assert.That(state.RiskLevel, Is.EqualTo(3));
        Assert.That(state.RiskAssessmentNotes, Is.EqualTo("Patient reports suicidal ideation without plan"));
    }

    // ─── 5. Set follow-up ───────────────────────────────────────────────────

    [Test]
    public async Task MentalHealth_CanSetFollowUp()
    {
        // Arrange
        IMentalHealthGrain grain = NewInstrument();
        await RecordPhq9Async(grain);
        DateTime followUpDate = new DateTime(2026, 4, 15, 10, 0, 0, DateTimeKind.Utc);

        // Act
        await grain.SetFollowUpAsync(true, followUpDate, "Repeat PHQ-9 and reassess medication response");
        MentalHealthState state = await grain.GetInstrumentAsync();

        // Assert
        Assert.That(state.RequiresFollowUp, Is.True);
        Assert.That(state.FollowUpDueDate, Is.EqualTo(followUpDate));
        Assert.That(state.FollowUpPlan, Is.EqualTo("Repeat PHQ-9 and reassess medication response"));
    }

    // ─── 6. Add item response ───────────────────────────────────────────────

    [Test]
    public async Task MentalHealth_CanAddItemResponse()
    {
        // Arrange
        IMentalHealthGrain grain = NewInstrument();
        await RecordPhq9Async(grain);

        // Act
        await grain.AddItemResponseAsync(1, "Little interest or pleasure in doing things", 2, "More than half the days");
        MentalHealthState state = await grain.GetInstrumentAsync();

        // Assert
        Assert.That(state.ItemResponses, Has.Count.EqualTo(1));
        Assert.That(state.ItemResponses[0].ItemNumber, Is.EqualTo(1));
        Assert.That(state.ItemResponses[0].ResponseValue, Is.EqualTo(2));
        Assert.That(state.ItemResponses[0].ResponseText, Is.EqualTo("More than half the days"));
    }

    // ─── 7. Add multiple item responses ─────────────────────────────────────

    [Test]
    public async Task MentalHealth_CanAddMultipleItemResponses()
    {
        // Arrange
        IMentalHealthGrain grain = NewInstrument();
        await RecordPhq9Async(grain);

        // Act
        await grain.AddItemResponseAsync(1, "Little interest or pleasure", 2, null);
        await grain.AddItemResponseAsync(2, "Feeling down, depressed", 1, null);
        await grain.AddItemResponseAsync(3, "Trouble falling or staying asleep", 3, null);
        MentalHealthState state = await grain.GetInstrumentAsync();

        // Assert
        Assert.That(state.ItemResponses, Has.Count.EqualTo(3));
        Assert.That(state.ItemResponses[0].ResponseValue, Is.EqualTo(2));
        Assert.That(state.ItemResponses[1].ResponseValue, Is.EqualTo(1));
        Assert.That(state.ItemResponses[2].ResponseValue, Is.EqualTo(3));
    }

    // ─── 8. Score instrument ────────────────────────────────────────────────

    [Test]
    public async Task MentalHealth_CanScoreInstrument()
    {
        // Arrange — PHQ-9 items 1-9, each scored 1 => total = 9 (MILD)
        IMentalHealthGrain grain = NewInstrument();
        await RecordPhq9Async(grain);
        await AddPhq9ResponsesAsync(grain, new[] { 1, 1, 1, 1, 1, 1, 1, 1, 1 });

        // Act
        await grain.ScoreInstrumentAsync();
        MentalHealthState state = await grain.GetInstrumentAsync();

        // Assert
        Assert.That(state.TotalScore, Is.EqualTo(9));
        Assert.That(state.ScoringMethodUsed, Is.EqualTo("AUTO"));
        Assert.That(state.ScoreInterpretation, Is.EqualTo("MILD"));
    }

    // ─── 9. Score interpretation — MODERATE ─────────────────────────────────

    [Test]
    public async Task MentalHealth_ScoreInterpretation_Moderate()
    {
        // Arrange — PHQ-9 items totaling 12: {2,2,2,1,1,1,1,1,1} = 12
        IMentalHealthGrain grain = NewInstrument();
        await RecordPhq9Async(grain);
        await AddPhq9ResponsesAsync(grain, new[] { 2, 2, 2, 1, 1, 1, 1, 1, 1 });

        // Act
        await grain.ScoreInstrumentAsync();
        MentalHealthState state = await grain.GetInstrumentAsync();

        // Assert
        Assert.That(state.TotalScore, Is.EqualTo(12));
        Assert.That(state.ScoreInterpretation, Is.EqualTo("MODERATE"));
    }

    // ─── 10. Score interpretation — SEVERE ──────────────────────────────────

    [Test]
    public async Task MentalHealth_ScoreInterpretation_Severe()
    {
        // Arrange — PHQ-9 items totaling 22: {3,3,3,3,2,2,2,2,2} = 22
        IMentalHealthGrain grain = NewInstrument();
        await RecordPhq9Async(grain);
        await AddPhq9ResponsesAsync(grain, new[] { 3, 3, 3, 3, 2, 2, 2, 2, 2 });

        // Act
        await grain.ScoreInstrumentAsync();
        MentalHealthState state = await grain.GetInstrumentAsync();

        // Assert
        Assert.That(state.TotalScore, Is.EqualTo(22));
        Assert.That(state.ScoreInterpretation, Is.EqualTo("SEVERE"));
    }

    // ─── 11. Set previous score ─────────────────────────────────────────────

    [Test]
    public async Task MentalHealth_CanSetPreviousScore()
    {
        // Arrange
        IMentalHealthGrain grain = NewInstrument();
        await RecordPhq9Async(grain);
        DateTime previousDate = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);

        // Act
        await grain.SetPreviousScoreAsync(18m, previousDate);
        MentalHealthState state = await grain.GetInstrumentAsync();

        // Assert
        Assert.That(state.PreviousScore, Is.EqualTo(18m));
        Assert.That(state.PreviousAdministrationDate, Is.EqualTo(previousDate));
    }

    // ─── 12. Calculate score change ─────────────────────────────────────────

    [Test]
    public async Task MentalHealth_CanCalculateScoreChange()
    {
        // Arrange — current score 12, previous 18 => change = -6
        IMentalHealthGrain grain = NewInstrument();
        await RecordPhq9Async(grain);
        await AddPhq9ResponsesAsync(grain, new[] { 2, 2, 2, 1, 1, 1, 1, 1, 1 });
        await grain.ScoreInstrumentAsync(); // TotalScore = 12
        await grain.SetPreviousScoreAsync(18m, new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc));

        // Act
        decimal? change = await grain.CalculateScoreChangeAsync();

        // Assert
        Assert.That(change, Is.Not.Null);
        Assert.That(change, Is.EqualTo(-6m));
    }

    // ─── 13. Calculate score change — no previous ───────────────────────────

    [Test]
    public async Task MentalHealth_CalculateScoreChange_ReturnsNull_WhenNoPrevious()
    {
        // Arrange — record with a total score but no previous score set
        IMentalHealthGrain grain = NewInstrument();
        await RecordPhq9Async(grain);
        await AddPhq9ResponsesAsync(grain, new[] { 1, 1, 1, 1, 1, 1, 1, 1, 1 });
        await grain.ScoreInstrumentAsync(); // TotalScore = 9

        // Act
        decimal? change = await grain.CalculateScoreChangeAsync();

        // Assert
        Assert.That(change, Is.Null);
    }

    // ─── 14. Risk assessment — imminent level ───────────────────────────────

    [Test]
    public async Task MentalHealth_RiskAssessment_ImminentLevel()
    {
        // Arrange
        IMentalHealthGrain grain = NewInstrument();
        await RecordPhq9Async(grain);

        // Act
        await grain.RecordRiskAssessmentAsync(4, "Active suicidal ideation with plan and access to means");
        MentalHealthState state = await grain.GetInstrumentAsync();

        // Assert
        Assert.That(state.RiskLevel, Is.EqualTo(4));
        Assert.That(state.RiskAssessmentNotes, Does.Contain("Active suicidal ideation"));
    }

    // ─── 15. Follow-up not required ─────────────────────────────────────────

    [Test]
    public async Task MentalHealth_FollowUp_NotRequired()
    {
        // Arrange
        IMentalHealthGrain grain = NewInstrument();
        await RecordPhq9Async(grain);

        // Act
        await grain.SetFollowUpAsync(false, null, null);
        MentalHealthState state = await grain.GetInstrumentAsync();

        // Assert
        Assert.That(state.RequiresFollowUp, Is.False);
        Assert.That(state.FollowUpDueDate, Is.Null);
        Assert.That(state.FollowUpPlan, Is.Null);
    }

    // ─── 16. Full workflow ──────────────────────────────────────────────────

    [Test]
    public async Task MentalHealth_FullWorkflow()
    {
        // Arrange
        IMentalHealthGrain grain = NewInstrument();
        await RecordPhq9Async(grain, "PAT-042");

        // Act — add item responses (PHQ-9, items 1-9, moderate total = 14)
        await AddPhq9ResponsesAsync(grain, new[] { 2, 2, 2, 2, 1, 1, 1, 1, 2 });

        // Score
        await grain.ScoreInstrumentAsync();
        MentalHealthState afterScore = await grain.GetInstrumentAsync();
        Assert.That(afterScore.TotalScore, Is.EqualTo(14));
        Assert.That(afterScore.ScoreInterpretation, Is.EqualTo("MODERATE"));
        Assert.That(afterScore.ScoringMethodUsed, Is.EqualTo("AUTO"));

        // Risk assessment
        await grain.RecordRiskAssessmentAsync(2, "Moderate risk — passive ideation, no plan");
        MentalHealthState afterRisk = await grain.GetInstrumentAsync();
        Assert.That(afterRisk.RiskLevel, Is.EqualTo(2));

        // Follow-up
        DateTime followUpDate = new DateTime(2026, 5, 1, 14, 0, 0, DateTimeKind.Utc);
        await grain.SetFollowUpAsync(true, followUpDate, "Reassess in 4 weeks; consider SSRI augmentation");
        MentalHealthState afterFollowUp = await grain.GetInstrumentAsync();
        Assert.That(afterFollowUp.RequiresFollowUp, Is.True);
        Assert.That(afterFollowUp.FollowUpPlan, Does.Contain("SSRI"));

        // Set previous score for trending
        await grain.SetPreviousScoreAsync(18m, new DateTime(2026, 2, 1, 10, 0, 0, DateTimeKind.Utc));

        // Calculate change
        decimal? change = await grain.CalculateScoreChangeAsync();
        Assert.That(change, Is.EqualTo(-4m));

        // Final state check
        MentalHealthState finalState = await grain.GetInstrumentAsync();
        Assert.That(finalState.PatientId, Is.EqualTo("PAT-042"));
        Assert.That(finalState.InstrumentName, Is.EqualTo("PHQ-9"));
        Assert.That(finalState.ItemResponses, Has.Count.EqualTo(9));
        Assert.That(finalState.PreviousScore, Is.EqualTo(18m));
        Assert.That(finalState.ClinicalRecommendation, Is.Not.Null.And.Not.Empty);
    }
}
