// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans.TestingHost;
using NewVistas.Abstractions.Events;
using NewVistas.Abstractions.Events.Clinical.Allergies;
using NewVistas.Abstractions.Events.Clinical.MentalHealth;
using NewVistas.Abstractions.EventSourcing;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Event-sourcing tests for the ALLERGIES and MENTAL_HEALTH domains.
/// ALLERGIES is embedded in PatientGrain (single AllergyRecordedV1 event).
/// MENTAL_HEALTH is its own grain with three events (record / risk / score)
/// — replay must reproduce the live instrument state including risk and score.
/// </summary>
[TestFixture]
public class AllergyMentalHealthEventSourcingTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private IPatientGrain Patient(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);

    private IMentalHealthGrain Mh(string instrumentId) =>
        _cluster.GrainFactory.GetGrain<IMentalHealthGrain>(instrumentId);

    private IPatientClinicalEventStreamGrain Stream(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientClinicalEventStreamGrain>(patientId);

    // ── ALLERGIES ─────────────────────────────────────────────────────────

    [Test]
    public async Task AddAllergy_EmitsAllergyRecordedV1()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        var entry = new AllergyEntry
        {
            AllergyId = $"ALG-{Guid.NewGuid()}",
            Allergen = "PENICILLIN",
            AllergenType = "DRUG",
            ReactionType = "ALLERGY",
            Reactions = new List<string> { "RASH", "ITCHING" },
            Severity = "MODERATE",
            ObservedHistorical = "O",
            OriginatorId = "USR-1",
            OriginatorName = "Smith,Jane"
        };

        await Patient(patientId).AddAllergyAsync(entry);
        await WaitForStreamVersionAsync(patientId, expected: 1);

        IReadOnlyList<EventEnvelope> events = await Stream(patientId).ReadAsync(0, 10);
        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0].EventType, Is.EqualTo(nameof(AllergyRecordedV1)));
        Assert.That(events[0].Domain, Is.EqualTo("ALLERGIES"));

        var payload = events[0].Payload as AllergyRecordedV1;
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.AllergyId, Is.EqualTo(entry.AllergyId));
        Assert.That(payload.Snapshot.Allergen, Is.EqualTo("PENICILLIN"));
        Assert.That(payload.Snapshot.Reactions, Has.Count.EqualTo(2));

        Assert.That(await Stream(patientId).VerifyChainAsync(), Is.True);
    }

    [Test]
    public async Task AddAllergy_DuplicateAllergyId_NoSecondEvent()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        var entry = new AllergyEntry
        {
            AllergyId = $"ALG-{Guid.NewGuid()}",
            Allergen = "PEANUTS",
            AllergenType = "FOOD"
        };

        await Patient(patientId).AddAllergyAsync(entry);
        await WaitForStreamVersionAsync(patientId, expected: 1);

        await Patient(patientId).AddAllergyAsync(entry); // dup
        await Task.Delay(150);

        Assert.That(await Stream(patientId).GetVersionAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task ReplayUntilAsync_AfterAllergyRecorded_RebuildsLiveState()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        var entry = new AllergyEntry
        {
            AllergyId = $"ALG-{Guid.NewGuid()}",
            Allergen = "LATEX",
            AllergenType = "OTHER",
            Severity = "SEVERE",
            Reactions = new List<string> { "ANAPHYLAXIS" }
        };
        await Patient(patientId).AddAllergyAsync(entry);
        await WaitForStreamVersionAsync(patientId, expected: 1);

        PatientStateSnapshot replayed =
            await Stream(patientId).ReplayUntilAsync(DateTime.UtcNow);
        Assert.That(replayed.Allergies, Has.Count.EqualTo(1));
        Assert.That(replayed.Allergies[0].Allergen, Is.EqualTo("LATEX"));
        Assert.That(replayed.Allergies[0].Reactions, Contains.Item("ANAPHYLAXIS"));
    }

    // ── MENTAL_HEALTH ─────────────────────────────────────────────────────

    private async Task<(string patientId, string instrumentId)> RecordPhq9Async(
        string patientId, decimal? totalScore = null,
        Dictionary<string, string>? responses = null)
    {
        string instrumentId = $"MH-{Guid.NewGuid()}";
        await Mh(instrumentId).RecordInstrumentAsync(
            patientId, "PHQ-9", "INSTR-PHQ9",
            DateTime.UtcNow, totalScore,
            null, null,
            responses,
            "PROV-1", "Smith,Jane",
            "PROV-1", "Smith,Jane",
            "LOC-1", "Clinic A",
            null, null);
        await WaitForStreamVersionAsync(patientId, expected: 1);
        return (patientId, instrumentId);
    }

    [Test]
    public async Task RecordMentalHealthInstrument_EmitsMentalHealthRecordedV1()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        var (_, instrumentId) = await RecordPhq9Async(patientId, totalScore: 12);

        IReadOnlyList<EventEnvelope> events = await Stream(patientId).ReadAsync(0, 10);
        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0].EventType, Is.EqualTo(nameof(MentalHealthRecordedV1)));
        Assert.That(events[0].Domain, Is.EqualTo("MENTAL_HEALTH"));

        var payload = events[0].Payload as MentalHealthRecordedV1;
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.InstrumentId, Is.EqualTo(instrumentId));
        Assert.That(payload.Snapshot.InstrumentName, Is.EqualTo("PHQ-9"));
        Assert.That(payload.Snapshot.TotalScore, Is.EqualTo(12));
    }

    [Test]
    public async Task RecordMentalHealthInstrument_Idempotent_OnSecondCall()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        var (_, instrumentId) = await RecordPhq9Async(patientId);

        // Second record on same grain key with different data — should be a no-op.
        await Mh(instrumentId).RecordInstrumentAsync(
            "OTHER-PAT", "GAD-7", null, DateTime.UtcNow, 99,
            null, null, null, null, null, null, null, null, null, null, null);
        await Task.Delay(150);

        Assert.That(await Stream(patientId).GetVersionAsync(), Is.EqualTo(1));
        MentalHealthState live = await Mh(instrumentId).GetInstrumentAsync();
        Assert.That(live.PatientId, Is.EqualTo(patientId));
        Assert.That(live.InstrumentName, Is.EqualTo("PHQ-9"));
    }

    [Test]
    public async Task RecordRiskAssessment_EmitsMentalHealthRiskAssessedV1()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        var (_, instrumentId) = await RecordPhq9Async(patientId);

        await Mh(instrumentId).RecordRiskAssessmentAsync(
            riskLevel: 3, riskNotes: "Active suicidal ideation, no plan");
        await WaitForStreamVersionAsync(patientId, expected: 2);

        IReadOnlyList<EventEnvelope> events = await Stream(patientId).ReadAsync(0, 10);
        Assert.That(events[1].EventType, Is.EqualTo(nameof(MentalHealthRiskAssessedV1)));
        var payload = events[1].Payload as MentalHealthRiskAssessedV1;
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.InstrumentId, Is.EqualTo(instrumentId));
        Assert.That(payload.RiskLevel, Is.EqualTo(3));
        Assert.That(payload.RiskNotes, Does.Contain("ideation"));

        Assert.That(await Stream(patientId).VerifyChainAsync(), Is.True);
    }

    [Test]
    public async Task ScoreInstrument_EmitsMentalHealthScoredV1()
    {
        // Score uses ItemResponses, so add several first.
        string patientId = $"PAT-{Guid.NewGuid()}";
        var (_, instrumentId) = await RecordPhq9Async(patientId);

        for (int i = 1; i <= 9; i++)
            await Mh(instrumentId).AddItemResponseAsync(i, $"Item {i}", responseValue: 2, null);

        await Mh(instrumentId).ScoreInstrumentAsync();
        // Wait long enough for the score event (the prior AddItemResponseAsync calls
        // don't emit causal events in this slice, so version count is 1 (record) + 1 (score) = 2.
        await WaitForStreamVersionAsync(patientId, expected: 2);

        IReadOnlyList<EventEnvelope> events = await Stream(patientId).ReadAsync(0, 10);
        Assert.That(events[1].EventType, Is.EqualTo(nameof(MentalHealthScoredV1)));
        var payload = events[1].Payload as MentalHealthScoredV1;
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.TotalScore, Is.EqualTo(18));   // 9 items × 2
        Assert.That(payload.ScoringMethod, Is.EqualTo("AUTO"));
    }

    [Test]
    public async Task ReplayUntilAsync_AfterFullMHLifecycle_ReproducesLiveState()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        var (_, instrumentId) = await RecordPhq9Async(patientId);

        await Mh(instrumentId).RecordRiskAssessmentAsync(2, "Mild concern, monitoring");
        for (int i = 1; i <= 9; i++)
            await Mh(instrumentId).AddItemResponseAsync(i, $"Item {i}", responseValue: 1, null);
        await Mh(instrumentId).ScoreInstrumentAsync();
        await WaitForStreamVersionAsync(patientId, expected: 3);

        PatientStateSnapshot replayed =
            await Stream(patientId).ReplayUntilAsync(DateTime.UtcNow);
        Assert.That(replayed.MentalHealthInstruments, Has.Count.EqualTo(1));

        MentalHealthState fromChain = replayed.MentalHealthInstruments[0];
        Assert.That(fromChain.InstrumentId, Is.EqualTo(instrumentId));
        Assert.That(fromChain.InstrumentName, Is.EqualTo("PHQ-9"));
        Assert.That(fromChain.RiskLevel, Is.EqualTo(2));
        Assert.That(fromChain.TotalScore, Is.EqualTo(9));
        Assert.That(fromChain.ScoringMethodUsed, Is.EqualTo("AUTO"));
    }

    [Test]
    public async Task ReplayUntilAsync_PointInTime_BeforeScoring_ShowsUnscored()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        var (_, instrumentId) = await RecordPhq9Async(patientId);

        for (int i = 1; i <= 9; i++)
            await Mh(instrumentId).AddItemResponseAsync(i, $"Item {i}", responseValue: 2, null);

        await Task.Delay(150);
        DateTime tBeforeScore = DateTime.UtcNow;
        await Task.Delay(150);

        await Mh(instrumentId).ScoreInstrumentAsync();
        await WaitForStreamVersionAsync(patientId, expected: 2);

        PatientStateSnapshot before =
            await Stream(patientId).ReplayUntilAsync(tBeforeScore);
        Assert.That(before.MentalHealthInstruments[0].TotalScore, Is.Null);

        PatientStateSnapshot after =
            await Stream(patientId).ReplayUntilAsync(DateTime.UtcNow);
        Assert.That(after.MentalHealthInstruments[0].TotalScore, Is.EqualTo(18));
    }

    // ── helpers ───────────────────────────────────────────────────────────

    private async Task WaitForStreamVersionAsync(
        string patientId, int expected, int timeoutMs = 5000)
    {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        IPatientClinicalEventStreamGrain stream = Stream(patientId);
        while (DateTime.UtcNow < deadline)
        {
            int v = await stream.GetVersionAsync();
            if (v >= expected) return;
            await Task.Delay(50);
        }
        int finalVersion = await stream.GetVersionAsync();
        Assert.Fail(
            $"Stream for {patientId} did not reach version {expected} within {timeoutMs}ms (current={finalVersion}).");
    }
}
