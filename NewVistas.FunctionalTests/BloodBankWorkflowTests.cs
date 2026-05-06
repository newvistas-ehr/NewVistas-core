// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for VistA Blood Bank — File #65.
/// Tests end-to-end workflows via <see cref="IPatientWorkflowGrain"/>.
/// Blood unit creation (admin function) is done directly via grain factory;
/// patient-centric operations go through the workflow grain.
/// </summary>
[TestFixture]
public class BloodBankWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IPatientWorkflowGrain NewWorkflow()
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>($"PATIENT-{Guid.NewGuid():N}");

    /// <summary>
    /// Creates an Available blood unit directly via grain factory and returns the raw unit ID
    /// (without the "BB-UNIT:" prefix). The workflow grain prepends that prefix itself,
    /// so callers should pass the raw ID to workflow grain methods.
    /// </summary>
    private async Task<string> CreateAvailableUnitAsync(
        BloodProductType type = BloodProductType.PackedRBC,
        AboBloodType abo = AboBloodType.O,
        RhBloodType rh = RhBloodType.Negative)
    {
        string rawId = Guid.NewGuid().ToString("N");
        IBloodUnitGrain unit = _cluster.GrainFactory.GetGrain<IBloodUnitGrain>($"BB-UNIT:{rawId}");
        await unit.CreateAsync(type, abo, rh,
            DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(40),
            "Regional Blood Center", null, null, 275m,
            false, true, false, false, null, null);
        return rawId; // raw ID — workflow grain adds "BB-UNIT:" prefix
    }

    /// <summary>Gets the blood unit grain using the raw ID returned by CreateAvailableUnitAsync.</summary>
    private IBloodUnitGrain GetUnit(string rawId)
        => _cluster.GrainFactory.GetGrain<IBloodUnitGrain>($"BB-UNIT:{rawId}");

    // ─── Blood type tests ─────────────────────────────────────────────────────

    [Test]
    public async Task BloodBankWorkflow_UpdateBloodType_PersistsAboAndRh()
    {
        // Arrange
        IPatientWorkflowGrain wf = NewWorkflow();

        // Act
        await wf.UpdateBloodTypeAsync(
            AboBloodType.O,
            RhBloodType.Negative,
            AntibodyScreenResult.Negative,
            DateTime.UtcNow.Date,
            directAntibodyTest: null,
            specialRequirements: "Irradiated products required",
            notes: null);
        BloodBankPatientState state = await wf.GetBloodBankPatientAsync();

        // Assert
        Assert.That(state.AboType, Is.EqualTo(AboBloodType.O));
        Assert.That(state.RhType, Is.EqualTo(RhBloodType.Negative));
        Assert.That(state.AntibodyScreenResult, Is.EqualTo(AntibodyScreenResult.Negative));
        Assert.That(state.SpecialRequirements, Is.EqualTo("Irradiated products required"));
    }

    [Test]
    public async Task BloodBankWorkflow_UpdateBloodType_CanBeCalledRepeatedly()
    {
        // Arrange
        IPatientWorkflowGrain wf = NewWorkflow();

        // Act — update twice (retesting changes type)
        await wf.UpdateBloodTypeAsync(AboBloodType.A, RhBloodType.Positive,
            AntibodyScreenResult.Pending, null, null, null, null);
        await wf.UpdateBloodTypeAsync(AboBloodType.A, RhBloodType.Positive,
            AntibodyScreenResult.Negative, DateTime.UtcNow.Date, null, null, null);
        BloodBankPatientState state = await wf.GetBloodBankPatientAsync();

        // Assert — latest values win
        Assert.That(state.AntibodyScreenResult, Is.EqualTo(AntibodyScreenResult.Negative));
        Assert.That(state.AntibodyScreenDate, Is.Not.Null);
    }

    // ─── Crossmatch workflow tests ────────────────────────────────────────────

    [Test]
    public async Task BloodBankWorkflow_RequestCrossmatch_ReturnsNonEmptyId()
    {
        // Arrange
        IPatientWorkflowGrain wf = NewWorkflow();
        string unitKey = await CreateAvailableUnitAsync();

        // Act
        string crossmatchId = await wf.RequestCrossmatchAsync(
            unitKey, CrossmatchUrgency.Routine, "NURSE-01", "Nurse Smith", notes: null);

        // Assert
        Assert.That(crossmatchId, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task BloodBankWorkflow_RequestCrossmatch_AppearsInPatientIndex()
    {
        // Arrange
        IPatientWorkflowGrain wf = NewWorkflow();
        string unitKey = await CreateAvailableUnitAsync();

        // Act
        string crossmatchId = await wf.RequestCrossmatchAsync(
            unitKey, CrossmatchUrgency.Urgent, "NURSE-02", "Nurse Jones", null);
        List<CrossmatchIndexEntry> crossmatches = await wf.GetCrossmatchesAsync();

        // Assert
        Assert.That(crossmatches, Has.Count.EqualTo(1));
        Assert.That(crossmatches[0].CrossmatchId, Is.EqualTo(crossmatchId));
        Assert.That(crossmatches[0].Urgency, Is.EqualTo(CrossmatchUrgency.Urgent));
        Assert.That(crossmatches[0].Result, Is.EqualTo(CrossmatchResult.Pending));
    }

    [Test]
    public async Task BloodBankWorkflow_RequestCrossmatch_ReservesBloodUnit()
    {
        // Arrange
        IPatientWorkflowGrain wf = NewWorkflow();
        string unitKey = await CreateAvailableUnitAsync();

        // Act
        await wf.RequestCrossmatchAsync(unitKey, CrossmatchUrgency.Stat, "NURSE-03", "Nurse Brown", null);

        // Assert — verify unit grain was updated to Reserved
        BloodUnitState unitState = await GetUnit(unitKey).GetUnitAsync();
        Assert.That(unitState.Status, Is.EqualTo(BloodUnitStatus.Reserved));
    }

    [Test]
    public async Task BloodBankWorkflow_RecordCrossmatchResult_UpdatesIndexEntry()
    {
        // Arrange
        IPatientWorkflowGrain wf = NewWorkflow();
        string unitKey = await CreateAvailableUnitAsync();
        string xmId = await wf.RequestCrossmatchAsync(unitKey, CrossmatchUrgency.Routine, "NURSE-04", "Nurse Taylor", null);

        // Act
        await wf.RecordCrossmatchResultAsync(xmId,
            CrossmatchResult.Compatible,
            CrossmatchMethod.Electronic,
            "TECH-01", "Tech Garcia",
            antibodyIdentification: null);
        List<CrossmatchIndexEntry> crossmatches = await wf.GetCrossmatchesAsync();

        // Assert
        CrossmatchIndexEntry entry = crossmatches.Single(x => x.CrossmatchId == xmId);
        Assert.That(entry.Result, Is.EqualTo(CrossmatchResult.Compatible));
    }

    [Test]
    public async Task BloodBankWorkflow_GetCrossmatches_ReturnsAllForPatient()
    {
        // Arrange
        IPatientWorkflowGrain wf = NewWorkflow();
        string unit1Key = await CreateAvailableUnitAsync();
        string unit2Key = await CreateAvailableUnitAsync(BloodProductType.Platelets, AboBloodType.A, RhBloodType.Positive);

        // Act — two crossmatch requests
        string xm1 = await wf.RequestCrossmatchAsync(unit1Key, CrossmatchUrgency.Routine, "NURSE-05", "Nurse Davis", null);
        string xm2 = await wf.RequestCrossmatchAsync(unit2Key, CrossmatchUrgency.Urgent, "NURSE-05", "Nurse Davis", null);
        List<CrossmatchIndexEntry> crossmatches = await wf.GetCrossmatchesAsync();

        // Assert
        Assert.That(crossmatches, Has.Count.EqualTo(2));
        Assert.That(crossmatches.Any(x => x.CrossmatchId == xm1), Is.True);
        Assert.That(crossmatches.Any(x => x.CrossmatchId == xm2), Is.True);
    }

    // ─── Transfusion workflow tests ───────────────────────────────────────────

    [Test]
    public async Task BloodBankWorkflow_StartTransfusion_ReturnsNonEmptyId()
    {
        // Arrange
        IPatientWorkflowGrain wf = NewWorkflow();
        string unitKey = await CreateAvailableUnitAsync();
        string xmId = await wf.RequestCrossmatchAsync(unitKey, CrossmatchUrgency.Routine, "NURSE-06", "Nurse Wilson", null);
        await wf.RecordCrossmatchResultAsync(xmId, CrossmatchResult.Compatible, CrossmatchMethod.Electronic,
            "TECH-02", "Tech Moore", null);

        // Act
        string txId = await wf.StartTransfusionAsync(
            xmId, unitKey,
            "NURSE-06", "Nurse Wilson",
            "DR-01", "Dr. Smith",
            "Left antecubital", "BP 118/76, HR 68");

        // Assert
        Assert.That(txId, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task BloodBankWorkflow_StartTransfusion_MarksUnitAsTransfused()
    {
        // Arrange
        IPatientWorkflowGrain wf = NewWorkflow();
        string unitKey = await CreateAvailableUnitAsync();
        string xmId = await wf.RequestCrossmatchAsync(unitKey, CrossmatchUrgency.Urgent, "NURSE-07", "Nurse Harris", null);
        await wf.RecordCrossmatchResultAsync(xmId, CrossmatchResult.Compatible, CrossmatchMethod.Electronic,
            "TECH-03", "Tech Clark", null);

        // Act
        await wf.StartTransfusionAsync(xmId, unitKey,
            "NURSE-07", "Nurse Harris", "DR-02", "Dr. Lee", null, null);

        // Assert — blood unit should now be Transfused
        BloodUnitState unitState = await GetUnit(unitKey).GetUnitAsync();
        Assert.That(unitState.Status, Is.EqualTo(BloodUnitStatus.Transfused));
    }

    [Test]
    public async Task BloodBankWorkflow_StartTransfusion_IncrementsPatientTransfusionCount()
    {
        // Arrange
        IPatientWorkflowGrain wf = NewWorkflow();
        await wf.UpdateBloodTypeAsync(AboBloodType.O, RhBloodType.Negative,
            AntibodyScreenResult.Negative, null, null, null, null);
        string unitKey = await CreateAvailableUnitAsync();
        string xmId = await wf.RequestCrossmatchAsync(unitKey, CrossmatchUrgency.Routine, "NURSE-08", "Nurse Robinson", null);
        await wf.RecordCrossmatchResultAsync(xmId, CrossmatchResult.Compatible, CrossmatchMethod.AHGPhase,
            "TECH-04", "Tech White", null);

        // Act
        await wf.StartTransfusionAsync(xmId, unitKey,
            "NURSE-08", "Nurse Robinson", "DR-03", "Dr. Patel", null, null);
        BloodBankPatientState state = await wf.GetBloodBankPatientAsync();

        // Assert
        Assert.That(state.TransfusionCount, Is.EqualTo(1));
    }

    [Test]
    public async Task BloodBankWorkflow_CompleteTransfusion_SetsCompletedStatus()
    {
        // Arrange
        IPatientWorkflowGrain wf = NewWorkflow();
        string unitKey = await CreateAvailableUnitAsync();
        string xmId = await wf.RequestCrossmatchAsync(unitKey, CrossmatchUrgency.Routine, "NURSE-09", "Nurse Thompson", null);
        await wf.RecordCrossmatchResultAsync(xmId, CrossmatchResult.Compatible, CrossmatchMethod.Electronic,
            "TECH-05", "Tech Jackson", null);
        string txId = await wf.StartTransfusionAsync(xmId, unitKey,
            "NURSE-09", "Nurse Thompson", "DR-04", "Dr. Martinez", null, null);

        // Act
        await wf.CompleteTransfusionAsync(txId, DateTime.UtcNow.AddHours(2), 275m, "BP 120/78, HR 70");
        List<TransfusionIndexEntry> history = await wf.GetTransfusionHistoryAsync();

        // Assert
        TransfusionIndexEntry entry = history.Single(t => t.TransfusionId == txId);
        Assert.That(entry.Status, Is.EqualTo(TransfusionStatus.Completed));
        Assert.That(entry.EndDateTime, Is.Not.Null);
    }

    [Test]
    public async Task BloodBankWorkflow_StopTransfusion_SetsReactionTypeInHistory()
    {
        // Arrange
        IPatientWorkflowGrain wf = NewWorkflow();
        string unitKey = await CreateAvailableUnitAsync();
        string xmId = await wf.RequestCrossmatchAsync(unitKey, CrossmatchUrgency.Stat, "NURSE-10", "Nurse Anderson", null);
        await wf.RecordCrossmatchResultAsync(xmId, CrossmatchResult.Compatible, CrossmatchMethod.Electronic,
            "TECH-06", "Tech Lewis", null);
        string txId = await wf.StartTransfusionAsync(xmId, unitKey,
            "NURSE-10", "Nurse Anderson", "DR-05", "Dr. Kim", null, null);

        // Act
        await wf.StopTransfusionAsync(txId,
            DateTime.UtcNow.AddMinutes(45),
            "Patient reported chills and fever",
            TransfusionReactionType.Febrile,
            "Temperature rose to 38.5°C during transfusion");
        List<TransfusionIndexEntry> history = await wf.GetTransfusionHistoryAsync();

        // Assert
        TransfusionIndexEntry entry = history.Single(t => t.TransfusionId == txId);
        Assert.That(entry.Status, Is.EqualTo(TransfusionStatus.Reaction));
        Assert.That(entry.ReactionType, Is.EqualTo(TransfusionReactionType.Febrile));
    }

    [Test]
    public async Task BloodBankWorkflow_GetTransfusionHistory_ReturnsAllTransfusions()
    {
        // Arrange — two full transfusion cycles for one patient
        IPatientWorkflowGrain wf = NewWorkflow();

        string unit1Key = await CreateAvailableUnitAsync();
        string xm1 = await wf.RequestCrossmatchAsync(unit1Key, CrossmatchUrgency.Routine, "NURSE-11", "Nurse Walker", null);
        await wf.RecordCrossmatchResultAsync(xm1, CrossmatchResult.Compatible, CrossmatchMethod.Electronic,
            "TECH-07", "Tech Hall", null);
        string tx1 = await wf.StartTransfusionAsync(xm1, unit1Key,
            "NURSE-11", "Nurse Walker", "DR-06", "Dr. Garcia", null, null);
        await wf.CompleteTransfusionAsync(tx1, DateTime.UtcNow.AddHours(2), 275m, null);

        string unit2Key = await CreateAvailableUnitAsync(BloodProductType.FreshFrozenPlasma, AboBloodType.A, RhBloodType.Positive);
        string xm2 = await wf.RequestCrossmatchAsync(unit2Key, CrossmatchUrgency.Urgent, "NURSE-11", "Nurse Walker", null);
        await wf.RecordCrossmatchResultAsync(xm2, CrossmatchResult.Compatible, CrossmatchMethod.Electronic,
            "TECH-07", "Tech Hall", null);
        string tx2 = await wf.StartTransfusionAsync(xm2, unit2Key,
            "NURSE-11", "Nurse Walker", "DR-06", "Dr. Garcia", null, null);

        // Act
        List<TransfusionIndexEntry> history = await wf.GetTransfusionHistoryAsync();

        // Assert — both transfusions appear
        Assert.That(history, Has.Count.EqualTo(2));
        Assert.That(history.Any(t => t.TransfusionId == tx1), Is.True);
        Assert.That(history.Any(t => t.TransfusionId == tx2), Is.True);
        // First is completed, second is still in progress
        Assert.That(history.Single(t => t.TransfusionId == tx1).Status, Is.EqualTo(TransfusionStatus.Completed));
        Assert.That(history.Single(t => t.TransfusionId == tx2).Status, Is.EqualTo(TransfusionStatus.InProgress));
    }
}
