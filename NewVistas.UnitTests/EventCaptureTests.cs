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
/// Unit tests for VistA Event Capture — Files #721 (EC PATIENT, EC ENCOUNTER) and #724 (DSS UNIT).
/// Tests individual grains directly via TestCluster and workflow grain integration.
/// MUMPS routines: ECPEC.m, ECPEEN.m, ECPEWL.m, ECPEDSS.m.
/// </summary>
[TestFixture]
public class EventCaptureTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IEventCaptureEncounterGrain NewEncounterGrain()
        => _cluster.GrainFactory.GetGrain<IEventCaptureEncounterGrain>(
            $"EC-ENCOUNTER:{Guid.NewGuid():N}");

    private IEventCapturePatientGrain PatientGrain(string patientId)
        => _cluster.GrainFactory.GetGrain<IEventCapturePatientGrain>(
            $"EC-PATIENT:{patientId}");

    private IEventCaptureEncounterIndexGrain EncounterIndex()
        => _cluster.GrainFactory.GetGrain<IEventCaptureEncounterIndexGrain>("EC-ENCOUNTER-IDX");

    private IDssUnitGrain NewDssUnitGrain()
        => _cluster.GrainFactory.GetGrain<IDssUnitGrain>(
            $"EC-DSS-UNIT:{Guid.NewGuid():N}");

    private IDssUnitIndexGrain DssUnitIndex()
        => _cluster.GrainFactory.GetGrain<IDssUnitIndexGrain>("EC-DSS-IDX");

    // ── EventCaptureEncounterGrain tests ──────────────────────────────────────

    [Test]
    public async Task EventCaptureEncounterGrain_CanCreate()
    {
        // Arrange
        IEventCaptureEncounterGrain grain = NewEncounterGrain();
        DateTime encounterTime = DateTime.UtcNow.AddHours(-1);

        // Act
        await grain.CreateAsync(
            patientId: "PATIENT-001",
            encounterDateTime: encounterTime,
            dssUnitId: "EC-DSS-UNIT:PC",
            dssUnitName: "PRIMARY CARE",
            dssUnitCode: "PC",
            clinicId: "CLINIC-001",
            clinicName: "Primary Care Clinic",
            locationId: "LOC-001",
            locationName: "Building A",
            primaryProviderId: "PROV-001",
            primaryProviderName: "Dr. Smith",
            attendingProviderId: null,
            attendingProviderName: null,
            encounterType: EcEncounterType.Outpatient,
            patientCategory: EcPatientCategory.ServiceConnected,
            primaryStopCode: "323",
            creditStopCode: null,
            comments: "Routine visit");

        // Assert
        EventCaptureEncounterState state = await grain.GetEncounterAsync();
        Assert.That(state.PatientId, Is.EqualTo("PATIENT-001"));
        Assert.That(state.DssUnitName, Is.EqualTo("PRIMARY CARE"));
        Assert.That(state.DssUnitCode, Is.EqualTo("PC"));
        Assert.That(state.PrimaryProviderName, Is.EqualTo("Dr. Smith"));
        Assert.That(state.EncounterType, Is.EqualTo(EcEncounterType.Outpatient));
        Assert.That(state.PatientCategory, Is.EqualTo(EcPatientCategory.ServiceConnected));
        Assert.That(state.Status, Is.EqualTo(EcEncounterStatus.Open));
        Assert.That(state.PrimaryStopCode, Is.EqualTo("323"));
        Assert.That(state.Procedures, Is.Empty);
        Assert.That(state.Diagnoses, Is.Empty);
        Assert.That(state.CreatedDate, Is.Not.EqualTo(DateTime.MinValue));
    }

    [Test]
    public async Task EventCaptureEncounterGrain_CanAddProcedure()
    {
        // Arrange
        IEventCaptureEncounterGrain grain = NewEncounterGrain();
        await grain.CreateAsync("PATIENT-002", DateTime.UtcNow, "EC-DSS-UNIT:PT",
            "PHYSICAL THERAPY", "PT", null, null, null, null,
            "PROV-002", "Dr. Jones", null, null,
            EcEncounterType.Outpatient, EcPatientCategory.NonServiceConnected,
            "350", null, null);

        // Act
        await grain.AddProcedureAsync("97110", "Therapeutic exercises", 2, "PROV-002", "Dr. Jones", null);
        await grain.AddProcedureAsync("97014", "Electrical stimulation", 1, "PROV-002", "Dr. Jones", "-59");

        // Assert
        EventCaptureEncounterState state = await grain.GetEncounterAsync();
        Assert.That(state.Procedures, Has.Count.EqualTo(2));

        EcProcedureEntry proc1 = state.Procedures.First(p => p.CptCode == "97110");
        Assert.That(proc1.Quantity, Is.EqualTo(2));
        Assert.That(proc1.ProviderName, Is.EqualTo("Dr. Jones"));
        Assert.That(proc1.ModifierCode, Is.Null);

        EcProcedureEntry proc2 = state.Procedures.First(p => p.CptCode == "97014");
        Assert.That(proc2.ModifierCode, Is.EqualTo("-59"));
    }

    [Test]
    public async Task EventCaptureEncounterGrain_AddProcedure_ReplacesExistingByCodeAndProvider()
    {
        // Arrange
        IEventCaptureEncounterGrain grain = NewEncounterGrain();
        await grain.CreateAsync("PATIENT-003", DateTime.UtcNow, "EC-DSS-UNIT:MH",
            "MENTAL HEALTH", "MH", null, null, null, null,
            "PROV-003", "Dr. Brown", null, null,
            EcEncounterType.Outpatient, EcPatientCategory.ServiceConnected,
            null, null, null);

        // Act — add same CPT/provider twice (should replace, not duplicate)
        await grain.AddProcedureAsync("90837", "Psychotherapy 60 min", 1, "PROV-003", "Dr. Brown", null);
        await grain.AddProcedureAsync("90837", "Psychotherapy 60 min", 2, "PROV-003", "Dr. Brown", null);

        // Assert
        EventCaptureEncounterState state = await grain.GetEncounterAsync();
        Assert.That(state.Procedures, Has.Count.EqualTo(1));
        Assert.That(state.Procedures[0].Quantity, Is.EqualTo(2));
    }

    [Test]
    public async Task EventCaptureEncounterGrain_CanAddDiagnosis()
    {
        // Arrange
        IEventCaptureEncounterGrain grain = NewEncounterGrain();
        await grain.CreateAsync("PATIENT-004", DateTime.UtcNow, "EC-DSS-UNIT:PC",
            "PRIMARY CARE", "PC", null, null, null, null,
            "PROV-004", "Dr. Lee", null, null,
            EcEncounterType.Outpatient, EcPatientCategory.NonServiceConnected,
            "323", null, null);

        // Act
        await grain.AddDiagnosisAsync("Z00.00", "Encounter for general adult medical exam", true);
        await grain.AddDiagnosisAsync("I10", "Essential (primary) hypertension", false);

        // Assert
        EventCaptureEncounterState state = await grain.GetEncounterAsync();
        Assert.That(state.Diagnoses, Has.Count.EqualTo(2));

        EcDiagnosisEntry primary = state.Diagnoses.Single(d => d.IsPrimary);
        Assert.That(primary.Icd10Code, Is.EqualTo("Z00.00"));

        EcDiagnosisEntry secondary = state.Diagnoses.Single(d => !d.IsPrimary);
        Assert.That(secondary.Icd10Code, Is.EqualTo("I10"));
    }

    [Test]
    public async Task EventCaptureEncounterGrain_CanComplete()
    {
        // Arrange
        IEventCaptureEncounterGrain grain = NewEncounterGrain();
        await grain.CreateAsync("PATIENT-005", DateTime.UtcNow.AddHours(-2),
            "EC-DSS-UNIT:PC", "PRIMARY CARE", "PC",
            null, null, null, null,
            "PROV-005", "Dr. Kim", null, null,
            EcEncounterType.Outpatient, EcPatientCategory.ServiceConnected,
            "323", null, null);

        // Act
        DateTime checkOut = DateTime.UtcNow;
        await grain.CompleteAsync(checkOut, 45);

        // Assert
        EventCaptureEncounterState state = await grain.GetEncounterAsync();
        Assert.That(state.Status, Is.EqualTo(EcEncounterStatus.Complete));
        Assert.That(state.CheckOutDateTime, Is.Not.Null);
        Assert.That(state.VisitLengthMinutes, Is.EqualTo(45));
    }

    [Test]
    public async Task EventCaptureEncounterGrain_CanDelete()
    {
        // Arrange
        IEventCaptureEncounterGrain grain = NewEncounterGrain();
        await grain.CreateAsync("PATIENT-006", DateTime.UtcNow,
            "EC-DSS-UNIT:PC", "PRIMARY CARE", "PC",
            null, null, null, null,
            "PROV-006", "Dr. Clark", null, null,
            EcEncounterType.Outpatient, EcPatientCategory.NonServiceConnected,
            null, null, null);

        // Act
        await grain.DeleteAsync("PROV-006", "Dr. Clark", "Created in error");

        // Assert
        EventCaptureEncounterState state = await grain.GetEncounterAsync();
        Assert.That(state.Status, Is.EqualTo(EcEncounterStatus.Deleted));
        Assert.That(state.DeleteReason, Is.EqualTo("Created in error"));
        Assert.That(state.DeletedByProviderName, Is.EqualTo("Dr. Clark"));
        Assert.That(state.DeletedDate, Is.Not.Null);
    }

    [Test]
    public async Task EventCaptureEncounterGrain_CanRemoveProcedure()
    {
        // Arrange
        IEventCaptureEncounterGrain grain = NewEncounterGrain();
        await grain.CreateAsync("PATIENT-007", DateTime.UtcNow, "EC-DSS-UNIT:PT",
            "PHYSICAL THERAPY", "PT", null, null, null, null,
            "PROV-007", "Dr. White", null, null,
            EcEncounterType.Outpatient, EcPatientCategory.ServiceConnected,
            null, null, null);

        await grain.AddProcedureAsync("97110", "Therapeutic exercises", 3, "PROV-007", "Dr. White", null);
        await grain.AddProcedureAsync("97014", "E-stim", 1, "PROV-007", "Dr. White", null);

        // Act
        await grain.RemoveProcedureAsync("97014", "PROV-007");

        // Assert
        EventCaptureEncounterState state = await grain.GetEncounterAsync();
        Assert.That(state.Procedures, Has.Count.EqualTo(1));
        Assert.That(state.Procedures[0].CptCode, Is.EqualTo("97110"));
    }

    // ── EventCapturePatientGrain tests ────────────────────────────────────────

    [Test]
    public async Task EventCapturePatientGrain_CanAddEncounters()
    {
        // Arrange
        string patientId = $"PATIENT-{Guid.NewGuid():N}";
        IEventCapturePatientGrain grain = PatientGrain(patientId);

        // Act
        DateTime dt1 = DateTime.UtcNow.AddDays(-2);
        DateTime dt2 = DateTime.UtcNow.AddDays(-1);
        await grain.AddEncounterAsync("EC-ENCOUNTER:A", dt1);
        await grain.AddEncounterAsync("EC-ENCOUNTER:B", dt2);

        // Assert
        EventCapturePatientState state = await grain.GetAsync();
        Assert.That(state.TotalEncounters, Is.EqualTo(2));
        Assert.That(state.LastEncounterDate, Is.EqualTo(dt2));

        // Newest first
        List<EcPatientEncounterEntry> entries = await grain.GetEncounterEntriesAsync(10);
        Assert.That(entries[0].EncounterId, Is.EqualTo("EC-ENCOUNTER:B"));
        Assert.That(entries[1].EncounterId, Is.EqualTo("EC-ENCOUNTER:A"));
    }

    [Test]
    public async Task EventCapturePatientGrain_AddEncounter_IgnoresDuplicate()
    {
        // Arrange
        string patientId = $"PATIENT-{Guid.NewGuid():N}";
        IEventCapturePatientGrain grain = PatientGrain(patientId);

        // Act
        await grain.AddEncounterAsync("EC-ENCOUNTER:DUP", DateTime.UtcNow);
        await grain.AddEncounterAsync("EC-ENCOUNTER:DUP", DateTime.UtcNow);

        // Assert
        EventCapturePatientState state = await grain.GetAsync();
        Assert.That(state.TotalEncounters, Is.EqualTo(1));
    }

    // ── EventCaptureEncounterIndexGrain tests ─────────────────────────────────

    [Test]
    public async Task EncounterIndexGrain_CanSearchByDssUnit()
    {
        // Arrange — use isolated index key per test to avoid cross-test pollution
        IEventCaptureEncounterIndexGrain index =
            _cluster.GrainFactory.GetGrain<IEventCaptureEncounterIndexGrain>(
                $"EC-ENC-IDX-TEST-{Guid.NewGuid():N}");

        EventCaptureIndexEntry entry1 = new()
        {
            EncounterId = $"EC-{Guid.NewGuid():N}",
            PatientId = "P-001",
            EncounterDateTime = DateTime.UtcNow.AddHours(-5),
            DssUnitId = "EC-DSS-UNIT:PC",
            DssUnitName = "PRIMARY CARE",
            DssUnitCode = "PC",
            PrimaryProviderId = "PROV-A",
            PrimaryProviderName = "Dr. A",
            EncounterType = EcEncounterType.Outpatient,
            Status = EcEncounterStatus.Open,
        };
        EventCaptureIndexEntry entry2 = new()
        {
            EncounterId = $"EC-{Guid.NewGuid():N}",
            PatientId = "P-002",
            EncounterDateTime = DateTime.UtcNow.AddHours(-3),
            DssUnitId = "EC-DSS-UNIT:PT",
            DssUnitName = "PHYSICAL THERAPY",
            DssUnitCode = "PT",
            PrimaryProviderId = "PROV-B",
            PrimaryProviderName = "Dr. B",
            EncounterType = EcEncounterType.Outpatient,
            Status = EcEncounterStatus.Complete,
        };

        await index.AddOrUpdateAsync(entry1);
        await index.AddOrUpdateAsync(entry2);

        // Act
        List<EventCaptureIndexEntry> pcResults =
            await index.SearchAsync(null, "EC-DSS-UNIT:PC", null, null, null, null, 50);
        List<EventCaptureIndexEntry> ptResults =
            await index.SearchAsync(null, "EC-DSS-UNIT:PT", null, null, null, null, 50);

        // Assert
        Assert.That(pcResults, Has.Count.EqualTo(1));
        Assert.That(pcResults[0].DssUnitCode, Is.EqualTo("PC"));

        Assert.That(ptResults, Has.Count.EqualTo(1));
        Assert.That(ptResults[0].Status, Is.EqualTo(EcEncounterStatus.Complete));
    }

    [Test]
    public async Task EncounterIndexGrain_CanSearchByDateRange()
    {
        // Arrange
        IEventCaptureEncounterIndexGrain index =
            _cluster.GrainFactory.GetGrain<IEventCaptureEncounterIndexGrain>(
                $"EC-ENC-IDX-DATE-{Guid.NewGuid():N}");

        DateTime yesterday = DateTime.UtcNow.AddDays(-1);
        DateTime today = DateTime.UtcNow;
        DateTime tomorrow = DateTime.UtcNow.AddDays(1);

        await index.AddOrUpdateAsync(new EventCaptureIndexEntry
        {
            EncounterId = $"EC-{Guid.NewGuid():N}",
            PatientId = "P-001",
            EncounterDateTime = yesterday,
            DssUnitId = "DSS-1",
            DssUnitName = "Test Unit",
            PrimaryProviderId = "P",
            PrimaryProviderName = "Dr. X",
            EncounterType = EcEncounterType.Outpatient,
            Status = EcEncounterStatus.Complete,
        });
        await index.AddOrUpdateAsync(new EventCaptureIndexEntry
        {
            EncounterId = $"EC-{Guid.NewGuid():N}",
            PatientId = "P-002",
            EncounterDateTime = today,
            DssUnitId = "DSS-1",
            DssUnitName = "Test Unit",
            PrimaryProviderId = "P",
            PrimaryProviderName = "Dr. X",
            EncounterType = EcEncounterType.Outpatient,
            Status = EcEncounterStatus.Open,
        });

        // Act — search only for today
        List<EventCaptureIndexEntry> results =
            await index.SearchAsync(null, null, null, null, today.Date, tomorrow, 50);

        // Assert
        Assert.That(results.All(e => e.EncounterDateTime >= today.Date), Is.True);
        Assert.That(results.Any(e => e.EncounterDateTime < today.Date), Is.False);
    }

    // ── DssUnitGrain tests ────────────────────────────────────────────────────

    [Test]
    public async Task DssUnitGrain_CanCreate()
    {
        // Arrange
        IDssUnitGrain grain = NewDssUnitGrain();

        // Act
        await grain.UpsertAsync(
            unitName: "PRIMARY CARE",
            unitCode: "PC",
            divisionId: null,
            divisionName: "Main Division",
            primaryStopCode: "323",
            creditStopCode: null,
            treatmentCode: null,
            description: "Primary care services",
            isActive: true);

        // Assert
        DssUnitState state = await grain.GetUnitAsync();
        Assert.That(state.UnitName, Is.EqualTo("PRIMARY CARE"));
        Assert.That(state.UnitCode, Is.EqualTo("PC"));
        Assert.That(state.PrimaryStopCode, Is.EqualTo("323"));
        Assert.That(state.IsActive, Is.True);
        Assert.That(state.DivisionName, Is.EqualTo("Main Division"));
    }

    [Test]
    public async Task DssUnitGrain_CanDeactivateAndReactivate()
    {
        // Arrange
        IDssUnitGrain grain = NewDssUnitGrain();
        await grain.UpsertAsync("PHYSICAL THERAPY", "PT", null, null, "350", null, null, null, true);

        // Act — deactivate
        await grain.DeactivateAsync();
        DssUnitState state = await grain.GetUnitAsync();
        Assert.That(state.IsActive, Is.False);

        // Act — reactivate
        await grain.ReactivateAsync();
        state = await grain.GetUnitAsync();
        Assert.That(state.IsActive, Is.True);
    }

    // ── DssUnitIndexGrain tests ───────────────────────────────────────────────

    [Test]
    public async Task DssUnitIndexGrain_CanSearchByName()
    {
        // Arrange — isolated index key to avoid cross-test pollution
        IDssUnitIndexGrain index =
            _cluster.GrainFactory.GetGrain<IDssUnitIndexGrain>(
                $"EC-DSS-IDX-TEST-{Guid.NewGuid():N}");

        await index.AddOrUpdateAsync(new DssUnitIndexEntry
        {
            DssUnitId = "EC-DSS-UNIT:001",
            UnitName = "PRIMARY CARE",
            UnitCode = "PC",
            IsActive = true,
        });
        await index.AddOrUpdateAsync(new DssUnitIndexEntry
        {
            DssUnitId = "EC-DSS-UNIT:002",
            UnitName = "PHYSICAL THERAPY",
            UnitCode = "PT",
            IsActive = true,
        });
        await index.AddOrUpdateAsync(new DssUnitIndexEntry
        {
            DssUnitId = "EC-DSS-UNIT:003",
            UnitName = "MENTAL HEALTH",
            UnitCode = "MH",
            IsActive = false,
        });

        // Act
        List<DssUnitIndexEntry> primaryResults = await index.SearchAsync("primary", true, 50);
        List<DssUnitIndexEntry> allActive = await index.SearchAsync(null, true, 50);
        List<DssUnitIndexEntry> all = await index.SearchAsync(null, false, 50);

        // Assert
        Assert.That(primaryResults, Has.Count.EqualTo(1));
        Assert.That(primaryResults[0].UnitCode, Is.EqualTo("PC"));

        Assert.That(allActive, Has.Count.EqualTo(2));
        Assert.That(all, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task DssUnitIndexGrain_AddOrUpdate_ReplacesExisting()
    {
        // Arrange
        IDssUnitIndexGrain index =
            _cluster.GrainFactory.GetGrain<IDssUnitIndexGrain>(
                $"EC-DSS-IDX-UPD-{Guid.NewGuid():N}");

        string unitId = "EC-DSS-UNIT:UPDATE-TEST";

        await index.AddOrUpdateAsync(new DssUnitIndexEntry
        {
            DssUnitId = unitId,
            UnitName = "OLD NAME",
            UnitCode = "ON",
            IsActive = true,
        });

        // Act — update with new name
        await index.AddOrUpdateAsync(new DssUnitIndexEntry
        {
            DssUnitId = unitId,
            UnitName = "NEW NAME",
            UnitCode = "NN",
            IsActive = false,
        });

        // Assert — only one entry, updated
        List<DssUnitIndexEntry> all = await index.GetAllAsync();
        DssUnitIndexEntry updated = all.Single(u => u.DssUnitId == unitId);
        Assert.That(updated.UnitName, Is.EqualTo("NEW NAME"));
        Assert.That(updated.IsActive, Is.False);
    }

    // ── Workflow grain integration tests ──────────────────────────────────────

    [Test]
    public async Task WorkflowGrain_CanCreateAndListEncounters()
    {
        // Arrange
        string patientId = $"PATIENT-WF-{Guid.NewGuid():N}";
        IPatientWorkflowGrain workflow =
            _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        // Act
        string enc1 = await workflow.CreateEventCaptureEncounterAsync(
            DateTime.UtcNow.AddHours(-3),
            "EC-DSS-UNIT:PC", "PRIMARY CARE", "PC",
            null, "Primary Care Clinic", null, null,
            "PROV-A", "Dr. Alpha",
            null, null,
            EcEncounterType.Outpatient,
            EcPatientCategory.ServiceConnected,
            "323", null, "Test encounter 1");

        string enc2 = await workflow.CreateEventCaptureEncounterAsync(
            DateTime.UtcNow.AddHours(-1),
            "EC-DSS-UNIT:PT", "PHYSICAL THERAPY", "PT",
            null, "PT Clinic", null, null,
            "PROV-B", "Dr. Beta",
            null, null,
            EcEncounterType.Outpatient,
            EcPatientCategory.ServiceConnected,
            "350", null, "Test encounter 2");

        // Assert — list
        List<EventCaptureIndexEntry> encounters =
            await workflow.GetEventCaptureEncountersAsync(50);

        Assert.That(encounters.Count, Is.GreaterThanOrEqualTo(2));
        Assert.That(encounters.Any(e => e.EncounterId == enc1), Is.True);
        Assert.That(encounters.Any(e => e.EncounterId == enc2), Is.True);
    }

    [Test]
    public async Task WorkflowGrain_CanAddProcedureAndCompleteEncounter()
    {
        // Arrange
        string patientId = $"PATIENT-WF-{Guid.NewGuid():N}";
        IPatientWorkflowGrain workflow =
            _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        string encounterId = await workflow.CreateEventCaptureEncounterAsync(
            DateTime.UtcNow.AddHours(-2),
            "EC-DSS-UNIT:PC", "PRIMARY CARE", "PC",
            null, null, null, null,
            "PROV-C", "Dr. Gamma",
            null, null,
            EcEncounterType.Outpatient,
            EcPatientCategory.NonServiceConnected,
            null, null, null);

        // Act — add procedure then complete
        await workflow.AddEcProcedureAsync(
            encounterId, "99213", "Office Visit E/M Level 3", 1,
            "PROV-C", "Dr. Gamma", null);

        await workflow.CompleteEventCaptureEncounterAsync(
            encounterId, DateTime.UtcNow, 30);

        // Assert — get detail
        EventCaptureEncounterState state =
            await workflow.GetEventCaptureEncounterAsync(encounterId);

        Assert.That(state.Status, Is.EqualTo(EcEncounterStatus.Complete));
        Assert.That(state.Procedures, Has.Count.EqualTo(1));
        Assert.That(state.Procedures[0].CptCode, Is.EqualTo("99213"));
        Assert.That(state.VisitLengthMinutes, Is.EqualTo(30));
    }

    [Test]
    public async Task WorkflowGrain_CanDeleteEncounter()
    {
        // Arrange
        string patientId = $"PATIENT-WF-{Guid.NewGuid():N}";
        IPatientWorkflowGrain workflow =
            _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        string encounterId = await workflow.CreateEventCaptureEncounterAsync(
            DateTime.UtcNow,
            "EC-DSS-UNIT:MH", "MENTAL HEALTH", "MH",
            null, null, null, null,
            "PROV-D", "Dr. Delta",
            null, null,
            EcEncounterType.Telephone,
            EcPatientCategory.ServiceConnected,
            null, null, null);

        // Act
        await workflow.DeleteEventCaptureEncounterAsync(
            encounterId, "PROV-D", "Dr. Delta", "Created in error");

        // Assert
        EventCaptureEncounterState state =
            await workflow.GetEventCaptureEncounterAsync(encounterId);
        Assert.That(state.Status, Is.EqualTo(EcEncounterStatus.Deleted));
        Assert.That(state.DeleteReason, Is.EqualTo("Created in error"));
    }
}
