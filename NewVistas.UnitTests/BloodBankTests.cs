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
/// Unit tests for VistA Blood Bank — File #65 (BLOOD BANK PATIENT, BLOOD UNIT, CROSSMATCH, TRANSFUSION).
/// Tests individual grains directly via TestCluster (not via the workflow grain).
/// </summary>
[TestFixture]
public class BloodBankTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IBloodBankPatientGrain NewPatient()
        => _cluster.GrainFactory.GetGrain<IBloodBankPatientGrain>($"BB-PATIENT:{Guid.NewGuid():N}");

    private IBloodUnitGrain NewUnit()
        => _cluster.GrainFactory.GetGrain<IBloodUnitGrain>($"BB-UNIT:{Guid.NewGuid():N}");

    private ICrossmatchGrain NewCrossmatch()
        => _cluster.GrainFactory.GetGrain<ICrossmatchGrain>($"BB-XM:{Guid.NewGuid():N}");

    private ICrossmatchIndexGrain CrossmatchIndex(string patientId)
        => _cluster.GrainFactory.GetGrain<ICrossmatchIndexGrain>($"BB-XM-IDX:{patientId}");

    private ITransfusionGrain NewTransfusion()
        => _cluster.GrainFactory.GetGrain<ITransfusionGrain>($"BB-TX:{Guid.NewGuid():N}");

    private ITransfusionIndexGrain TransfusionIndex(string patientId)
        => _cluster.GrainFactory.GetGrain<ITransfusionIndexGrain>($"BB-TX-IDX:{patientId}");

    private static Task CreateUnitAsync(IBloodUnitGrain unit, BloodProductType type = BloodProductType.PackedRBC,
        AboBloodType abo = AboBloodType.O, RhBloodType rh = RhBloodType.Negative)
        => unit.CreateAsync(type, abo, rh,
            DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(40),
            null, null, null, null,
            false, false, false, false, null, null);

    // ─── BloodBankPatient tests ──────────────────────────────────────────────

    [Test]
    public async Task BloodBankPatient_Initialize_SetsPatientId()
    {
        // Arrange
        string patientId = $"PAT-{Guid.NewGuid():N}";
        IBloodBankPatientGrain grain = NewPatient();

        // Act
        await grain.InitializeAsync(patientId);
        BloodBankPatientState state = await grain.GetAsync();

        // Assert
        Assert.That(state.PatientId, Is.EqualTo(patientId));
    }

    [Test]
    public async Task BloodBankPatient_Initialize_IsIdempotent()
    {
        // Arrange
        string patientId = $"PAT-{Guid.NewGuid():N}";
        IBloodBankPatientGrain grain = NewPatient();

        // Act — second call with different ID should be ignored
        await grain.InitializeAsync(patientId);
        await grain.InitializeAsync("DIFFERENT-PATIENT-ID");
        BloodBankPatientState state = await grain.GetAsync();

        // Assert — original value preserved
        Assert.That(state.PatientId, Is.EqualTo(patientId));
    }

    [Test]
    public async Task BloodBankPatient_UpdateBloodType_PersistsAllFields()
    {
        // Arrange
        IBloodBankPatientGrain grain = NewPatient();
        await grain.InitializeAsync($"PAT-{Guid.NewGuid():N}");
        DateTime screenDate = DateTime.UtcNow.Date;

        // Act
        await grain.UpdateBloodTypeAsync(
            AboBloodType.O,
            RhBloodType.Negative,
            AntibodyScreenResult.Negative,
            screenDate,
            directAntibodyTest: "IAT",
            specialRequirements: "Leukoreduced and irradiated required",
            notes: "Alloimmunized patient");
        BloodBankPatientState state = await grain.GetAsync();

        // Assert
        Assert.That(state.AboType, Is.EqualTo(AboBloodType.O));
        Assert.That(state.RhType, Is.EqualTo(RhBloodType.Negative));
        Assert.That(state.AntibodyScreenResult, Is.EqualTo(AntibodyScreenResult.Negative));
        Assert.That(state.AntibodyScreenDate, Is.EqualTo(screenDate));
        Assert.That(state.DirectAntibodyTest, Is.EqualTo("IAT"));
        Assert.That(state.SpecialRequirements, Is.EqualTo("Leukoreduced and irradiated required"));
    }

    [Test]
    public async Task BloodBankPatient_IncrementTransfusionCount_AccumulatesCorrectly()
    {
        // Arrange
        IBloodBankPatientGrain grain = NewPatient();
        await grain.InitializeAsync($"PAT-{Guid.NewGuid():N}");

        // Act — increment three times
        await grain.IncrementTransfusionCountAsync();
        await grain.IncrementTransfusionCountAsync();
        await grain.IncrementTransfusionCountAsync();
        BloodBankPatientState state = await grain.GetAsync();

        // Assert
        Assert.That(state.TransfusionCount, Is.EqualTo(3));
    }

    // ─── BloodUnit tests ─────────────────────────────────────────────────────

    [Test]
    public async Task BloodUnit_Create_SetsFieldsAndDefaultsToAvailable()
    {
        // Arrange
        IBloodUnitGrain unit = NewUnit();
        DateTime collectionDate = DateTime.UtcNow.AddDays(-1);
        DateTime expirationDate = DateTime.UtcNow.AddDays(41);

        // Act
        await unit.CreateAsync(
            BloodProductType.PackedRBC,
            AboBloodType.A,
            RhBloodType.Positive,
            collectionDate,
            expirationDate,
            "Regional Blood Center",
            "DONOR-001",
            "PRBC-A001",
            275m,
            isIrradiated: false,
            isLeukoreduced: true,
            isWashed: false,
            isAntigenNegative: false,
            antigenNegativeFor: null,
            notes: null);
        BloodUnitState state = await unit.GetUnitAsync();

        // Assert
        Assert.That(state.ProductType, Is.EqualTo(BloodProductType.PackedRBC));
        Assert.That(state.AboType, Is.EqualTo(AboBloodType.A));
        Assert.That(state.RhType, Is.EqualTo(RhBloodType.Positive));
        Assert.That(state.Status, Is.EqualTo(BloodUnitStatus.Available));
        Assert.That(state.IsLeukoreduced, Is.True);
        Assert.That(state.VolumeML, Is.EqualTo(275m));
        Assert.That(state.SourceFacility, Is.EqualTo("Regional Blood Center"));
    }

    [Test]
    public async Task BloodUnit_Reserve_ChangesStatusAndRecordsPatient()
    {
        // Arrange
        IBloodUnitGrain unit = NewUnit();
        await CreateUnitAsync(unit, BloodProductType.PackedRBC, AboBloodType.O, RhBloodType.Negative);
        string patientId = $"PAT-{Guid.NewGuid():N}";
        string crossmatchId = $"BB-XM:{Guid.NewGuid():N}";

        // Act
        await unit.ReserveAsync(patientId, crossmatchId);
        BloodUnitState state = await unit.GetUnitAsync();

        // Assert
        Assert.That(state.Status, Is.EqualTo(BloodUnitStatus.Reserved));
        Assert.That(state.ReservedForPatientId, Is.EqualTo(patientId));
        Assert.That(state.ReservedForCrossmatchId, Is.EqualTo(crossmatchId));
    }

    [Test]
    public async Task BloodUnit_MarkTransfused_ChangesStatusAndRecordsIds()
    {
        // Arrange
        IBloodUnitGrain unit = NewUnit();
        await CreateUnitAsync(unit, BloodProductType.PackedRBC, AboBloodType.B, RhBloodType.Positive);
        string patientId = $"PAT-{Guid.NewGuid():N}";
        string transfusionId = $"BB-TX:{Guid.NewGuid():N}";
        DateTime transfusionDate = DateTime.UtcNow;

        // Act
        await unit.MarkTransfusedAsync(patientId, transfusionId, transfusionDate);
        BloodUnitState state = await unit.GetUnitAsync();

        // Assert
        Assert.That(state.Status, Is.EqualTo(BloodUnitStatus.Transfused));
        Assert.That(state.TransfusedToPatientId, Is.EqualTo(patientId));
        Assert.That(state.TransfusionId, Is.EqualTo(transfusionId));
    }

    [Test]
    public async Task BloodUnit_Quarantine_ChangesStatusToQuarantine()
    {
        // Arrange
        IBloodUnitGrain unit = NewUnit();
        await CreateUnitAsync(unit, BloodProductType.Platelets, AboBloodType.AB, RhBloodType.Positive);

        // Act
        await unit.QuarantineAsync("Suspected contamination");
        BloodUnitState state = await unit.GetUnitAsync();

        // Assert
        Assert.That(state.Status, Is.EqualTo(BloodUnitStatus.Quarantine));
    }

    [Test]
    public async Task BloodUnit_Discard_SetsDiscardedStatusAndReason()
    {
        // Arrange
        IBloodUnitGrain unit = NewUnit();
        await CreateUnitAsync(unit, BloodProductType.FreshFrozenPlasma, AboBloodType.A, RhBloodType.Positive);

        // Act
        await unit.DiscardAsync("Unit expired");
        BloodUnitState state = await unit.GetUnitAsync();

        // Assert
        Assert.That(state.Status, Is.EqualTo(BloodUnitStatus.Discarded));
        Assert.That(state.DisposalReason, Is.EqualTo("Unit expired"));
    }

    [Test]
    public async Task BloodUnit_ReleaseReservation_RestoresAvailableAndClearsPatient()
    {
        // Arrange
        IBloodUnitGrain unit = NewUnit();
        await CreateUnitAsync(unit);
        await unit.ReserveAsync($"PAT-{Guid.NewGuid():N}", $"BB-XM:{Guid.NewGuid():N}");

        // Act
        await unit.ReleaseReservationAsync();
        BloodUnitState state = await unit.GetUnitAsync();

        // Assert
        Assert.That(state.Status, Is.EqualTo(BloodUnitStatus.Available));
        Assert.That(state.ReservedForPatientId, Is.Null);
        Assert.That(state.ReservedForCrossmatchId, Is.Null);
    }

    // ─── BloodUnitIndex tests ────────────────────────────────────────────────

    [Test]
    public async Task BloodUnitIndex_AddOrUpdate_AccumulatesMultipleEntries()
    {
        // Arrange — use unique index key to isolate from other tests
        IBloodUnitIndexGrain idx = _cluster.GrainFactory.GetGrain<IBloodUnitIndexGrain>(
            $"BB-UNIT-IDX-ACCUM:{Guid.NewGuid():N}");
        string uid1 = $"BB-UNIT:{Guid.NewGuid():N}";
        string uid2 = $"BB-UNIT:{Guid.NewGuid():N}";

        // Act
        await idx.AddOrUpdateAsync(new BloodUnitIndexEntry
        {
            UnitId = uid1, ProductType = BloodProductType.PackedRBC,
            AboType = AboBloodType.O, RhType = RhBloodType.Negative,
            Status = BloodUnitStatus.Available
        });
        await idx.AddOrUpdateAsync(new BloodUnitIndexEntry
        {
            UnitId = uid2, ProductType = BloodProductType.Platelets,
            AboType = AboBloodType.A, RhType = RhBloodType.Positive,
            Status = BloodUnitStatus.Available
        });
        List<BloodUnitIndexEntry> all = await idx.GetAllAsync();

        // Assert
        Assert.That(all, Has.Count.EqualTo(2));
        Assert.That(all.Any(u => u.UnitId == uid1), Is.True);
        Assert.That(all.Any(u => u.UnitId == uid2), Is.True);
    }

    [Test]
    public async Task BloodUnitIndex_Search_FiltersByProductType()
    {
        // Arrange
        IBloodUnitIndexGrain idx = _cluster.GrainFactory.GetGrain<IBloodUnitIndexGrain>(
            $"BB-UNIT-IDX-FILTER:{Guid.NewGuid():N}");
        string prbcId = $"BB-UNIT:{Guid.NewGuid():N}";
        string platId = $"BB-UNIT:{Guid.NewGuid():N}";
        await idx.AddOrUpdateAsync(new BloodUnitIndexEntry
        {
            UnitId = prbcId, ProductType = BloodProductType.PackedRBC,
            AboType = AboBloodType.O, RhType = RhBloodType.Positive,
            Status = BloodUnitStatus.Available
        });
        await idx.AddOrUpdateAsync(new BloodUnitIndexEntry
        {
            UnitId = platId, ProductType = BloodProductType.Platelets,
            AboType = AboBloodType.O, RhType = RhBloodType.Positive,
            Status = BloodUnitStatus.Available
        });

        // Act
        List<BloodUnitIndexEntry> results = await idx.SearchAsync(
            BloodProductType.PackedRBC, null, null, null, availableOnly: false);

        // Assert
        Assert.That(results.All(r => r.ProductType == BloodProductType.PackedRBC), Is.True);
        Assert.That(results.Any(r => r.UnitId == prbcId), Is.True);
        Assert.That(results.Any(r => r.UnitId == platId), Is.False);
    }

    [Test]
    public async Task BloodUnitIndex_Search_AvailableOnly_ExcludesNonAvailable()
    {
        // Arrange
        IBloodUnitIndexGrain idx = _cluster.GrainFactory.GetGrain<IBloodUnitIndexGrain>(
            $"BB-UNIT-IDX-AVAIL:{Guid.NewGuid():N}");
        string availId = $"BB-UNIT:{Guid.NewGuid():N}";
        string reservedId = $"BB-UNIT:{Guid.NewGuid():N}";
        await idx.AddOrUpdateAsync(new BloodUnitIndexEntry
        {
            UnitId = availId, ProductType = BloodProductType.PackedRBC,
            AboType = AboBloodType.A, RhType = RhBloodType.Positive,
            Status = BloodUnitStatus.Available
        });
        await idx.AddOrUpdateAsync(new BloodUnitIndexEntry
        {
            UnitId = reservedId, ProductType = BloodProductType.PackedRBC,
            AboType = AboBloodType.A, RhType = RhBloodType.Positive,
            Status = BloodUnitStatus.Reserved
        });

        // Act
        List<BloodUnitIndexEntry> results = await idx.SearchAsync(null, null, null, null, availableOnly: true);

        // Assert
        Assert.That(results.Any(r => r.UnitId == availId), Is.True);
        Assert.That(results.Any(r => r.UnitId == reservedId), Is.False);
    }

    // ─── Crossmatch tests ────────────────────────────────────────────────────

    [Test]
    public async Task Crossmatch_Create_InitialResultIsPending()
    {
        // Arrange
        ICrossmatchGrain xm = NewCrossmatch();
        string patientId = $"PAT-{Guid.NewGuid():N}";
        string unitId = $"BB-UNIT:{Guid.NewGuid():N}";

        // Act
        await xm.CreateAsync(patientId, unitId, CrossmatchUrgency.Routine,
            "NURSE-01", "Nurse Smith",
            "O", "Neg", "O", "Neg", notes: null);
        CrossmatchState state = await xm.GetCrossmatchAsync();

        // Assert
        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.UnitId, Is.EqualTo(unitId));
        Assert.That(state.Urgency, Is.EqualTo(CrossmatchUrgency.Routine));
        Assert.That(state.Result, Is.EqualTo(CrossmatchResult.Pending));
        Assert.That(state.IssuedDate, Is.Null);
    }

    [Test]
    public async Task Crossmatch_RecordResult_SetsResultAndTechnician()
    {
        // Arrange
        ICrossmatchGrain xm = NewCrossmatch();
        await xm.CreateAsync($"PAT-{Guid.NewGuid():N}", $"BB-UNIT:{Guid.NewGuid():N}",
            CrossmatchUrgency.Urgent, "NURSE-02", "Nurse Jones",
            "A", "Pos", "A", "Pos", null);

        // Act
        await xm.RecordResultAsync(CrossmatchResult.Compatible, CrossmatchMethod.Electronic,
            "TECH-01", "Tech Williams", antibodyIdentification: null);
        CrossmatchState state = await xm.GetCrossmatchAsync();

        // Assert
        Assert.That(state.Result, Is.EqualTo(CrossmatchResult.Compatible));
        Assert.That(state.CrossmatchMethod, Is.EqualTo(CrossmatchMethod.Electronic));
        Assert.That(state.TechnicianId, Is.EqualTo("TECH-01"));
        Assert.That(state.TechnicianName, Is.EqualTo("Tech Williams"));
        Assert.That(state.ResultDate, Is.Not.Null);
    }

    [Test]
    public async Task Crossmatch_RecordResult_IncompatibleSetsAntibodyId()
    {
        // Arrange
        ICrossmatchGrain xm = NewCrossmatch();
        await xm.CreateAsync($"PAT-{Guid.NewGuid():N}", $"BB-UNIT:{Guid.NewGuid():N}",
            CrossmatchUrgency.Stat, "NURSE-03", "Nurse Brown",
            "B", "Neg", "A", "Pos", null);

        // Act
        await xm.RecordResultAsync(CrossmatchResult.Incompatible, CrossmatchMethod.Full,
            "TECH-02", "Tech Davis", "Anti-K antibody identified");
        CrossmatchState state = await xm.GetCrossmatchAsync();

        // Assert
        Assert.That(state.Result, Is.EqualTo(CrossmatchResult.Incompatible));
        Assert.That(state.AntibodyIdentification, Is.EqualTo("Anti-K antibody identified"));
    }

    [Test]
    public async Task Crossmatch_IssueUnit_SetsIssuedDateAndTransfusionId()
    {
        // Arrange
        ICrossmatchGrain xm = NewCrossmatch();
        await xm.CreateAsync($"PAT-{Guid.NewGuid():N}", $"BB-UNIT:{Guid.NewGuid():N}",
            CrossmatchUrgency.Stat, "NURSE-04", "Nurse Miller", null, null, null, null, null);
        await xm.RecordResultAsync(CrossmatchResult.Compatible, CrossmatchMethod.AHGPhase,
            "TECH-03", "Tech Garcia", null);
        string transfusionId = $"BB-TX:{Guid.NewGuid():N}";

        // Act
        await xm.IssueUnitAsync("NURSE-04", "Nurse Miller", transfusionId);
        CrossmatchState state = await xm.GetCrossmatchAsync();

        // Assert
        Assert.That(state.IssuedDate, Is.Not.Null);
        Assert.That(state.IssuedByUserId, Is.EqualTo("NURSE-04"));
        Assert.That(state.IssuedByUserName, Is.EqualTo("Nurse Miller"));
        Assert.That(state.TransfusionId, Is.EqualTo(transfusionId));
    }

    [Test]
    public async Task Crossmatch_Cancel_SetsCancelledResult()
    {
        // Arrange
        ICrossmatchGrain xm = NewCrossmatch();
        await xm.CreateAsync($"PAT-{Guid.NewGuid():N}", $"BB-UNIT:{Guid.NewGuid():N}",
            CrossmatchUrgency.Routine, "NURSE-05", "Nurse Taylor", null, null, null, null, null);

        // Act
        await xm.CancelAsync("USER-01", "Patient discharged before transfusion");
        CrossmatchState state = await xm.GetCrossmatchAsync();

        // Assert
        Assert.That(state.Result, Is.EqualTo(CrossmatchResult.Cancelled));
    }

    // ─── CrossmatchIndex tests ───────────────────────────────────────────────

    [Test]
    public async Task CrossmatchIndex_AddOrUpdate_AccumulatesEntries()
    {
        // Arrange — unique patient so index is isolated
        string patientId = $"PAT-{Guid.NewGuid():N}";
        ICrossmatchIndexGrain idx = CrossmatchIndex(patientId);
        string xmId1 = $"BB-XM:{Guid.NewGuid():N}";
        string xmId2 = $"BB-XM:{Guid.NewGuid():N}";

        // Act
        await idx.AddOrUpdateAsync(new CrossmatchIndexEntry
        {
            CrossmatchId = xmId1, UnitId = "UNIT-A",
            Result = CrossmatchResult.Compatible, RequestedDate = DateTime.UtcNow
        });
        await idx.AddOrUpdateAsync(new CrossmatchIndexEntry
        {
            CrossmatchId = xmId2, UnitId = "UNIT-B",
            Result = CrossmatchResult.Pending, RequestedDate = DateTime.UtcNow
        });
        List<CrossmatchIndexEntry> all = await idx.GetAllAsync();

        // Assert
        Assert.That(all, Has.Count.EqualTo(2));
        Assert.That(all.Any(x => x.CrossmatchId == xmId1), Is.True);
        Assert.That(all.Any(x => x.CrossmatchId == xmId2), Is.True);
    }

    [Test]
    public async Task CrossmatchIndex_AddOrUpdate_UpdatesExistingEntry()
    {
        // Arrange
        string patientId = $"PAT-{Guid.NewGuid():N}";
        ICrossmatchIndexGrain idx = CrossmatchIndex(patientId);
        string xmId = $"BB-XM:{Guid.NewGuid():N}";

        // Act — add then update
        await idx.AddOrUpdateAsync(new CrossmatchIndexEntry
        {
            CrossmatchId = xmId, UnitId = "UNIT-C",
            Result = CrossmatchResult.Pending, RequestedDate = DateTime.UtcNow
        });
        await idx.AddOrUpdateAsync(new CrossmatchIndexEntry
        {
            CrossmatchId = xmId, UnitId = "UNIT-C",
            Result = CrossmatchResult.Compatible, IsIssued = true, RequestedDate = DateTime.UtcNow
        });
        List<CrossmatchIndexEntry> all = await idx.GetAllAsync();

        // Assert — still just one entry, with updated result
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].Result, Is.EqualTo(CrossmatchResult.Compatible));
        Assert.That(all[0].IsIssued, Is.True);
    }

    // ─── Transfusion tests ───────────────────────────────────────────────────

    [Test]
    public async Task Transfusion_Start_SetsInProgressAndFields()
    {
        // Arrange
        ITransfusionGrain tx = NewTransfusion();
        string patientId = $"PAT-{Guid.NewGuid():N}";
        string unitId = $"BB-UNIT:{Guid.NewGuid():N}";

        // Act
        await tx.StartAsync(patientId, unitId, crossmatchId: null,
            "PackedRBC", "O", "Negative",
            "NURSE-06", "Nurse Anderson", "DR-01", "Dr. Smith",
            "Right antecubital", "BP 120/80, HR 72");
        TransfusionState state = await tx.GetTransfusionAsync();

        // Assert
        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.UnitId, Is.EqualTo(unitId));
        Assert.That(state.Status, Is.EqualTo(TransfusionStatus.InProgress));
        Assert.That(state.ProductType, Is.EqualTo("PackedRBC"));
        Assert.That(state.AboType, Is.EqualTo("O"));
        Assert.That(state.RhType, Is.EqualTo("Negative"));
        Assert.That(state.AdministeredByUserId, Is.EqualTo("NURSE-06"));
        Assert.That(state.InfusionSite, Is.EqualTo("Right antecubital"));
        Assert.That(state.PreTransfusionVitals, Is.EqualTo("BP 120/80, HR 72"));
        Assert.That(state.EndDateTime, Is.Null);
    }

    [Test]
    public async Task Transfusion_Complete_SetsCompletedStatusAndVolume()
    {
        // Arrange
        ITransfusionGrain tx = NewTransfusion();
        await tx.StartAsync($"PAT-{Guid.NewGuid():N}", $"BB-UNIT:{Guid.NewGuid():N}", null,
            "PackedRBC", "A", "Positive",
            "NURSE-07", "Nurse Lee", "DR-02", "Dr. Johnson",
            null, null);
        DateTime endTime = DateTime.UtcNow.AddHours(2);

        // Act
        await tx.CompleteAsync(endTime, 275m, "BP 122/82, HR 70, no adverse events");
        TransfusionState state = await tx.GetTransfusionAsync();

        // Assert
        Assert.That(state.Status, Is.EqualTo(TransfusionStatus.Completed));
        Assert.That(state.EndDateTime, Is.Not.Null);
        Assert.That(state.VolumeML, Is.EqualTo(275m));
        Assert.That(state.PostTransfusionVitals, Is.EqualTo("BP 122/82, HR 70, no adverse events"));
        Assert.That(state.ReactionType, Is.EqualTo(TransfusionReactionType.None));
    }

    [Test]
    public async Task Transfusion_Stop_SetsReactionStatusAndType()
    {
        // Arrange
        ITransfusionGrain tx = NewTransfusion();
        await tx.StartAsync($"PAT-{Guid.NewGuid():N}", $"BB-UNIT:{Guid.NewGuid():N}", null,
            "PackedRBC", "B", "Positive",
            "NURSE-08", "Nurse Martinez", "DR-03", "Dr. Chen",
            null, null);

        // Act
        await tx.StopAsync(DateTime.UtcNow.AddMinutes(30),
            "Patient developed urticaria",
            TransfusionReactionType.Allergic,
            "Hives noted on trunk 30 minutes into transfusion");
        TransfusionState state = await tx.GetTransfusionAsync();

        // Assert
        Assert.That(state.Status, Is.EqualTo(TransfusionStatus.Reaction));
        Assert.That(state.ReactionType, Is.EqualTo(TransfusionReactionType.Allergic));
        Assert.That(state.StopReason, Is.EqualTo("Patient developed urticaria"));
        Assert.That(state.ReactionNotes, Contains.Substring("Hives"));
        Assert.That(state.EndDateTime, Is.Not.Null);
    }

    [Test]
    public async Task Transfusion_Stop_WithoutReaction_SetsStoppedStatus()
    {
        // Arrange
        ITransfusionGrain tx = NewTransfusion();
        await tx.StartAsync($"PAT-{Guid.NewGuid():N}", $"BB-UNIT:{Guid.NewGuid():N}", null,
            "FreshFrozenPlasma", "AB", "Positive",
            "NURSE-09", "Nurse Wilson", "DR-04", "Dr. Patel",
            null, null);

        // Act
        await tx.StopAsync(DateTime.UtcNow.AddMinutes(10),
            "Physician order to discontinue",
            TransfusionReactionType.None,
            reactionNotes: null);
        TransfusionState state = await tx.GetTransfusionAsync();

        // Assert
        Assert.That(state.Status, Is.EqualTo(TransfusionStatus.Stopped));
        Assert.That(state.ReactionType, Is.EqualTo(TransfusionReactionType.None));
    }

    // ─── TransfusionIndex tests ──────────────────────────────────────────────

    [Test]
    public async Task TransfusionIndex_AddOrUpdate_AccumulatesEntries()
    {
        // Arrange — unique patient ID isolates the index grain
        string patientId = $"PAT-{Guid.NewGuid():N}";
        ITransfusionIndexGrain idx = TransfusionIndex(patientId);
        string txId1 = $"BB-TX:{Guid.NewGuid():N}";
        string txId2 = $"BB-TX:{Guid.NewGuid():N}";

        // Act
        await idx.AddOrUpdateAsync(new TransfusionIndexEntry
        {
            TransfusionId = txId1, UnitId = "UNIT-X",
            ProductType = "PackedRBC", AboType = "O", RhType = "Neg",
            StartDateTime = DateTime.UtcNow, Status = TransfusionStatus.Completed
        });
        await idx.AddOrUpdateAsync(new TransfusionIndexEntry
        {
            TransfusionId = txId2, UnitId = "UNIT-Y",
            ProductType = "Platelets", AboType = "A", RhType = "Pos",
            StartDateTime = DateTime.UtcNow, Status = TransfusionStatus.InProgress
        });
        List<TransfusionIndexEntry> all = await idx.GetAllAsync();

        // Assert
        Assert.That(all, Has.Count.EqualTo(2));
        Assert.That(all.Any(t => t.TransfusionId == txId1), Is.True);
        Assert.That(all.Any(t => t.TransfusionId == txId2), Is.True);
    }
}
