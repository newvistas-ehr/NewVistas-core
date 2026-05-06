// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for VistA Controlled Substances module -- File #58.80/#58.82.
/// System-level grains; no workflow grain involvement.
/// Tests end-to-end inspection + dispense workflows via direct grain factory access.
/// </summary>
[TestFixture]
public class ControlledSubstancesWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private ICSInspectionGrain GetInspectionGrain(string id) =>
        _cluster.GrainFactory.GetGrain<ICSInspectionGrain>($"CS-INSPECTION:{id}");

    private ICSInspectionLogGrain GetInspectionLog(string locationId) =>
        _cluster.GrainFactory.GetGrain<ICSInspectionLogGrain>($"CS-INSPECT-LOG:{locationId}");

    private ICSDispenseRecordGrain GetDispenseGrain(string id) =>
        _cluster.GrainFactory.GetGrain<ICSDispenseRecordGrain>($"CS-DISPENSE:{id}");

    private ICSDispenseLogGrain GetDispenseLog(string locationId) =>
        _cluster.GrainFactory.GetGrain<ICSDispenseLogGrain>($"CS-DISPENSE-LOG:{locationId}");

    private static async Task CreateDefaultInspection(ICSInspectionGrain grain)
    {
        await grain.CreateInspectionAsync(
            "VAULT-1A", "Pharmacy Vault 1A",
            CSInspectionType.Scheduled, DateTime.UtcNow,
            "INS-001", "Jane Inspector",
            "WIT-001", "Bob Witness",
            null, null, "Routine monthly inspection");
    }

    private static async Task CreateDefaultDispense(ICSDispenseRecordGrain grain)
    {
        await grain.CreateRecordAsync(
            "VAULT-1A", "Pharmacy Vault 1A",
            "PAT-001", "John Doe", new DateTime(1960, 3, 15),
            "DRUG-001", "Morphine Sulfate 15mg",
            DEADrugSchedule.ScheduleII, "12345-6789-01",
            2.0m, "tablets", 48.0m,
            CSDispenseType.Routine,
            "PRV-001", "Dr. Smith", "AB1234567",
            "RPH-001", "Jane Pharmacist",
            "WIT-001", "Bob Witness",
            DateTime.UtcNow, "RX-001", "ORD-001", null);
    }

    // ── 1 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task Inspection_Create_PersistsAllFields()
    {
        string id = Guid.NewGuid().ToString("N");
        ICSInspectionGrain grain = GetInspectionGrain(id);

        await CreateDefaultInspection(grain);

        CSInspectionState state = await grain.GetInspectionAsync();
        Assert.That(state.LocationId, Is.EqualTo("VAULT-1A"));
        Assert.That(state.LocationName, Is.EqualTo("Pharmacy Vault 1A"));
        Assert.That(state.InspectionType, Is.EqualTo(CSInspectionType.Scheduled));
        Assert.That(state.InspectorName, Is.EqualTo("Jane Inspector"));
        Assert.That(state.WitnessName, Is.EqualTo("Bob Witness"));
        Assert.That(state.Notes, Is.EqualTo("Routine monthly inspection"));
    }

    // ── 2 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task Inspection_AddDrugCount_AppendsToList()
    {
        string id = Guid.NewGuid().ToString("N");
        ICSInspectionGrain grain = GetInspectionGrain(id);
        await CreateDefaultInspection(grain);

        CSInspectionCount count = new CSInspectionCount
        {
            DrugName = "Oxycodone 5mg",
            DrugSchedule = DEADrugSchedule.ScheduleII,
            SystemCount = 100m,
            PhysicalCount = 100m,
            Discrepancy = 0m,
            CountUnit = "tablets",
            Notes = null
        };
        await grain.AddDrugCountAsync(count);

        CSInspectionState state = await grain.GetInspectionAsync();
        Assert.That(state.DrugCounts, Has.Count.EqualTo(1));
        Assert.That(state.DrugCounts[0].DrugName, Is.EqualTo("Oxycodone 5mg"));
        Assert.That(state.DrugCounts[0].Discrepancy, Is.EqualTo(0m));
    }

    // ── 3 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task Inspection_MultipleDrugCounts_AllRecorded()
    {
        string id = Guid.NewGuid().ToString("N");
        ICSInspectionGrain grain = GetInspectionGrain(id);
        await CreateDefaultInspection(grain);

        await grain.AddDrugCountAsync(new CSInspectionCount
        {
            DrugName = "Oxycodone 5mg", DrugSchedule = DEADrugSchedule.ScheduleII,
            SystemCount = 100, PhysicalCount = 100, Discrepancy = 0, CountUnit = "tablets"
        });
        await grain.AddDrugCountAsync(new CSInspectionCount
        {
            DrugName = "Morphine Sulfate 15mg", DrugSchedule = DEADrugSchedule.ScheduleII,
            SystemCount = 50, PhysicalCount = 48, Discrepancy = -2, CountUnit = "tablets"
        });

        CSInspectionState state = await grain.GetInspectionAsync();
        Assert.That(state.DrugCounts, Has.Count.EqualTo(2));
    }

    // ── 4 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task Inspection_Finalize_SetsResultAndDiscrepancy()
    {
        string id = Guid.NewGuid().ToString("N");
        ICSInspectionGrain grain = GetInspectionGrain(id);
        await CreateDefaultInspection(grain);

        await grain.AddDrugCountAsync(new CSInspectionCount
        {
            DrugName = "Morphine Sulfate", DrugSchedule = DEADrugSchedule.ScheduleII,
            SystemCount = 50, PhysicalCount = 48, Discrepancy = -2, CountUnit = "tablets"
        });

        await grain.FinalizeInspectionAsync(
            CSInspectionResult.Failed, true,
            "SUP-001", "Supervisor Jones", "Two tablets unaccounted for");

        CSInspectionState state = await grain.GetInspectionAsync();
        Assert.That(state.OverallResult, Is.EqualTo(CSInspectionResult.Failed));
        Assert.That(state.DiscrepanciesReported, Is.True);
        Assert.That(state.ReportedToName, Is.EqualTo("Supervisor Jones"));
        Assert.That(state.InvestigationNotes, Is.EqualTo("Two tablets unaccounted for"));
    }

    // ── 5 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task Inspection_Finalize_Passed_NoDiscrepancies()
    {
        string id = Guid.NewGuid().ToString("N");
        ICSInspectionGrain grain = GetInspectionGrain(id);
        await CreateDefaultInspection(grain);

        await grain.FinalizeInspectionAsync(
            CSInspectionResult.Passed, false, null, null, null);

        CSInspectionState state = await grain.GetInspectionAsync();
        Assert.That(state.OverallResult, Is.EqualTo(CSInspectionResult.Passed));
        Assert.That(state.DiscrepanciesReported, Is.False);
    }

    // ── 6 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task InspectionLog_Upsert_AndGetAll()
    {
        string locationId = $"LOC-{Guid.NewGuid():N}";
        ICSInspectionLogGrain log = GetInspectionLog(locationId);

        CSInspectionSummaryEntry entry = new CSInspectionSummaryEntry
        {
            InspectionId = Guid.NewGuid().ToString("N"),
            LocationId = locationId,
            InspectionType = CSInspectionType.Scheduled,
            InspectionDateTime = DateTime.UtcNow,
            InspectorName = "Jane Inspector",
            OverallResult = CSInspectionResult.Passed,
            TotalDiscrepancies = 0,
            CreatedDate = DateTime.UtcNow
        };
        await log.UpsertInspectionAsync(entry);

        List<CSInspectionSummaryEntry> all = await log.GetAllInspectionsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].InspectorName, Is.EqualTo("Jane Inspector"));
    }

    // ── 7 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task InspectionLog_GetByType_FiltersCorrectly()
    {
        string locationId = $"LOC-{Guid.NewGuid():N}";
        ICSInspectionLogGrain log = GetInspectionLog(locationId);

        await log.UpsertInspectionAsync(new CSInspectionSummaryEntry
        {
            InspectionId = Guid.NewGuid().ToString("N"), LocationId = locationId,
            InspectionType = CSInspectionType.Scheduled, InspectionDateTime = DateTime.UtcNow,
            InspectorName = "Inspector A", OverallResult = CSInspectionResult.Passed
        });
        await log.UpsertInspectionAsync(new CSInspectionSummaryEntry
        {
            InspectionId = Guid.NewGuid().ToString("N"), LocationId = locationId,
            InspectionType = CSInspectionType.Unscheduled, InspectionDateTime = DateTime.UtcNow,
            InspectorName = "Inspector B", OverallResult = CSInspectionResult.Passed
        });

        List<CSInspectionSummaryEntry> scheduled = await log.GetInspectionsByTypeAsync(CSInspectionType.Scheduled);
        Assert.That(scheduled, Has.Count.EqualTo(1));
        Assert.That(scheduled[0].InspectorName, Is.EqualTo("Inspector A"));
    }

    // ── 8 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task InspectionLog_GetFailed_ReturnsOnlyFailures()
    {
        string locationId = $"LOC-{Guid.NewGuid():N}";
        ICSInspectionLogGrain log = GetInspectionLog(locationId);

        await log.UpsertInspectionAsync(new CSInspectionSummaryEntry
        {
            InspectionId = Guid.NewGuid().ToString("N"), LocationId = locationId,
            InspectionType = CSInspectionType.Scheduled, InspectionDateTime = DateTime.UtcNow,
            InspectorName = "Inspector A", OverallResult = CSInspectionResult.Passed
        });
        await log.UpsertInspectionAsync(new CSInspectionSummaryEntry
        {
            InspectionId = Guid.NewGuid().ToString("N"), LocationId = locationId,
            InspectionType = CSInspectionType.DiscrepancyFollowUp, InspectionDateTime = DateTime.UtcNow,
            InspectorName = "Inspector B", OverallResult = CSInspectionResult.Failed, TotalDiscrepancies = 2
        });

        List<CSInspectionSummaryEntry> failed = await log.GetFailedInspectionsAsync();
        Assert.That(failed, Has.Count.EqualTo(1));
        Assert.That(failed[0].OverallResult, Is.EqualTo(CSInspectionResult.Failed));
    }

    // ── 9 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task Dispense_Create_PersistsAllFields()
    {
        string id = Guid.NewGuid().ToString("N");
        ICSDispenseRecordGrain grain = GetDispenseGrain(id);

        await CreateDefaultDispense(grain);

        CSDispenseRecordState state = await grain.GetRecordAsync();
        Assert.That(state.LocationId, Is.EqualTo("VAULT-1A"));
        Assert.That(state.PatientName, Is.EqualTo("John Doe"));
        Assert.That(state.DrugName, Is.EqualTo("Morphine Sulfate 15mg"));
        Assert.That(state.DEASchedule, Is.EqualTo(DEADrugSchedule.ScheduleII));
        Assert.That(state.QuantityDispensed, Is.EqualTo(2.0m));
        Assert.That(state.RunningBalance, Is.EqualTo(48.0m));
        Assert.That(state.DispenseType, Is.EqualTo(CSDispenseType.Routine));
        Assert.That(state.PrescriberName, Is.EqualTo("Dr. Smith"));
        Assert.That(state.DispensedByName, Is.EqualTo("Jane Pharmacist"));
        Assert.That(state.WitnessName, Is.EqualTo("Bob Witness"));
    }

    // ── 10 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task DispenseLog_Upsert_AndGetAll()
    {
        string locationId = $"LOC-{Guid.NewGuid():N}";
        ICSDispenseLogGrain log = GetDispenseLog(locationId);

        CSDispenseSummaryEntry entry = new CSDispenseSummaryEntry
        {
            RecordId = Guid.NewGuid().ToString("N"),
            LocationId = locationId,
            PatientName = "John Doe",
            DrugName = "Morphine Sulfate 15mg",
            DrugSchedule = DEADrugSchedule.ScheduleII,
            QuantityDispensed = 2.0m,
            UnitOfMeasure = "tablets",
            DispensedByName = "Jane Pharmacist",
            DispenseDateTime = DateTime.UtcNow,
            RunningBalance = 48.0m,
            DrugId = "DRUG-001"
        };
        await log.UpsertRecordAsync(entry);

        List<CSDispenseSummaryEntry> all = await log.GetAllRecordsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].PatientName, Is.EqualTo("John Doe"));
    }

    // ── 11 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task DispenseLog_GetByDrug_FiltersCorrectly()
    {
        string locationId = $"LOC-{Guid.NewGuid():N}";
        ICSDispenseLogGrain log = GetDispenseLog(locationId);

        await log.UpsertRecordAsync(new CSDispenseSummaryEntry
        {
            RecordId = Guid.NewGuid().ToString("N"), LocationId = locationId,
            PatientName = "Patient A", DrugName = "Morphine", DrugId = "DRUG-001",
            DrugSchedule = DEADrugSchedule.ScheduleII, QuantityDispensed = 2,
            UnitOfMeasure = "tablets", DispensedByName = "RPH-A", DispenseDateTime = DateTime.UtcNow
        });
        await log.UpsertRecordAsync(new CSDispenseSummaryEntry
        {
            RecordId = Guid.NewGuid().ToString("N"), LocationId = locationId,
            PatientName = "Patient B", DrugName = "Tramadol", DrugId = "DRUG-002",
            DrugSchedule = DEADrugSchedule.ScheduleIV, QuantityDispensed = 4,
            UnitOfMeasure = "tablets", DispensedByName = "RPH-B", DispenseDateTime = DateTime.UtcNow
        });

        List<CSDispenseSummaryEntry> morphine = await log.GetRecordsByDrugAsync("DRUG-001");
        Assert.That(morphine, Has.Count.EqualTo(1));
        Assert.That(morphine[0].DrugName, Is.EqualTo("Morphine"));
    }

    // ── 12 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task DispenseLog_GetBySchedule_FiltersCorrectly()
    {
        string locationId = $"LOC-{Guid.NewGuid():N}";
        ICSDispenseLogGrain log = GetDispenseLog(locationId);

        await log.UpsertRecordAsync(new CSDispenseSummaryEntry
        {
            RecordId = Guid.NewGuid().ToString("N"), LocationId = locationId,
            PatientName = "Patient A", DrugName = "Morphine", DrugId = "DRUG-001",
            DrugSchedule = DEADrugSchedule.ScheduleII, QuantityDispensed = 2,
            UnitOfMeasure = "tablets", DispensedByName = "RPH-A", DispenseDateTime = DateTime.UtcNow
        });
        await log.UpsertRecordAsync(new CSDispenseSummaryEntry
        {
            RecordId = Guid.NewGuid().ToString("N"), LocationId = locationId,
            PatientName = "Patient B", DrugName = "Tramadol", DrugId = "DRUG-002",
            DrugSchedule = DEADrugSchedule.ScheduleIV, QuantityDispensed = 4,
            UnitOfMeasure = "tablets", DispensedByName = "RPH-B", DispenseDateTime = DateTime.UtcNow
        });

        List<CSDispenseSummaryEntry> scheduleII = await log.GetRecordsByScheduleAsync(DEADrugSchedule.ScheduleII);
        Assert.That(scheduleII, Has.Count.EqualTo(1));
        Assert.That(scheduleII[0].PatientName, Is.EqualTo("Patient A"));
    }

    // ── 13 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task EndToEnd_InspectionCreatedFinalizedAndLoggedForLocation()
    {
        string inspId = Guid.NewGuid().ToString("N");
        string locationId = $"LOC-{Guid.NewGuid():N}";

        // Create and finalize an inspection
        ICSInspectionGrain grain = GetInspectionGrain(inspId);
        await grain.CreateInspectionAsync(
            locationId, "Main Vault",
            CSInspectionType.Unscheduled, DateTime.UtcNow,
            "INS-002", "Sam Inspector",
            "WIT-002", "Alice Witness",
            null, null, null);

        await grain.AddDrugCountAsync(new CSInspectionCount
        {
            DrugName = "Fentanyl Patch", DrugSchedule = DEADrugSchedule.ScheduleII,
            SystemCount = 20, PhysicalCount = 20, Discrepancy = 0, CountUnit = "patches"
        });

        await grain.FinalizeInspectionAsync(
            CSInspectionResult.Passed, false, null, null, null);

        // Upsert to location log
        ICSInspectionLogGrain log = GetInspectionLog(locationId);
        CSInspectionState state = await grain.GetInspectionAsync();
        await log.UpsertInspectionAsync(new CSInspectionSummaryEntry
        {
            InspectionId = inspId,
            LocationId = locationId,
            InspectionType = state.InspectionType,
            InspectionDateTime = state.InspectionDateTime,
            InspectorName = state.InspectorName,
            OverallResult = state.OverallResult,
            TotalDiscrepancies = state.TotalDiscrepancies,
            CreatedDate = state.CreatedDate
        });

        // Verify log contains the inspection
        List<CSInspectionSummaryEntry> all = await log.GetAllInspectionsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].InspectionId, Is.EqualTo(inspId));
        Assert.That(all[0].OverallResult, Is.EqualTo(CSInspectionResult.Passed));
    }

    // ── 14 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task EndToEnd_DispenseCreatedAndLoggedForLocation()
    {
        string recordId = Guid.NewGuid().ToString("N");
        string locationId = $"LOC-{Guid.NewGuid():N}";

        // Create dispense record
        ICSDispenseRecordGrain grain = GetDispenseGrain(recordId);
        await grain.CreateRecordAsync(
            locationId, "Satellite Pharmacy",
            "PAT-100", "Jane Patient", new DateTime(1975, 5, 20),
            "DRUG-010", "Hydrocodone/APAP 5/325",
            DEADrugSchedule.ScheduleII, null,
            30.0m, "tablets", 470.0m,
            CSDispenseType.Routine,
            "PRV-010", "Dr. Adams", "CD9876543",
            "RPH-010", "Pharmacist Lee",
            "WIT-010", "Witness Kim",
            DateTime.UtcNow, "RX-100", null, null);

        CSDispenseRecordState state = await grain.GetRecordAsync();

        // Upsert to location log
        ICSDispenseLogGrain log = GetDispenseLog(locationId);
        await log.UpsertRecordAsync(new CSDispenseSummaryEntry
        {
            RecordId = recordId,
            LocationId = locationId,
            PatientName = state.PatientName,
            DrugName = state.DrugName,
            DrugSchedule = state.DEASchedule,
            QuantityDispensed = state.QuantityDispensed,
            UnitOfMeasure = state.UnitOfMeasure,
            DispensedByName = state.DispensedByName,
            DispenseDateTime = state.DispenseDateTime,
            RunningBalance = state.RunningBalance,
            DrugId = state.DrugId
        });

        List<CSDispenseSummaryEntry> all = await log.GetAllRecordsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].RecordId, Is.EqualTo(recordId));
        Assert.That(all[0].DrugName, Is.EqualTo("Hydrocodone/APAP 5/325"));
    }
}
