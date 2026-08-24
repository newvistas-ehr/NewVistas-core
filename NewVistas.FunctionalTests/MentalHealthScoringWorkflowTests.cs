// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// End-to-end tests for the mental health scoring workflow on
/// <see cref="IPatientWorkflowGrain"/> — instrument administration, item
/// responses, auto-scoring, trending (previous score / score change),
/// risk assessment, follow-up, and the paged history reader.
/// The existing MentalHealthWorkflowTests exercise the grain directly;
/// these tests cover the workflow-grain composition layer.
/// </summary>
[TestFixture]
public class MentalHealthScoringWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private IPatientWorkflowGrain Workflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private IMentalHealthGrain Instrument(string instrumentId)
        => _cluster.GrainFactory.GetGrain<IMentalHealthGrain>(instrumentId);

    /// <summary>Administers an unscored instrument through the workflow.</summary>
    private static Task<string> AdministerAsync(
        IPatientWorkflowGrain wf, string instrumentName, DateTime? administered = null)
        => wf.RecordMentalHealthScreenAsync(
            instrumentName, administered ?? DateTime.UtcNow,
            null, null, null, null,
            "PROV-100", "Dr. Chen", "LOC-MH", "Mental Health Clinic", null);

    /// <summary>Adds sequential item responses (items 1..values.Length).</summary>
    private static async Task AddResponsesAsync(IPatientWorkflowGrain wf, string mhId, int[] values)
    {
        for (int i = 0; i < values.Length; i++)
            await wf.AddMentalHealthItemResponseAsync(mhId, i + 1, $"Item {i + 1}", values[i], null);
    }

    // ─── Scoring ────────────────────────────────────────────────────────────

    [Test]
    public async Task Scoring_TotalsItemResponses_AndInterpretsKnownInstrument()
    {
        // Arrange — PHQ-9 responses {0,1,2,3,0,1,2,3,1} = 13 → MODERATE, positive screen
        string pid = $"MHPAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(pid);
        string mhId = await AdministerAsync(wf, "PHQ-9");
        await AddResponsesAsync(wf, mhId, new[] { 0, 1, 2, 3, 0, 1, 2, 3, 1 });

        // Act
        await wf.ScoreMentalHealthInstrumentAsync(mhId);

        // Assert — the score must be visible through the workflow read path
        List<MentalHealthSummary> screens = await wf.GetMentalHealthScreensAsync();
        MentalHealthSummary summary = screens.Single(s => s.InstrumentId == mhId);
        Assert.That(summary.TotalScore, Is.EqualTo(13m));
        Assert.That(summary.ScoreInterpretation, Is.EqualTo("MODERATE"));
        Assert.That(summary.IsPositiveScreen, Is.True);
    }

    [Test]
    public async Task Scoring_WithZeroResponses_ScoresZeroNotError()
    {
        // Arrange — instrument administered but no item responses entered
        string pid = $"MHPAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(pid);
        string mhId = await AdministerAsync(wf, "PHQ-9");

        // Act
        await wf.ScoreMentalHealthInstrumentAsync(mhId);

        // Assert — an empty response set sums to zero and interprets as MINIMAL/negative
        MentalHealthState state = await Instrument(mhId).GetInstrumentAsync();
        Assert.That(state.TotalScore, Is.EqualTo(0m));
        Assert.That(state.ScoreInterpretation, Is.EqualTo("MINIMAL"));
        Assert.That(state.IsPositiveScreen, Is.False);
        Assert.That(state.ScoringMethodUsed, Is.EqualTo("AUTO"));
    }

    [Test]
    public async Task Scoring_Twice_RecomputesWithoutDuplicatingResponses()
    {
        // Arrange — first pass: {2,2,2} = 6 (MILD)
        string pid = $"MHPAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(pid);
        string mhId = await AdministerAsync(wf, "PHQ-9");
        await AddResponsesAsync(wf, mhId, new[] { 2, 2, 2 });
        await wf.ScoreMentalHealthInstrumentAsync(mhId);

        MentalHealthState first = await Instrument(mhId).GetInstrumentAsync();
        Assert.That(first.TotalScore, Is.EqualTo(6m));

        // Act — correct item 1 from 2 to 3 (same item number must replace, not append)
        // and re-score.
        await wf.AddMentalHealthItemResponseAsync(mhId, 1, "Item 1", 3, null);
        await wf.ScoreMentalHealthInstrumentAsync(mhId);

        // Assert — score reflects the corrected response exactly once
        MentalHealthState second = await Instrument(mhId).GetInstrumentAsync();
        Assert.That(second.ItemResponses, Has.Count.EqualTo(3), "re-answering an item must replace it");
        Assert.That(second.TotalScore, Is.EqualTo(7m));
        Assert.That(second.ScoreInterpretation, Is.EqualTo("MILD"));

        // The workflow read path shows one screen, not one per scoring pass
        List<MentalHealthSummary> screens = await wf.GetMentalHealthScreensAsync();
        Assert.That(screens.Count(s => s.InstrumentId == mhId), Is.EqualTo(1));
    }

    [Test]
    public async Task Scoring_UnknownInstrument_SumsButLeavesInterpretationNull()
    {
        // Arrange — an instrument name in neither the library nor the fallback table
        string pid = $"MHPAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(pid);
        string mhId = await AdministerAsync(wf, "ZZ-UNVALIDATED-SCALE");
        await AddResponsesAsync(wf, mhId, new[] { 1, 2 });

        // Act
        await wf.ScoreMentalHealthInstrumentAsync(mhId);

        // Assert — raw total is recorded but no interpretation is invented,
        // and the scoring method says explicitly that this was only a raw sum
        // (not an AUTO-scored validated screen)
        MentalHealthState state = await Instrument(mhId).GetInstrumentAsync();
        Assert.That(state.TotalScore, Is.EqualTo(3m));
        Assert.That(state.ScoreInterpretation, Is.Null);
        Assert.That(state.IsPositiveScreen, Is.Null);
        Assert.That(state.ScoringMethodUsed,
            Is.EqualTo("RAW-SUM (no scoring definition for this instrument)"));
    }

    // ─── Trending: previous score and score change ──────────────────────────

    [Test]
    public async Task ScoreChange_WithoutPreviousScore_ReturnsNull()
    {
        // Arrange — scored current instrument, no previous score set
        string pid = $"MHPAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(pid);
        string mhId = await AdministerAsync(wf, "PHQ-9");
        await AddResponsesAsync(wf, mhId, new[] { 1, 1, 1 });
        await wf.ScoreMentalHealthInstrumentAsync(mhId);

        // Act
        decimal? change = await wf.CalculateMentalHealthScoreChangeAsync(mhId);

        // Assert
        Assert.That(change, Is.Null);
    }

    [Test]
    public async Task ScoreChange_Improvement_IsNegative()
    {
        // Arrange — current 6, previous 10: change = current - previous = -4
        string pid = $"MHPAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(pid);
        string mhId = await AdministerAsync(wf, "PHQ-9");
        await AddResponsesAsync(wf, mhId, new[] { 2, 2, 2 });
        await wf.ScoreMentalHealthInstrumentAsync(mhId);

        DateTime previousDate = new DateTime(2026, 5, 1, 9, 0, 0, DateTimeKind.Utc);
        await wf.SetMentalHealthPreviousScoreAsync(mhId, 10m, previousDate);

        // Act
        decimal? change = await wf.CalculateMentalHealthScoreChangeAsync(mhId);

        // Assert — depression improved, so the delta must be negative
        Assert.That(change, Is.EqualTo(-4m));

        MentalHealthState state = await Instrument(mhId).GetInstrumentAsync();
        Assert.That(state.PreviousScore, Is.EqualTo(10m));
        Assert.That(state.PreviousAdministrationDate, Is.EqualTo(previousDate));
    }

    [Test]
    public async Task ScoreChange_Worsening_IsPositive()
    {
        // Arrange — current 6, previous 2: change = +4
        string pid = $"MHPAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(pid);
        string mhId = await AdministerAsync(wf, "PHQ-9");
        await AddResponsesAsync(wf, mhId, new[] { 2, 2, 2 });
        await wf.ScoreMentalHealthInstrumentAsync(mhId);
        await wf.SetMentalHealthPreviousScoreAsync(mhId, 2m, DateTime.UtcNow.AddMonths(-3));

        // Act
        decimal? change = await wf.CalculateMentalHealthScoreChangeAsync(mhId);

        // Assert
        Assert.That(change, Is.EqualTo(4m));
    }

    // ─── Risk assessment and follow-up ──────────────────────────────────────

    [Test]
    public async Task RiskAssessment_Recorded_IsRetrievable()
    {
        // Arrange
        string pid = $"MHPAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(pid);
        string mhId = await AdministerAsync(wf, "C-SSRS");

        // Act
        await wf.RecordMentalHealthRiskAssessmentAsync(mhId, 3, "Suicidal ideation without plan");

        // Assert
        MentalHealthState state = await Instrument(mhId).GetInstrumentAsync();
        Assert.That(state.RiskLevel, Is.EqualTo(3));
        Assert.That(state.RiskAssessmentNotes, Is.EqualTo("Suicidal ideation without plan"));
    }

    [Test]
    public async Task FollowUp_Set_IsRetrievable()
    {
        // Arrange
        string pid = $"MHPAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(pid);
        string mhId = await AdministerAsync(wf, "PHQ-9");
        DateTime due = new DateTime(2026, 10, 1, 14, 0, 0, DateTimeKind.Utc);

        // Act
        await wf.SetMentalHealthFollowUpAsync(mhId, true, due, "Repeat PHQ-9 in 6 weeks");

        // Assert
        MentalHealthState state = await Instrument(mhId).GetInstrumentAsync();
        Assert.That(state.RequiresFollowUp, Is.True);
        Assert.That(state.FollowUpDueDate, Is.EqualTo(due));
        Assert.That(state.FollowUpPlan, Is.EqualTo("Repeat PHQ-9 in 6 weeks"));
    }

    // ─── Paged history ──────────────────────────────────────────────────────

    [Test]
    public async Task History_ReturnsAllScreensNewestFirst_AndPages()
    {
        // Arrange — three screens recorded in sequence (the history index orders
        // by append time, newest first)
        string pid = $"MHPAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(pid);
        string first = await AdministerAsync(wf, "PHQ-9", DateTime.UtcNow.AddDays(-60));
        string second = await AdministerAsync(wf, "GAD-7", DateTime.UtcNow.AddDays(-30));
        string third = await AdministerAsync(wf, "AUDIT-C", DateTime.UtcNow);

        // Act
        List<MentalHealthSummary> page = await wf.GetMentalHealthHistoryAsync(0, 10);

        // Assert — all three, newest append first
        Assert.That(page.Select(s => s.InstrumentId), Is.EqualTo(new[] { third, second, first }));

        // Paging: skip the newest, take one → the middle screen
        List<MentalHealthSummary> middle = await wf.GetMentalHealthHistoryAsync(1, 1);
        Assert.That(middle, Has.Count.EqualTo(1));
        Assert.That(middle[0].InstrumentId, Is.EqualTo(second));
        Assert.That(middle[0].InstrumentName, Is.EqualTo("GAD-7"));
    }
}

