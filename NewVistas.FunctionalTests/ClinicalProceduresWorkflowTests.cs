// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for Clinical Procedures — VistA File #702.
/// Tests end-to-end workflows via <see cref="IPatientWorkflowGrain"/>.
/// </summary>
[TestFixture]
public class ClinicalProceduresWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain Workflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ── Order procedure ────────────────────────────────────────────────────────

    [Test]
    public async Task OrderProcedure_ReturnsNonEmptyId()
    {
        // Arrange
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Act
        string procedureId = await wf.OrderClinicProcedureAsync(
            ClinicProcedureCategory.EEG,
            "95819",
            "EEG with sleep",
            DateTime.UtcNow,
            "PROV-001", "Dr. Smith",
            "LOC-001", "Neuro Lab",
            "R/O seizure disorder");

        // Assert
        Assert.That(procedureId, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task OrderProcedure_CategoryStored()
    {
        // Arrange
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Act
        string procedureId = await wf.OrderClinicProcedureAsync(
            ClinicProcedureCategory.EEG,
            "95819",
            "EEG with sleep",
            DateTime.UtcNow,
            null, null, null, null, null);

        ClinicProcedureState state = await wf.GetClinicProcedureAsync(procedureId);

        // Assert
        Assert.That(state.Category, Is.EqualTo(ClinicProcedureCategory.EEG));
    }

    [Test]
    public async Task OrderProcedure_ProviderNameStored()
    {
        // Arrange
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Act
        string procedureId = await wf.OrderClinicProcedureAsync(
            ClinicProcedureCategory.EMG,
            "95907",
            "EMG upper extremity",
            DateTime.UtcNow,
            "PROV-002", "Dr. Johnson",
            null, null, null);

        ClinicProcedureState state = await wf.GetClinicProcedureAsync(procedureId);

        // Assert
        Assert.That(state.ProviderName, Is.EqualTo("Dr. Johnson"));
    }

    [Test]
    public async Task GetProcedures_ReturnsEmptyByDefault()
    {
        // Arrange
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Act
        List<ClinicProcedureIndexEntry> procedures = await wf.GetClinicProceduresAsync();

        // Assert
        Assert.That(procedures, Is.Empty);
    }

    [Test]
    public async Task OrderProcedure_AppearsInIndex()
    {
        // Arrange
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Act
        string procedureId = await wf.OrderClinicProcedureAsync(
            ClinicProcedureCategory.SleepStudy,
            "95810",
            "Polysomnography",
            DateTime.UtcNow,
            "PROV-003", "Dr. Lee",
            "LOC-002", "Sleep Lab",
            "Excessive daytime sleepiness");

        List<ClinicProcedureIndexEntry> procedures = await wf.GetClinicProceduresAsync();

        // Assert
        Assert.That(procedures, Has.Count.EqualTo(1));
        Assert.That(procedures[0].ProcedureId, Is.EqualTo(procedureId));
        Assert.That(procedures[0].Status, Is.EqualTo(ClinicProcedureStatus.Ordered));
    }

    [Test]
    public async Task ScheduleProcedure_UpdatesStatus()
    {
        // Arrange
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string procedureId = await wf.OrderClinicProcedureAsync(
            ClinicProcedureCategory.Audiometry,
            "92557",
            "Comprehensive audiometry",
            DateTime.UtcNow,
            null, null, null, null, null);

        DateTime scheduledDate = DateTime.UtcNow.AddDays(7);

        // Act
        await wf.ScheduleClinicProcedureAsync(procedureId, scheduledDate);

        ClinicProcedureState state = await wf.GetClinicProcedureAsync(procedureId);

        // Assert
        Assert.That(state.Status, Is.EqualTo(ClinicProcedureStatus.Scheduled));
        Assert.That(state.ScheduledDate, Is.Not.Null);
    }

    [Test]
    public async Task CompleteProcedure_UpdatesStatus()
    {
        // Arrange
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string procedureId = await wf.OrderClinicProcedureAsync(
            ClinicProcedureCategory.NerveConduction,
            "95907",
            "NCS bilateral upper extremity",
            DateTime.UtcNow,
            null, null, null, null, null);

        // Act
        await wf.CompleteClinicProcedureAsync(
            procedureId,
            DateTime.UtcNow,
            "Normal motor and sensory conduction velocities",
            "No evidence of neuropathy",
            "Patient tolerated well");

        ClinicProcedureState state = await wf.GetClinicProcedureAsync(procedureId);

        // Assert
        Assert.That(state.Status, Is.EqualTo(ClinicProcedureStatus.Completed));
        Assert.That(state.Findings, Does.Contain("Normal motor"));
        Assert.That(state.Impression, Does.Contain("neuropathy"));
    }

    [Test]
    public async Task CancelProcedure_UpdatesStatus()
    {
        // Arrange
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string procedureId = await wf.OrderClinicProcedureAsync(
            ClinicProcedureCategory.EEG,
            "95816",
            "EEG awake only",
            DateTime.UtcNow,
            null, null, null, null, null);

        // Act
        await wf.CancelClinicProcedureAsync(procedureId, "Patient declined");

        ClinicProcedureState state = await wf.GetClinicProcedureAsync(procedureId);

        // Assert
        Assert.That(state.Status, Is.EqualTo(ClinicProcedureStatus.Cancelled));
        Assert.That(state.CancellationReason, Is.EqualTo("Patient declined"));
    }

    [Test]
    public async Task RecordEegResults_PersistsEegFields()
    {
        // Arrange
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string procedureId = await wf.OrderClinicProcedureAsync(
            ClinicProcedureCategory.EEG,
            "95819",
            "EEG with sleep",
            DateTime.UtcNow,
            null, null, null, null, null);

        // Act
        await wf.RecordClinicEegResultsAsync(
            procedureId,
            durationMinutes: 30,
            background: "Posterior dominant rhythm 10 Hz",
            alertType: EegAlertType.Normal,
            seizureActivity: false,
            focalRegion: null,
            activations: new List<string> { "Hyperventilation", "Photic stimulation" });

        ClinicProcedureState state = await wf.GetClinicProcedureAsync(procedureId);

        // Assert
        Assert.That(state.EegDurationMinutes, Is.EqualTo(30));
        Assert.That(state.EegBackground, Does.Contain("10 Hz"));
        Assert.That(state.EegAlertType, Is.EqualTo(EegAlertType.Normal));
        Assert.That(state.EegSeizureActivity, Is.False);
        Assert.That(state.EegActivations, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task RecordEmgResults_PersistsEmgFields()
    {
        // Arrange
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string procedureId = await wf.OrderClinicProcedureAsync(
            ClinicProcedureCategory.EMG,
            "95907",
            "EMG bilateral lower extremity",
            DateTime.UtcNow,
            null, null, null, null, null);

        // Act
        await wf.RecordClinicEmgResultsAsync(
            procedureId,
            musclesStudied: new List<string> { "Tibialis anterior", "Gastrocnemius", "Vastus medialis" },
            findingType: EmgFindingType.Neuropathy,
            spontaneousActivity: "Fibrillation potentials noted",
            mupDescription: "Large polyphasic MUPs");

        ClinicProcedureState state = await wf.GetClinicProcedureAsync(procedureId);

        // Assert
        Assert.That(state.EmgMusclesStudied, Has.Count.EqualTo(3));
        Assert.That(state.EmgFindingType, Is.EqualTo(EmgFindingType.Neuropathy));
        Assert.That(state.EmgSpontaneousActivity, Does.Contain("Fibrillation"));
        Assert.That(state.EmgMupDescription, Does.Contain("polyphasic"));
    }

    [Test]
    public async Task GetProceduresByCategory_FiltersCorrectly()
    {
        // Arrange
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.OrderClinicProcedureAsync(
            ClinicProcedureCategory.EEG,
            "95819", "EEG with sleep", DateTime.UtcNow,
            null, null, null, null, null);

        await wf.OrderClinicProcedureAsync(
            ClinicProcedureCategory.EMG,
            "95907", "EMG upper extremity", DateTime.UtcNow,
            null, null, null, null, null);

        await wf.OrderClinicProcedureAsync(
            ClinicProcedureCategory.EEG,
            "95816", "EEG awake only", DateTime.UtcNow,
            null, null, null, null, null);

        // Act
        List<ClinicProcedureIndexEntry> eegOnly = await wf.GetClinicProceduresByCategoryAsync(ClinicProcedureCategory.EEG);
        List<ClinicProcedureIndexEntry> emgOnly = await wf.GetClinicProceduresByCategoryAsync(ClinicProcedureCategory.EMG);

        // Assert
        Assert.That(eegOnly, Has.Count.EqualTo(2));
        Assert.That(emgOnly, Has.Count.EqualTo(1));
        Assert.That(eegOnly.All(e => e.Category == ClinicProcedureCategory.EEG), Is.True);
    }

    [Test]
    public async Task GetCompletedProcedures_ReturnsOnlyCompleted()
    {
        // Arrange
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string p1 = await wf.OrderClinicProcedureAsync(
            ClinicProcedureCategory.EEG,
            "95819", "EEG with sleep", DateTime.UtcNow,
            null, null, null, null, null);

        string p2 = await wf.OrderClinicProcedureAsync(
            ClinicProcedureCategory.EMG,
            "95907", "EMG upper", DateTime.UtcNow,
            null, null, null, null, null);

        await wf.CompleteClinicProcedureAsync(p1, DateTime.UtcNow, "Normal", "WNL", null);

        // Act
        List<ClinicProcedureIndexEntry> completed = await wf.GetCompletedClinicProceduresAsync();

        // Assert
        Assert.That(completed, Has.Count.EqualTo(1));
        Assert.That(completed[0].ProcedureId, Is.EqualTo(p1));
        Assert.That(completed[0].Status, Is.EqualTo(ClinicProcedureStatus.Completed));
    }

    [Test]
    public async Task FullLifecycle_OrderScheduleComplete()
    {
        // Arrange
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Act — order
        string procedureId = await wf.OrderClinicProcedureAsync(
            ClinicProcedureCategory.SleepStudy,
            "95810", "Polysomnography",
            DateTime.UtcNow,
            "PROV-010", "Dr. Patel",
            "LOC-003", "Sleep Lab",
            "OSA screening");

        ClinicProcedureState state = await wf.GetClinicProcedureAsync(procedureId);
        Assert.That(state.Status, Is.EqualTo(ClinicProcedureStatus.Ordered));

        // Act — schedule
        DateTime scheduledDate = DateTime.UtcNow.AddDays(14);
        await wf.ScheduleClinicProcedureAsync(procedureId, scheduledDate);

        state = await wf.GetClinicProcedureAsync(procedureId);
        Assert.That(state.Status, Is.EqualTo(ClinicProcedureStatus.Scheduled));

        // Act — complete
        await wf.CompleteClinicProcedureAsync(
            procedureId,
            DateTime.UtcNow,
            "AHI 22 events/hr; sleep efficiency 78%",
            "Moderate obstructive sleep apnea",
            "CPAP titration recommended");

        state = await wf.GetClinicProcedureAsync(procedureId);

        // Assert
        Assert.That(state.Status, Is.EqualTo(ClinicProcedureStatus.Completed));
        Assert.That(state.Impression, Does.Contain("obstructive sleep apnea"));
        Assert.That(state.PerformedDate, Is.Not.Null);
    }

    [Test]
    public async Task MultipleProcedures_AllAppearInIndex()
    {
        // Arrange
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Act
        await wf.OrderClinicProcedureAsync(
            ClinicProcedureCategory.EEG,
            "95819", "EEG with sleep", DateTime.UtcNow,
            null, null, null, null, null);

        await wf.OrderClinicProcedureAsync(
            ClinicProcedureCategory.EMG,
            "95907", "EMG lower extremity", DateTime.UtcNow,
            null, null, null, null, null);

        await wf.OrderClinicProcedureAsync(
            ClinicProcedureCategory.Audiometry,
            "92557", "Comprehensive audiometry", DateTime.UtcNow,
            null, null, null, null, null);

        List<ClinicProcedureIndexEntry> all = await wf.GetClinicProceduresAsync();

        // Assert
        Assert.That(all, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task RecordNcsResults_PersistsNcsFields()
    {
        // Arrange
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string procedureId = await wf.OrderClinicProcedureAsync(
            ClinicProcedureCategory.NerveConduction,
            "95907",
            "NCS bilateral upper extremity",
            DateTime.UtcNow,
            null, null, null, null, null);

        // Act
        await wf.RecordClinicNcsResultsAsync(
            procedureId,
            nervesStudied: new List<string> { "Median motor", "Median sensory", "Ulnar motor", "Ulnar sensory" },
            meanMotorVelocity: 52.3m,
            meanSensoryVelocity: 48.1m,
            fWavesObtained: true,
            findingType: EmgFindingType.Normal);

        ClinicProcedureState state = await wf.GetClinicProcedureAsync(procedureId);

        // Assert
        Assert.That(state.NcsNervesStudied, Has.Count.EqualTo(4));
        Assert.That(state.NcsMeanMotorVelocity, Is.EqualTo(52.3m));
        Assert.That(state.NcsMeanSensoryVelocity, Is.EqualTo(48.1m));
        Assert.That(state.NcsFWavesObtained, Is.True);
        Assert.That(state.NcsFindingType, Is.EqualTo(EmgFindingType.Normal));
    }

    [Test]
    public async Task RecordSleepStudyResults_PersistsSleepFields()
    {
        // Arrange
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string procedureId = await wf.OrderClinicProcedureAsync(
            ClinicProcedureCategory.SleepStudy,
            "95810",
            "Diagnostic polysomnography",
            DateTime.UtcNow,
            null, null, null, null, null);

        // Act
        await wf.RecordClinicSleepStudyResultsAsync(
            procedureId,
            studyType: SleepStudyType.Diagnostic,
            apneaType: SleepApneaType.Obstructive,
            apneaHypopneaIndex: 28.5m,
            cpapPressureCmH2O: null,
            sleepEfficiencyPct: 72.3m,
            totalSleepTimeMin: 312,
            sleepLatencyMin: 18.5m,
            remLatencyMin: 95.2m);

        ClinicProcedureState state = await wf.GetClinicProcedureAsync(procedureId);

        // Assert
        Assert.That(state.SleepStudyType, Is.EqualTo(SleepStudyType.Diagnostic));
        Assert.That(state.SleepApneaType, Is.EqualTo(SleepApneaType.Obstructive));
        Assert.That(state.ApneaHypopneaIndex, Is.EqualTo(28.5m));
        Assert.That(state.SleepEfficiencyPct, Is.EqualTo(72.3m));
        Assert.That(state.TotalSleepTimeMin, Is.EqualTo(312));
    }

    [Test]
    public async Task RecordAudiometryResults_PersistsAudioFields()
    {
        // Arrange
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        string procedureId = await wf.OrderClinicProcedureAsync(
            ClinicProcedureCategory.Audiometry,
            "92557",
            "Comprehensive audiometry",
            DateTime.UtcNow,
            null, null, null, null, null);

        // Act
        await wf.RecordClinicAudiometryResultsAsync(
            procedureId,
            hearingLossType: HearingLossType.Sensorineural,
            rightEarPta: 35m,
            leftEarPta: 40m,
            speechDiscriminationRight: 88m,
            speechDiscriminationLeft: 84m,
            tympanometryRight: "Type A (normal)",
            tympanometryLeft: "Type A (normal)");

        ClinicProcedureState state = await wf.GetClinicProcedureAsync(procedureId);

        // Assert
        Assert.That(state.HearingLossType, Is.EqualTo(HearingLossType.Sensorineural));
        Assert.That(state.RightEarPta, Is.EqualTo(35m));
        Assert.That(state.LeftEarPta, Is.EqualTo(40m));
        Assert.That(state.SpeechDiscriminationRight, Is.EqualTo(88m));
        Assert.That(state.TympanometryRight, Does.Contain("Type A"));
    }
}
