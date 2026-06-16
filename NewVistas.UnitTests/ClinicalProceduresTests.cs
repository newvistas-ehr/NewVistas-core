// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

// ═══════════════════════════════════════════════════════════════════════════
// ClinicProcedureGrainTests
// ═══════════════════════════════════════════════════════════════════════════

[TestFixture]
public class ClinicProcedureGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IClinicProcedureGrain NewProc() =>
        _cluster.GrainFactory.GetGrain<IClinicProcedureGrain>($"CP-PROC:{Guid.NewGuid()}");

    // ── Order / Basic ──────────────────────────────────────────────────────

    [Test]
    public async Task ClinicProcedureGrain_CanOrderProcedure()
    {
        IClinicProcedureGrain grain = NewProc();
        DateTime ordered = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        await grain.OrderProcedureAsync(
            "PAT-001", ClinicProcedureCategory.EEG,
            "95819", "EEG with sleep, awake and drowsy",
            ordered, "PROV-001", "Dr. Neuro", "LOC-NEURO", "Neurology Lab",
            "Seizure evaluation");

        ClinicProcedureState state = await grain.GetProcedureAsync();

        Assert.That(state.PatientId, Is.EqualTo("PAT-001"));
        Assert.That(state.Category, Is.EqualTo(ClinicProcedureCategory.EEG));
        Assert.That(state.ProcedureCode, Is.EqualTo("95819"));
        Assert.That(state.ProcedureDescription, Is.EqualTo("EEG with sleep, awake and drowsy"));
        Assert.That(state.OrderedDate, Is.EqualTo(ordered));
        Assert.That(state.ProviderName, Is.EqualTo("Dr. Neuro"));
        Assert.That(state.LocationName, Is.EqualTo("Neurology Lab"));
        Assert.That(state.Indication, Is.EqualTo("Seizure evaluation"));
        Assert.That(state.Status, Is.EqualTo(ClinicProcedureStatus.Ordered));
    }

    [Test]
    public async Task ClinicProcedureGrain_ProcedureId_MatchesGrainKey()
    {
        string key = $"CP-PROC:{Guid.NewGuid()}";
        IClinicProcedureGrain grain = _cluster.GrainFactory.GetGrain<IClinicProcedureGrain>(key);
        await grain.OrderProcedureAsync(
            "PAT-002", ClinicProcedureCategory.EMG,
            "95860", "EMG, one extremity", DateTime.UtcNow,
            null, null, null, null, null);

        ClinicProcedureState state = await grain.GetProcedureAsync();
        Assert.That(state.ProcedureId, Is.EqualTo(key));
    }

    [Test]
    public async Task ClinicProcedureGrain_DefaultStatus_IsOrdered()
    {
        IClinicProcedureGrain grain = NewProc();
        await grain.OrderProcedureAsync(
            "PAT-003", ClinicProcedureCategory.SleepStudy,
            "95810", "Polysomnography", DateTime.UtcNow,
            null, null, null, null, null);

        ClinicProcedureState state = await grain.GetProcedureAsync();
        Assert.That(state.Status, Is.EqualTo(ClinicProcedureStatus.Ordered));
    }

    // ── Schedule ───────────────────────────────────────────────────────────

    [Test]
    public async Task ClinicProcedureGrain_ScheduleProcedure_SetsScheduledDateAndStatus()
    {
        IClinicProcedureGrain grain = NewProc();
        await grain.OrderProcedureAsync(
            "PAT-010", ClinicProcedureCategory.Audiometry,
            "92557", "Audiometry comprehensive", DateTime.UtcNow,
            null, null, null, null, null);

        DateTime scheduled = new DateTime(2025, 7, 15, 9, 0, 0, DateTimeKind.Utc);
        await grain.ScheduleProcedureAsync(scheduled);

        ClinicProcedureState state = await grain.GetProcedureAsync();
        Assert.That(state.ScheduledDate, Is.EqualTo(scheduled));
        Assert.That(state.Status, Is.EqualTo(ClinicProcedureStatus.Scheduled));
    }

    // ── Complete ───────────────────────────────────────────────────────────

    [Test]
    public async Task ClinicProcedureGrain_CompleteProcedure_SetsAllFields()
    {
        IClinicProcedureGrain grain = NewProc();
        await grain.OrderProcedureAsync(
            "PAT-020", ClinicProcedureCategory.EEG,
            "95819", "Routine EEG", DateTime.UtcNow,
            "PROV-001", "Dr. Neuro", null, null, null);

        DateTime performed = new DateTime(2025, 7, 20, 10, 0, 0, DateTimeKind.Utc);
        await grain.CompleteProcedureAsync(performed,
            "Alpha rhythm present at 10 Hz. No epileptiform discharges.",
            "Normal awake and drowsy EEG.",
            "Patient cooperative throughout.");

        ClinicProcedureState state = await grain.GetProcedureAsync();
        Assert.That(state.Status, Is.EqualTo(ClinicProcedureStatus.Completed));
        Assert.That(state.PerformedDate, Is.EqualTo(performed));
        Assert.That(state.Findings, Does.Contain("Alpha rhythm"));
        Assert.That(state.Impression, Is.EqualTo("Normal awake and drowsy EEG."));
        Assert.That(state.Notes, Does.Contain("cooperative"));
    }

    // ── Cancel ─────────────────────────────────────────────────────────────

    [Test]
    public async Task ClinicProcedureGrain_CancelProcedure_SetsStatusAndReason()
    {
        IClinicProcedureGrain grain = NewProc();
        await grain.OrderProcedureAsync(
            "PAT-030", ClinicProcedureCategory.NerveConduction,
            "95910", "NCS, 7-8 nerves", DateTime.UtcNow,
            null, null, null, null, null);

        await grain.CancelProcedureAsync("Patient declined");

        ClinicProcedureState state = await grain.GetProcedureAsync();
        Assert.That(state.Status, Is.EqualTo(ClinicProcedureStatus.Cancelled));
        Assert.That(state.CancellationReason, Is.EqualTo("Patient declined"));
    }

    [Test]
    public async Task ClinicProcedureGrain_CancelProcedure_NullReasonAllowed()
    {
        IClinicProcedureGrain grain = NewProc();
        await grain.OrderProcedureAsync(
            "PAT-031", ClinicProcedureCategory.Other,
            "00000", "Other", DateTime.UtcNow,
            null, null, null, null, null);

        await grain.CancelProcedureAsync(null);

        ClinicProcedureState state = await grain.GetProcedureAsync();
        Assert.That(state.Status, Is.EqualTo(ClinicProcedureStatus.Cancelled));
        Assert.That(state.CancellationReason, Is.Null);
    }

    // ── EEG Results ────────────────────────────────────────────────────────

    [Test]
    public async Task ClinicProcedureGrain_RecordEegResults_NormalStudy()
    {
        IClinicProcedureGrain grain = NewProc();
        await grain.OrderProcedureAsync(
            "PAT-040", ClinicProcedureCategory.EEG,
            "95819", "Routine EEG", DateTime.UtcNow,
            null, null, null, null, null);

        await grain.RecordEegResultsAsync(
            durationMinutes: 30,
            background: "Normal posterior dominant rhythm at 10 Hz",
            alertType: EegAlertType.Normal,
            seizureActivity: false,
            focalRegion: null,
            activations: new List<string> { "Hyperventilation", "Photic stimulation" });

        ClinicProcedureState state = await grain.GetProcedureAsync();
        Assert.That(state.EegDurationMinutes, Is.EqualTo(30));
        Assert.That(state.EegBackground, Does.Contain("10 Hz"));
        Assert.That(state.EegAlertType, Is.EqualTo(EegAlertType.Normal));
        Assert.That(state.EegSeizureActivity, Is.False);
        Assert.That(state.EegFocalRegion, Is.Null);
        Assert.That(state.EegActivations, Has.Count.EqualTo(2));
        Assert.That(state.EegActivations, Contains.Item("Hyperventilation"));
    }

    [Test]
    public async Task ClinicProcedureGrain_RecordEegResults_SeizureActivity()
    {
        IClinicProcedureGrain grain = NewProc();
        await grain.OrderProcedureAsync(
            "PAT-041", ClinicProcedureCategory.EEG,
            "95819", "Routine EEG", DateTime.UtcNow,
            null, null, null, null, null);

        await grain.RecordEegResultsAsync(
            durationMinutes: 60,
            background: "Diffuse slowing with intermittent left temporal sharp waves",
            alertType: EegAlertType.AbnormalFocal,
            seizureActivity: true,
            focalRegion: "Left temporal (T3-T5)",
            activations: null);

        ClinicProcedureState state = await grain.GetProcedureAsync();
        Assert.That(state.EegAlertType, Is.EqualTo(EegAlertType.AbnormalFocal));
        Assert.That(state.EegSeizureActivity, Is.True);
        Assert.That(state.EegFocalRegion, Is.EqualTo("Left temporal (T3-T5)"));
        Assert.That(state.EegActivations, Is.Empty);
    }

    // ── EMG Results ────────────────────────────────────────────────────────

    [Test]
    public async Task ClinicProcedureGrain_RecordEmgResults_NormalStudy()
    {
        IClinicProcedureGrain grain = NewProc();
        await grain.OrderProcedureAsync(
            "PAT-050", ClinicProcedureCategory.EMG,
            "95860", "EMG one extremity", DateTime.UtcNow,
            null, null, null, null, null);

        var muscles = new List<string> { "Right biceps brachii", "Right deltoid", "Right first dorsal interosseous" };
        await grain.RecordEmgResultsAsync(
            musclesStudied: muscles,
            findingType: EmgFindingType.Normal,
            spontaneousActivity: "No fibrillation potentials or positive sharp waves",
            mupDescription: "Normal morphology, recruitment, and interference pattern");

        ClinicProcedureState state = await grain.GetProcedureAsync();
        Assert.That(state.EmgFindingType, Is.EqualTo(EmgFindingType.Normal));
        Assert.That(state.EmgMusclesStudied, Has.Count.EqualTo(3));
        Assert.That(state.EmgMusclesStudied, Contains.Item("Right biceps brachii"));
        Assert.That(state.EmgSpontaneousActivity, Does.Contain("fibrillation"));
        Assert.That(state.EmgMupDescription, Does.Contain("Normal morphology"));
    }

    [Test]
    public async Task ClinicProcedureGrain_RecordEmgResults_MyopathyPattern()
    {
        IClinicProcedureGrain grain = NewProc();
        await grain.OrderProcedureAsync(
            "PAT-051", ClinicProcedureCategory.EMG,
            "95860", "EMG one extremity", DateTime.UtcNow,
            null, null, null, null, null);

        await grain.RecordEmgResultsAsync(
            musclesStudied: new List<string> { "Left vastus lateralis", "Left tibialis anterior" },
            findingType: EmgFindingType.Myopathy,
            spontaneousActivity: "Occasional fibrillation potentials",
            mupDescription: "Short-duration, polyphasic MUPs with early recruitment");

        ClinicProcedureState state = await grain.GetProcedureAsync();
        Assert.That(state.EmgFindingType, Is.EqualTo(EmgFindingType.Myopathy));
        Assert.That(state.EmgMusclesStudied, Has.Count.EqualTo(2));
    }

    // ── NCS Results ────────────────────────────────────────────────────────

    [Test]
    public async Task ClinicProcedureGrain_RecordNcsResults_Normal()
    {
        IClinicProcedureGrain grain = NewProc();
        await grain.OrderProcedureAsync(
            "PAT-060", ClinicProcedureCategory.NerveConduction,
            "95910", "NCS 7-8 nerves", DateTime.UtcNow,
            null, null, null, null, null);

        var nerves = new List<string> { "Right median motor", "Right ulnar motor", "Right median sensory", "Right ulnar sensory" };
        await grain.RecordNcsResultsAsync(
            nervesStudied: nerves,
            meanMotorVelocity: 55.2m,
            meanSensoryVelocity: 51.8m,
            fWavesObtained: true,
            findingType: EmgFindingType.Normal);

        ClinicProcedureState state = await grain.GetProcedureAsync();
        Assert.That(state.NcsNervesStudied, Has.Count.EqualTo(4));
        Assert.That(state.NcsMeanMotorVelocity, Is.EqualTo(55.2m));
        Assert.That(state.NcsMeanSensoryVelocity, Is.EqualTo(51.8m));
        Assert.That(state.NcsFWavesObtained, Is.True);
        Assert.That(state.NcsFindingType, Is.EqualTo(EmgFindingType.Normal));
    }

    [Test]
    public async Task ClinicProcedureGrain_RecordNcsResults_Neuropathy()
    {
        IClinicProcedureGrain grain = NewProc();
        await grain.OrderProcedureAsync(
            "PAT-061", ClinicProcedureCategory.NerveConduction,
            "95910", "NCS 7-8 nerves", DateTime.UtcNow,
            null, null, null, null, null);

        await grain.RecordNcsResultsAsync(
            nervesStudied: new List<string> { "Right peroneal motor", "Right tibial motor", "Right sural sensory" },
            meanMotorVelocity: 32.1m,
            meanSensoryVelocity: null,
            fWavesObtained: false,
            findingType: EmgFindingType.Neuropathy);

        ClinicProcedureState state = await grain.GetProcedureAsync();
        Assert.That(state.NcsFindingType, Is.EqualTo(EmgFindingType.Neuropathy));
        Assert.That(state.NcsMeanMotorVelocity, Is.EqualTo(32.1m));
        Assert.That(state.NcsMeanSensoryVelocity, Is.Null);
        Assert.That(state.NcsFWavesObtained, Is.False);
    }

    // ── Sleep Study Results ────────────────────────────────────────────────

    [Test]
    public async Task ClinicProcedureGrain_RecordSleepStudyResults_ObstructiveSleepApnea()
    {
        IClinicProcedureGrain grain = NewProc();
        await grain.OrderProcedureAsync(
            "PAT-070", ClinicProcedureCategory.SleepStudy,
            "95810", "Polysomnography", DateTime.UtcNow,
            null, "Dr. Sleep", null, "Sleep Lab", "Snoring, daytime sleepiness");

        await grain.RecordSleepStudyResultsAsync(
            studyType: SleepStudyType.Diagnostic,
            apneaType: SleepApneaType.Obstructive,
            apneaHypopneaIndex: 28.4m,
            cpapPressureCmH2O: null,
            sleepEfficiencyPct: 72.5m,
            totalSleepTimeMin: 362,
            sleepLatencyMin: 8.2m,
            remLatencyMin: 95.0m);

        ClinicProcedureState state = await grain.GetProcedureAsync();
        Assert.That(state.SleepStudyType, Is.EqualTo(SleepStudyType.Diagnostic));
        Assert.That(state.SleepApneaType, Is.EqualTo(SleepApneaType.Obstructive));
        Assert.That(state.ApneaHypopneaIndex, Is.EqualTo(28.4m));
        Assert.That(state.SleepEfficiencyPct, Is.EqualTo(72.5m));
        Assert.That(state.TotalSleepTimeMin, Is.EqualTo(362));
        Assert.That(state.SleepLatencyMin, Is.EqualTo(8.2m));
        Assert.That(state.RemLatencyMin, Is.EqualTo(95.0m));
        Assert.That(state.CpapPressureCmH2O, Is.Null);
    }

    [Test]
    public async Task ClinicProcedureGrain_RecordSleepStudyResults_CpapTitration()
    {
        IClinicProcedureGrain grain = NewProc();
        await grain.OrderProcedureAsync(
            "PAT-071", ClinicProcedureCategory.SleepStudy,
            "95811", "CPAP titration", DateTime.UtcNow,
            null, null, null, null, null);

        await grain.RecordSleepStudyResultsAsync(
            studyType: SleepStudyType.CpapTitration,
            apneaType: SleepApneaType.Obstructive,
            apneaHypopneaIndex: 1.2m,
            cpapPressureCmH2O: 12.0m,
            sleepEfficiencyPct: 86.3m,
            totalSleepTimeMin: 415,
            sleepLatencyMin: 5.0m,
            remLatencyMin: 75.0m);

        ClinicProcedureState state = await grain.GetProcedureAsync();
        Assert.That(state.SleepStudyType, Is.EqualTo(SleepStudyType.CpapTitration));
        Assert.That(state.CpapPressureCmH2O, Is.EqualTo(12.0m));
        Assert.That(state.ApneaHypopneaIndex, Is.EqualTo(1.2m));
    }

    // ── Audiometry Results ─────────────────────────────────────────────────

    [Test]
    public async Task ClinicProcedureGrain_RecordAudiometryResults_Sensorineural()
    {
        IClinicProcedureGrain grain = NewProc();
        await grain.OrderProcedureAsync(
            "PAT-080", ClinicProcedureCategory.Audiometry,
            "92557", "Comprehensive audiometry", DateTime.UtcNow,
            null, "Dr. Audio", null, "Audiology Clinic", "Hearing loss evaluation");

        await grain.RecordAudiometryResultsAsync(
            hearingLossType: HearingLossType.Sensorineural,
            rightEarPta: 38.3m,
            leftEarPta: 42.5m,
            speechDiscriminationRight: 88.0m,
            speechDiscriminationLeft: 84.0m,
            tympanometryRight: "A",
            tympanometryLeft: "A");

        ClinicProcedureState state = await grain.GetProcedureAsync();
        Assert.That(state.HearingLossType, Is.EqualTo(HearingLossType.Sensorineural));
        Assert.That(state.RightEarPta, Is.EqualTo(38.3m));
        Assert.That(state.LeftEarPta, Is.EqualTo(42.5m));
        Assert.That(state.SpeechDiscriminationRight, Is.EqualTo(88.0m));
        Assert.That(state.SpeechDiscriminationLeft, Is.EqualTo(84.0m));
        Assert.That(state.TympanometryRight, Is.EqualTo("A"));
        Assert.That(state.TympanometryLeft, Is.EqualTo("A"));
    }

    [Test]
    public async Task ClinicProcedureGrain_RecordAudiometryResults_NormalHearing()
    {
        IClinicProcedureGrain grain = NewProc();
        await grain.OrderProcedureAsync(
            "PAT-081", ClinicProcedureCategory.Audiometry,
            "92557", "Comprehensive audiometry", DateTime.UtcNow,
            null, null, null, null, null);

        await grain.RecordAudiometryResultsAsync(
            hearingLossType: HearingLossType.None,
            rightEarPta: 12.0m,
            leftEarPta: 10.0m,
            speechDiscriminationRight: 100.0m,
            speechDiscriminationLeft: 100.0m,
            tympanometryRight: "A",
            tympanometryLeft: "A");

        ClinicProcedureState state = await grain.GetProcedureAsync();
        Assert.That(state.HearingLossType, Is.EqualTo(HearingLossType.None));
        Assert.That(state.RightEarPta, Is.EqualTo(12.0m));
    }

    // ── LastModifiedDate ───────────────────────────────────────────────────

    [Test]
    public async Task ClinicProcedureGrain_LastModifiedDate_UpdatesOnWrite()
    {
        IClinicProcedureGrain grain = NewProc();
        await grain.OrderProcedureAsync(
            "PAT-090", ClinicProcedureCategory.EEG,
            "95819", "Routine EEG", DateTime.UtcNow,
            null, null, null, null, null);

        ClinicProcedureState before = await grain.GetProcedureAsync();
        DateTime beforeModified = before.LastModifiedDate;

        await Task.Delay(10);
        await grain.CompleteProcedureAsync(
            DateTime.UtcNow, "Normal EEG", "Normal", null);

        ClinicProcedureState after = await grain.GetProcedureAsync();
        Assert.That(after.LastModifiedDate, Is.GreaterThan(beforeModified));
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// ClinicProcedureIndexGrainTests
// ═══════════════════════════════════════════════════════════════════════════

[TestFixture]
public class ClinicProcedureIndexGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IClinicProcedureIndexGrain NewIndex() =>
        _cluster.GrainFactory.GetGrain<IClinicProcedureIndexGrain>($"CP-PROC-IDX:{Guid.NewGuid()}");

    private static ClinicProcedureIndexEntry MakeEntry(string id, ClinicProcedureCategory cat, ClinicProcedureStatus status, DateTime ordered) =>
        new()
        {
            ProcedureId = id,
            Category = cat,
            ProcedureCode = "00000",
            ProcedureDescription = "Test Procedure",
            Status = status,
            OrderedDate = ordered,
            PerformedDate = status == ClinicProcedureStatus.Completed ? ordered.AddDays(1) : null,
            ProviderName = "Dr. Test",
            LocationName = "Test Lab",
            Impression = status == ClinicProcedureStatus.Completed ? "Normal" : null
        };

    [Test]
    public async Task ClinicProcedureIndexGrain_StartsEmpty()
    {
        IClinicProcedureIndexGrain index = NewIndex();
        List<ClinicProcedureIndexEntry> all = await index.GetAllProceduresAsync();
        Assert.That(all, Is.Empty);
    }

    [Test]
    public async Task ClinicProcedureIndexGrain_UpsertAndRetrieve()
    {
        IClinicProcedureIndexGrain index = NewIndex();
        ClinicProcedureIndexEntry entry = MakeEntry("CP-PROC-A", ClinicProcedureCategory.EEG,
            ClinicProcedureStatus.Ordered, DateTime.UtcNow);

        await index.UpsertProcedureAsync(entry);

        List<ClinicProcedureIndexEntry> all = await index.GetAllProceduresAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].ProcedureId, Is.EqualTo("CP-PROC-A"));
    }

    [Test]
    public async Task ClinicProcedureIndexGrain_UpsertUpdatesExisting()
    {
        IClinicProcedureIndexGrain index = NewIndex();
        string id = $"CP-PROC:{Guid.NewGuid()}";
        ClinicProcedureIndexEntry original = MakeEntry(id, ClinicProcedureCategory.EMG,
            ClinicProcedureStatus.Ordered, DateTime.UtcNow);
        await index.UpsertProcedureAsync(original);

        ClinicProcedureIndexEntry updated = MakeEntry(id, ClinicProcedureCategory.EMG,
            ClinicProcedureStatus.Completed, original.OrderedDate);
        await index.UpsertProcedureAsync(updated);

        List<ClinicProcedureIndexEntry> all = await index.GetAllProceduresAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].Status, Is.EqualTo(ClinicProcedureStatus.Completed));
    }

    [Test]
    public async Task ClinicProcedureIndexGrain_FilterByCategory()
    {
        IClinicProcedureIndexGrain index = NewIndex();
        DateTime now = DateTime.UtcNow;

        await index.UpsertProcedureAsync(MakeEntry("CP-1", ClinicProcedureCategory.EEG, ClinicProcedureStatus.Ordered, now));
        await index.UpsertProcedureAsync(MakeEntry("CP-2", ClinicProcedureCategory.EMG, ClinicProcedureStatus.Ordered, now));
        await index.UpsertProcedureAsync(MakeEntry("CP-3", ClinicProcedureCategory.EEG, ClinicProcedureStatus.Completed, now.AddDays(-1)));

        List<ClinicProcedureIndexEntry> eegs = await index.GetProceduresByCategoryAsync(ClinicProcedureCategory.EEG);
        Assert.That(eegs, Has.Count.EqualTo(2));
        Assert.That(eegs.All(e => e.Category == ClinicProcedureCategory.EEG), Is.True);
    }

    [Test]
    public async Task ClinicProcedureIndexGrain_FilterByCompleted()
    {
        IClinicProcedureIndexGrain index = NewIndex();
        DateTime now = DateTime.UtcNow;

        await index.UpsertProcedureAsync(MakeEntry("CP-A", ClinicProcedureCategory.SleepStudy, ClinicProcedureStatus.Ordered, now));
        await index.UpsertProcedureAsync(MakeEntry("CP-B", ClinicProcedureCategory.SleepStudy, ClinicProcedureStatus.Completed, now.AddDays(-2)));
        await index.UpsertProcedureAsync(MakeEntry("CP-C", ClinicProcedureCategory.Audiometry, ClinicProcedureStatus.Completed, now.AddDays(-1)));

        List<ClinicProcedureIndexEntry> completed = await index.GetCompletedProceduresAsync();
        Assert.That(completed, Has.Count.EqualTo(2));
        Assert.That(completed.All(e => e.Status == ClinicProcedureStatus.Completed), Is.True);
    }

    [Test]
    public async Task ClinicProcedureIndexGrain_RemoveProcedure()
    {
        IClinicProcedureIndexGrain index = NewIndex();
        string id = $"CP-PROC:{Guid.NewGuid()}";
        await index.UpsertProcedureAsync(MakeEntry(id, ClinicProcedureCategory.EEG,
            ClinicProcedureStatus.Ordered, DateTime.UtcNow));

        await index.RemoveProcedureAsync(id);

        List<ClinicProcedureIndexEntry> all = await index.GetAllProceduresAsync();
        Assert.That(all, Is.Empty);
    }

    [Test]
    public async Task ClinicProcedureIndexGrain_RemoveNonExistent_IsIdempotent()
    {
        IClinicProcedureIndexGrain index = NewIndex();
        Assert.DoesNotThrowAsync(() => index.RemoveProcedureAsync("CP-PROC:nonexistent"));
    }

    [Test]
    public async Task ClinicProcedureIndexGrain_OrderedByDateDescending()
    {
        IClinicProcedureIndexGrain index = NewIndex();
        DateTime now = DateTime.UtcNow;

        await index.UpsertProcedureAsync(MakeEntry("CP-OLD", ClinicProcedureCategory.EEG, ClinicProcedureStatus.Ordered, now.AddDays(-10)));
        await index.UpsertProcedureAsync(MakeEntry("CP-NEW", ClinicProcedureCategory.EMG, ClinicProcedureStatus.Ordered, now));
        await index.UpsertProcedureAsync(MakeEntry("CP-MID", ClinicProcedureCategory.NerveConduction, ClinicProcedureStatus.Ordered, now.AddDays(-5)));

        List<ClinicProcedureIndexEntry> all = await index.GetAllProceduresAsync();
        Assert.That(all[0].ProcedureId, Is.EqualTo("CP-NEW"));
        Assert.That(all[1].ProcedureId, Is.EqualTo("CP-MID"));
        Assert.That(all[2].ProcedureId, Is.EqualTo("CP-OLD"));
    }
}