/// <summary>
/// End-to-end tests for the health factor lifecycle on
/// <see cref="IPatientWorkflowGrain"/> — record, severity update, value and
/// category assignment, resolve/reactivate, per-factor history entries, and
/// the paged patient-level history reader (File #9000010.23).
/// </summary>
[TestFixture]
public class HealthFactorLifecycleWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private IPatientWorkflowGrain Workflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private IHealthFactorGrain Factor(string healthFactorId)
        => _cluster.GrainFactory.GetGrain<IHealthFactorGrain>(healthFactorId);

    private static Task<string> RecordSmokerAsync(IPatientWorkflowGrain wf, DateTime? eventDate = null)
        => wf.RecordHealthFactorAsync(
            "CURRENT SMOKER", "TOBACCO", eventDate ?? DateTime.UtcNow,
            "MODERATE", null, "LOC-1", "Primary Care",
            "USR-1", "RIVERA,ANA", "1 pack/day");

    // ─── Full lifecycle ─────────────────────────────────────────────────────

    [Test]
    public async Task Lifecycle_RecordUpdateResolveReactivate()
    {
        // Arrange
        string pid = $"HFPAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(pid);
        string hfId = await RecordSmokerAsync(wf);

        // Act 1 — update severity; it must surface through the workflow read
        await wf.UpdateHealthFactorSeverityAsync(hfId, "HEAVY");
        List<HealthFactorSummary> factors = await wf.GetHealthFactorsAsync();
        HealthFactorSummary summary = factors.Single(f => f.HealthFactorId == hfId);
        Assert.That(summary.LevelSeverity, Is.EqualTo("HEAVY"));
        // A freshly recorded factor has no explicit evaluation status yet
        Assert.That(summary.EvaluationStatus, Is.Null);

        // Act 2 — set value and category
        await wf.SetHealthFactorValueAsync(hfId, "20", "cigarettes/day");
        await wf.SetHealthFactorCategoryAsync(hfId, "TOBACCO USE", "CURRENT SMOKER");
        HealthFactorState afterValue = await Factor(hfId).GetHealthFactorAsync();
        Assert.That(afterValue.Value, Is.EqualTo("20"));
        Assert.That(afterValue.Magnitude, Is.EqualTo("cigarettes/day"));
        Assert.That(afterValue.Category, Is.EqualTo("TOBACCO USE"));
        Assert.That(afterValue.Subcategory, Is.EqualTo("CURRENT SMOKER"));

        // Act 3 — resolve
        await wf.ResolveHealthFactorAsync(hfId, "RIVERA,ANA");
        HealthFactorState resolved = await Factor(hfId).GetHealthFactorAsync();
        Assert.That(resolved.EvaluationStatus, Is.EqualTo("RESOLVED"));
        Assert.That(resolved.ResolvedByName, Is.EqualTo("RIVERA,ANA"));
        Assert.That(resolved.ResolutionDate, Is.Not.Null);

        // A resolved factor is still returned by the workflow read (it is
        // part of the record, not deleted) — and the summary must say it is
        // RESOLVED, not render as current
        List<HealthFactorSummary> afterResolve = await wf.GetHealthFactorsAsync();
        HealthFactorSummary resolvedSummary = afterResolve.Single(f => f.HealthFactorId == hfId);
        Assert.That(resolvedSummary.EvaluationStatus, Is.EqualTo("RESOLVED"));

        // Act 4 — reactivate: active again with resolution details cleared
        await wf.ReactivateHealthFactorAsync(hfId);
        HealthFactorState reactivated = await Factor(hfId).GetHealthFactorAsync();
        Assert.That(reactivated.EvaluationStatus, Is.EqualTo("CURRENT"));
        Assert.That(reactivated.ResolutionDate, Is.Null);
        Assert.That(reactivated.ResolvedByName, Is.Null);

        // The reactivated status flows through the workflow read as well
        List<HealthFactorSummary> afterReactivate = await wf.GetHealthFactorsAsync();
        Assert.That(afterReactivate.Single(f => f.HealthFactorId == hfId).EvaluationStatus,
            Is.EqualTo("CURRENT"));
    }

    [Test]
    public async Task Resolve_Twice_RemainsResolved()
    {
        // Arrange
        string pid = $"HFPAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(pid);
        string hfId = await RecordSmokerAsync(wf);

        // Act — resolving an already-resolved factor is a harmless overwrite
        await wf.ResolveHealthFactorAsync(hfId, "RIVERA,ANA");
        await wf.ResolveHealthFactorAsync(hfId, "CHEN,MICHAEL");

        // Assert — last writer wins; status stays RESOLVED
        HealthFactorState state = await Factor(hfId).GetHealthFactorAsync();
        Assert.That(state.EvaluationStatus, Is.EqualTo("RESOLVED"));
        Assert.That(state.ResolvedByName, Is.EqualTo("CHEN,MICHAEL"));
        Assert.That(state.ResolutionDate, Is.Not.Null);
    }

    [Test]
    public async Task Reactivate_NeverResolvedFactor_MarksCurrent()
    {
        // Arrange — a freshly recorded factor has no explicit evaluation status
        string pid = $"HFPAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(pid);
        string hfId = await RecordSmokerAsync(wf);

        // Act — reactivating an active factor must not throw or corrupt state
        await wf.ReactivateHealthFactorAsync(hfId);

        // Assert
        HealthFactorState state = await Factor(hfId).GetHealthFactorAsync();
        Assert.That(state.EvaluationStatus, Is.EqualTo("CURRENT"));
        Assert.That(state.ResolutionDate, Is.Null);
    }

    // ─── Per-factor history entries ─────────────────────────────────────────

    [Test]
    public async Task HistoryEntries_GrowMonotonically_InInsertionOrder()
    {
        // Arrange
        string pid = $"HFPAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(pid);
        string hfId = await RecordSmokerAsync(wf);

        // Act — three longitudinal observations
        await wf.AddHealthFactorHistoryEntryAsync(hfId, "20", "HEAVY", "Baseline", "RIVERA,ANA");
        await wf.AddHealthFactorHistoryEntryAsync(hfId, "10", "MODERATE", "Cutting down", "RIVERA,ANA");
        await wf.AddHealthFactorHistoryEntryAsync(hfId, "0", "NONE", "Quit", "CHEN,MICHAEL");

        // Assert — entries accumulate (never replace) and keep insertion order
        List<HealthFactorHistoryEntry> history = await Factor(hfId).GetHistoryAsync();
        Assert.That(history, Has.Count.EqualTo(3));
        Assert.That(history.Select(h => h.Value), Is.EqualTo(new[] { "20", "10", "0" }));
        Assert.That(history[0].SeverityLevel, Is.EqualTo("HEAVY"));
        Assert.That(history[2].Comment, Is.EqualTo("Quit"));
        Assert.That(history[2].RecordedByName, Is.EqualTo("CHEN,MICHAEL"));
        Assert.That(history.Select(h => h.EntryDate), Is.Ordered.Ascending);

        // One more entry grows the list — nothing is trimmed or overwritten
        await wf.AddHealthFactorHistoryEntryAsync(hfId, "0", "NONE", "Still abstinent", "CHEN,MICHAEL");
        List<HealthFactorHistoryEntry> grown = await Factor(hfId).GetHistoryAsync();
        Assert.That(grown, Has.Count.EqualTo(4));
    }

    // ─── Paged patient-level history ────────────────────────────────────────

    [Test]
    public async Task History_ReturnsAllFactorsNewestFirst_AndPages()
    {
        // Arrange — three factors recorded in sequence
        string pid = $"HFPAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(pid);
        string first = await wf.RecordHealthFactorAsync(
            "CURRENT SMOKER", "TOBACCO", DateTime.UtcNow.AddDays(-90),
            "HEAVY", null, null, null, "USR-1", "RIVERA,ANA", null);
        string second = await wf.RecordHealthFactorAsync(
            "ALCOHOL USE", "SUBSTANCE", DateTime.UtcNow.AddDays(-45),
            "MODERATE", null, null, null, "USR-1", "RIVERA,ANA", null);
        string third = await wf.RecordHealthFactorAsync(
            "SEDENTARY LIFESTYLE", "LIFESTYLE", DateTime.UtcNow,
            null, null, null, null, "USR-2", "CHEN,MICHAEL", null);

        // Act
        List<HealthFactorSummary> page = await wf.GetHealthFactorHistoryAsync(0, 10);

        // Assert — all three, newest append first
        Assert.That(page.Select(f => f.HealthFactorId), Is.EqualTo(new[] { third, second, first }));

        // Paging: skip the newest, take one → the middle factor
        List<HealthFactorSummary> middle = await wf.GetHealthFactorHistoryAsync(1, 1);
        Assert.That(middle, Has.Count.EqualTo(1));
        Assert.That(middle[0].HealthFactorId, Is.EqualTo(second));
        Assert.That(middle[0].HealthFactorName, Is.EqualTo("ALCOHOL USE"));
        Assert.That(middle[0].Category, Is.EqualTo("SUBSTANCE"));
    }
}
