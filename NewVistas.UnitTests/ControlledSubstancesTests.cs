// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.Extensions.DependencyInjection;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Grains;
using Orleans.TestingHost;

namespace NewVistas.UnitTests;

// ── CSInspectionGrain Tests ───────────────────────────────────────────────────

[TestFixture]
public class CSInspectionGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private ICSInspectionGrain GetGrain() =>
        _cluster.GrainFactory.GetGrain<ICSInspectionGrain>($"CS-INSPECTION:{Guid.NewGuid()}");

    private static Task CreateDefaultInspection(ICSInspectionGrain grain) =>
        grain.CreateInspectionAsync(
            "VAULT-1A", "Pharmacy Vault 1A",
            CSInspectionType.Scheduled, DateTime.UtcNow,
            "INS-001", "Jane Inspector",
            "WIT-001", "Bob Witness",
            null, null, null);

    [Test]
    public async Task CanCreateInspection()
    {
        ICSInspectionGrain grain = GetGrain();
        await CreateDefaultInspection(grain);

        CSInspectionState state = await grain.GetInspectionAsync();
        Assert.That(state.LocationId, Is.EqualTo("VAULT-1A"));
        Assert.That(state.LocationName, Is.EqualTo("Pharmacy Vault 1A"));
        Assert.That(state.InspectionType, Is.EqualTo(CSInspectionType.Scheduled));
        Assert.That(state.InspectorId, Is.EqualTo("INS-001"));
        Assert.That(state.InspectorName, Is.EqualTo("Jane Inspector"));
        Assert.That(state.WitnessId, Is.EqualTo("WIT-001"));
        Assert.That(state.WitnessName, Is.EqualTo("Bob Witness"));
    }

    [Test]
    public async Task InspectionIdMatchesGrainKey()
    {
        string key = $"CS-INSPECTION:{Guid.NewGuid()}";
        ICSInspectionGrain grain = _cluster.GrainFactory.GetGrain<ICSInspectionGrain>(key);
        await CreateDefaultInspection(grain);

        CSInspectionState state = await grain.GetInspectionAsync();
        Assert.That(state.InspectionId, Is.EqualTo(key));
    }

    [Test]
    public async Task DefaultResultIsPassedAfterCreate()
    {
        ICSInspectionGrain grain = GetGrain();
        await CreateDefaultInspection(grain);

        CSInspectionState state = await grain.GetInspectionAsync();
        Assert.That(state.OverallResult, Is.EqualTo(CSInspectionResult.Passed));
        Assert.That(state.TotalDiscrepancies, Is.EqualTo(0));
    }

    [Test]
    public async Task CanAddSingleDrugCount()
    {
        ICSInspectionGrain grain = GetGrain();
        await CreateDefaultInspection(grain);

        await grain.AddDrugCountAsync(new CSInspectionCount
        {
            DrugName = "Morphine Sulfate",
            DrugSchedule = DEADrugSchedule.ScheduleII,
            SystemCount = 10,
            PhysicalCount = 10,
            CountUnit = "tablets",
        });

        CSInspectionState state = await grain.GetInspectionAsync();
        Assert.That(state.DrugCounts, Has.Count.EqualTo(1));
        Assert.That(state.DrugCounts[0].DrugName, Is.EqualTo("Morphine Sulfate"));
        Assert.That(state.DrugCounts[0].DrugSchedule, Is.EqualTo(DEADrugSchedule.ScheduleII));
    }

    [Test]
    public async Task CanAddMultipleDrugCounts()
    {
        ICSInspectionGrain grain = GetGrain();
        await CreateDefaultInspection(grain);

        await grain.AddDrugCountAsync(new CSInspectionCount { DrugName = "Morphine", DrugSchedule = DEADrugSchedule.ScheduleII, SystemCount = 10, PhysicalCount = 10, CountUnit = "tablets" });
        await grain.AddDrugCountAsync(new CSInspectionCount { DrugName = "Oxycodone", DrugSchedule = DEADrugSchedule.ScheduleII, SystemCount = 5, PhysicalCount = 5, CountUnit = "tablets" });
        await grain.AddDrugCountAsync(new CSInspectionCount { DrugName = "Lorazepam", DrugSchedule = DEADrugSchedule.ScheduleIV, SystemCount = 20, PhysicalCount = 20, CountUnit = "tablets" });

        CSInspectionState state = await grain.GetInspectionAsync();
        Assert.That(state.DrugCounts, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task DiscrepancyCalculatedAsPhysicalMinusSystem()
    {
        ICSInspectionGrain grain = GetGrain();
        await CreateDefaultInspection(grain);

        // Shortage: physical (8) < system (10) → discrepancy = -2
        await grain.AddDrugCountAsync(new CSInspectionCount
        {
            DrugName = "Morphine",
            DrugSchedule = DEADrugSchedule.ScheduleII,
            SystemCount = 10,
            PhysicalCount = 8,
            CountUnit = "tablets",
        });

        CSInspectionState state = await grain.GetInspectionAsync();
        Assert.That(state.DrugCounts[0].Discrepancy, Is.EqualTo(-2m));
    }

    [Test]
    public async Task CanFinalizeAsPassed()
    {
        ICSInspectionGrain grain = GetGrain();
        await CreateDefaultInspection(grain);
        await grain.AddDrugCountAsync(new CSInspectionCount { DrugName = "Morphine", DrugSchedule = DEADrugSchedule.ScheduleII, SystemCount = 10, PhysicalCount = 10, CountUnit = "tablets" });

        await grain.FinalizeInspectionAsync(CSInspectionResult.Passed, false, null, null, null);

        CSInspectionState state = await grain.GetInspectionAsync();
        Assert.That(state.OverallResult, Is.EqualTo(CSInspectionResult.Passed));
        Assert.That(state.TotalDiscrepancies, Is.EqualTo(0));
    }

    [Test]
    public async Task CanFinalizeAsFailed()
    {
        ICSInspectionGrain grain = GetGrain();
        await CreateDefaultInspection(grain);
        await grain.AddDrugCountAsync(new CSInspectionCount { DrugName = "Morphine", DrugSchedule = DEADrugSchedule.ScheduleII, SystemCount = 10, PhysicalCount = 8, CountUnit = "tablets" });

        await grain.FinalizeInspectionAsync(CSInspectionResult.Failed, true, "SUP-001", "Chief Pharmacist", "Investigating shortage");

        CSInspectionState state = await grain.GetInspectionAsync();
        Assert.That(state.OverallResult, Is.EqualTo(CSInspectionResult.Failed));
        Assert.That(state.TotalDiscrepancies, Is.EqualTo(1));
        Assert.That(state.DiscrepanciesReported, Is.True);
        Assert.That(state.ReportedToName, Is.EqualTo("Chief Pharmacist"));
    }

    [Test]
    public async Task CanFinalizeWithDiscrepancy()
    {
        ICSInspectionGrain grain = GetGrain();
        await CreateDefaultInspection(grain);
        await grain.AddDrugCountAsync(new CSInspectionCount { DrugName = "Oxycodone", DrugSchedule = DEADrugSchedule.ScheduleII, SystemCount = 5, PhysicalCount = 7, CountUnit = "tablets" });

        await grain.FinalizeInspectionAsync(CSInspectionResult.DiscrepancyIdentified, false, null, null, null);

        CSInspectionState state = await grain.GetInspectionAsync();
        Assert.That(state.OverallResult, Is.EqualTo(CSInspectionResult.DiscrepancyIdentified));
        Assert.That(state.DrugCounts[0].Discrepancy, Is.EqualTo(2m)); // overage
        Assert.That(state.TotalDiscrepancies, Is.EqualTo(1));
    }

    [Test]
    public async Task TotalDiscrepanciesCountsOnlyNonZero()
    {
        ICSInspectionGrain grain = GetGrain();
        await CreateDefaultInspection(grain);

        // Add 3 drugs: 2 match, 1 doesn't
        await grain.AddDrugCountAsync(new CSInspectionCount { DrugName = "Drug A", DrugSchedule = DEADrugSchedule.ScheduleII, SystemCount = 10, PhysicalCount = 10, CountUnit = "tabs" });
        await grain.AddDrugCountAsync(new CSInspectionCount { DrugName = "Drug B", DrugSchedule = DEADrugSchedule.ScheduleIII, SystemCount = 5, PhysicalCount = 4, CountUnit = "tabs" });
        await grain.AddDrugCountAsync(new CSInspectionCount { DrugName = "Drug C", DrugSchedule = DEADrugSchedule.ScheduleIV, SystemCount = 20, PhysicalCount = 20, CountUnit = "tabs" });

        await grain.FinalizeInspectionAsync(CSInspectionResult.Failed, false, null, null, null);

        CSInspectionState state = await grain.GetInspectionAsync();
        Assert.That(state.TotalDiscrepancies, Is.EqualTo(1)); // Only Drug B has discrepancy
    }

    [Test]
    public async Task DiscrepanciesReportedFlagSet()
    {
        ICSInspectionGrain grain = GetGrain();
        await CreateDefaultInspection(grain);
        await grain.AddDrugCountAsync(new CSInspectionCount { DrugName = "Morphine", DrugSchedule = DEADrugSchedule.ScheduleII, SystemCount = 10, PhysicalCount = 9, CountUnit = "tabs" });

        await grain.FinalizeInspectionAsync(CSInspectionResult.Failed, true, "MGR-001", "Pharmacy Manager", "Reviewing");

        CSInspectionState state = await grain.GetInspectionAsync();
        Assert.That(state.DiscrepanciesReported, Is.True);
        Assert.That(state.ReportedToId, Is.EqualTo("MGR-001"));
        Assert.That(state.ReportedToName, Is.EqualTo("Pharmacy Manager"));
        Assert.That(state.ReportedDateTime, Is.Not.Null);
    }

    [Test]
    public async Task LastModifiedDateUpdatesOnFinalize()
    {
        ICSInspectionGrain grain = GetGrain();
        await CreateDefaultInspection(grain);

        DateTime beforeFinalize = (await grain.GetInspectionAsync()).LastModifiedDate;
        await Task.Delay(10);

        await grain.FinalizeInspectionAsync(CSInspectionResult.Passed, false, null, null, null);

        DateTime afterFinalize = (await grain.GetInspectionAsync()).LastModifiedDate;
        Assert.That(afterFinalize, Is.GreaterThan(beforeFinalize));
    }
}

// ── CSInspectionLogGrain Tests ────────────────────────────────────────────────

[TestFixture]
public class CSInspectionLogGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private ICSInspectionLogGrain GetLog() =>
        _cluster.GrainFactory.GetGrain<ICSInspectionLogGrain>($"CS-INSPECT-LOG:LOC-{Guid.NewGuid()}");

    private static CSInspectionSummaryEntry MakeSummary(string id, CSInspectionType type = CSInspectionType.Scheduled, CSInspectionResult result = CSInspectionResult.Passed, DateTime? dt = null) =>
        new()
        {
            InspectionId = id,
            LocationId = "VAULT-TEST",
            InspectionType = type,
            InspectionDateTime = dt ?? DateTime.UtcNow,
            InspectorName = "Jane Inspector",
            OverallResult = result,
            TotalDiscrepancies = result == CSInspectionResult.Passed ? 0 : 1,
            CreatedDate = DateTime.UtcNow,
        };

    [Test]
    public async Task EmptyOnStart()
    {
        ICSInspectionLogGrain log = GetLog();
        List<CSInspectionSummaryEntry> all = await log.GetAllInspectionsAsync();
        Assert.That(all, Is.Empty);
    }

    [Test]
    public async Task CanUpsertAndRetrieve()
    {
        ICSInspectionLogGrain log = GetLog();
        string id = $"CS-INSPECTION:{Guid.NewGuid()}";
        await log.UpsertInspectionAsync(MakeSummary(id));

        List<CSInspectionSummaryEntry> all = await log.GetAllInspectionsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].InspectionId, Is.EqualTo(id));
    }

    [Test]
    public async Task OrderedNewestFirst()
    {
        ICSInspectionLogGrain log = GetLog();
        DateTime older = DateTime.UtcNow.AddDays(-5);
        DateTime newer = DateTime.UtcNow;

        string idOld = $"CS-INSPECTION:{Guid.NewGuid()}";
        string idNew = $"CS-INSPECTION:{Guid.NewGuid()}";
        await log.UpsertInspectionAsync(MakeSummary(idOld, dt: older));
        await log.UpsertInspectionAsync(MakeSummary(idNew, dt: newer));

        List<CSInspectionSummaryEntry> all = await log.GetAllInspectionsAsync();
        Assert.That(all[0].InspectionId, Is.EqualTo(idNew));
        Assert.That(all[1].InspectionId, Is.EqualTo(idOld));
    }

    [Test]
    public async Task UpsertUpdatesExisting()
    {
        ICSInspectionLogGrain log = GetLog();
        string id = $"CS-INSPECTION:{Guid.NewGuid()}";
        await log.UpsertInspectionAsync(MakeSummary(id, result: CSInspectionResult.Passed));

        CSInspectionSummaryEntry updated = MakeSummary(id, result: CSInspectionResult.Failed);
        updated.TotalDiscrepancies = 3;
        await log.UpsertInspectionAsync(updated);

        List<CSInspectionSummaryEntry> all = await log.GetAllInspectionsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].OverallResult, Is.EqualTo(CSInspectionResult.Failed));
        Assert.That(all[0].TotalDiscrepancies, Is.EqualTo(3));
    }

    [Test]
    public async Task GetByTypeFilters()
    {
        ICSInspectionLogGrain log = GetLog();
        await log.UpsertInspectionAsync(MakeSummary($"CS-INSPECTION:{Guid.NewGuid()}", type: CSInspectionType.Scheduled));
        await log.UpsertInspectionAsync(MakeSummary($"CS-INSPECTION:{Guid.NewGuid()}", type: CSInspectionType.Unscheduled));
        await log.UpsertInspectionAsync(MakeSummary($"CS-INSPECTION:{Guid.NewGuid()}", type: CSInspectionType.Unscheduled));

        List<CSInspectionSummaryEntry> unscheduled = await log.GetInspectionsByTypeAsync(CSInspectionType.Unscheduled);
        Assert.That(unscheduled, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task GetFailedReturnsFailedAndDiscrepancy()
    {
        ICSInspectionLogGrain log = GetLog();
        await log.UpsertInspectionAsync(MakeSummary($"CS-INSPECTION:{Guid.NewGuid()}", result: CSInspectionResult.Passed));
        await log.UpsertInspectionAsync(MakeSummary($"CS-INSPECTION:{Guid.NewGuid()}", result: CSInspectionResult.Failed));
        await log.UpsertInspectionAsync(MakeSummary($"CS-INSPECTION:{Guid.NewGuid()}", result: CSInspectionResult.DiscrepancyIdentified));
        await log.UpsertInspectionAsync(MakeSummary($"CS-INSPECTION:{Guid.NewGuid()}", result: CSInspectionResult.PassedWithNotes));

        List<CSInspectionSummaryEntry> failed = await log.GetFailedInspectionsAsync();
        Assert.That(failed, Has.Count.EqualTo(2));
        Assert.That(failed.All(f => f.OverallResult == CSInspectionResult.Failed ||
                                    f.OverallResult == CSInspectionResult.DiscrepancyIdentified), Is.True);
    }

    [Test]
    public async Task RemoveIsIdempotent()
    {
        ICSInspectionLogGrain log = GetLog();
        string id = $"CS-INSPECTION:{Guid.NewGuid()}";
        await log.UpsertInspectionAsync(MakeSummary(id));

        await log.RemoveInspectionAsync(id);
        await log.RemoveInspectionAsync(id); // second remove is no-op

        List<CSInspectionSummaryEntry> all = await log.GetAllInspectionsAsync();
        Assert.That(all, Is.Empty);
    }
}

// ── CSDispenseRecordGrain Tests ────────────────────────────────────────────────

[TestFixture]
public class CSDispenseRecordGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private ICSDispenseRecordGrain GetGrain() =>
        _cluster.GrainFactory.GetGrain<ICSDispenseRecordGrain>($"CS-DISPENSE:{Guid.NewGuid()}");

    private static Task CreateDefaultRecord(ICSDispenseRecordGrain grain, DEADrugSchedule schedule = DEADrugSchedule.ScheduleII, decimal qty = 2m, decimal balance = 48m, string? witnessName = "Bob Witness") =>
        grain.CreateRecordAsync(
            "VAULT-1A", "Pharmacy Vault 1A",
            "PAT-001", "John Doe",
            new DateTime(1980, 5, 15),
            "DRUG-MOR-001", "Morphine Sulfate",
            schedule, "12345-678-90",
            qty, "tablets",
            balance,
            CSDispenseType.Routine,
            "DOC-001", "Dr. Smith", "XS1234567",
            "PHARM-001", "Alice Pharmacist",
            "WIT-001", witnessName,
            DateTime.UtcNow, "RX-12345", null, null);

    [Test]
    public async Task CanCreateRecord()
    {
        ICSDispenseRecordGrain grain = GetGrain();
        await CreateDefaultRecord(grain);

        CSDispenseRecordState state = await grain.GetRecordAsync();
        Assert.That(state.LocationId, Is.EqualTo("VAULT-1A"));
        Assert.That(state.PatientId, Is.EqualTo("PAT-001"));
        Assert.That(state.PatientName, Is.EqualTo("John Doe"));
        Assert.That(state.DrugId, Is.EqualTo("DRUG-MOR-001"));
        Assert.That(state.DrugName, Is.EqualTo("Morphine Sulfate"));
        Assert.That(state.PrescriberName, Is.EqualTo("Dr. Smith"));
        Assert.That(state.PrescriberDEANumber, Is.EqualTo("XS1234567"));
        Assert.That(state.DispensedByName, Is.EqualTo("Alice Pharmacist"));
        Assert.That(state.WitnessName, Is.EqualTo("Bob Witness"));
        Assert.That(state.PrescriptionNumber, Is.EqualTo("RX-12345"));
    }

    [Test]
    public async Task RecordIdMatchesGrainKey()
    {
        string key = $"CS-DISPENSE:{Guid.NewGuid()}";
        ICSDispenseRecordGrain grain = _cluster.GrainFactory.GetGrain<ICSDispenseRecordGrain>(key);
        await CreateDefaultRecord(grain);

        CSDispenseRecordState state = await grain.GetRecordAsync();
        Assert.That(state.RecordId, Is.EqualTo(key));
    }

    [Test]
    public async Task ScheduleIITracked()
    {
        ICSDispenseRecordGrain grain = GetGrain();
        await CreateDefaultRecord(grain, schedule: DEADrugSchedule.ScheduleII);

        CSDispenseRecordState state = await grain.GetRecordAsync();
        Assert.That(state.DEASchedule, Is.EqualTo(DEADrugSchedule.ScheduleII));
    }

    [Test]
    public async Task RunningBalanceStored()
    {
        ICSDispenseRecordGrain grain = GetGrain();
        await CreateDefaultRecord(grain, qty: 4m, balance: 46m);

        CSDispenseRecordState state = await grain.GetRecordAsync();
        Assert.That(state.QuantityDispensed, Is.EqualTo(4m));
        Assert.That(state.RunningBalance, Is.EqualTo(46m));
    }

    [Test]
    public async Task WitnessRequired()
    {
        ICSDispenseRecordGrain grain = GetGrain();
        await CreateDefaultRecord(grain, witnessName: "Carl Witness");

        CSDispenseRecordState state = await grain.GetRecordAsync();
        Assert.That(state.WitnessName, Is.EqualTo("Carl Witness"));
        Assert.That(state.WitnessId, Is.EqualTo("WIT-001"));
    }
}

// ── CSDispenseLogGrain Tests ──────────────────────────────────────────────────

[TestFixture]
public class CSDispenseLogGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private ICSDispenseLogGrain GetLog() =>
        _cluster.GrainFactory.GetGrain<ICSDispenseLogGrain>($"CS-DISPENSE-LOG:LOC-{Guid.NewGuid()}");

    private static CSDispenseSummaryEntry MakeEntry(string id, string drugId = "DRUG-001", DEADrugSchedule schedule = DEADrugSchedule.ScheduleII, DateTime? dt = null, decimal balance = 50m) =>
        new()
        {
            RecordId = id,
            LocationId = "VAULT-TEST",
            PatientName = "John Doe",
            DrugName = "Morphine",
            DrugId = drugId,
            DrugSchedule = schedule,
            QuantityDispensed = 2m,
            UnitOfMeasure = "tablets",
            DispensedByName = "Alice Pharmacist",
            DispenseDateTime = dt ?? DateTime.UtcNow,
            RunningBalance = balance,
        };

    [Test]
    public async Task EmptyOnStart()
    {
        ICSDispenseLogGrain log = GetLog();
        List<CSDispenseSummaryEntry> all = await log.GetAllRecordsAsync();
        Assert.That(all, Is.Empty);
    }

    [Test]
    public async Task CanUpsertAndRetrieve()
    {
        ICSDispenseLogGrain log = GetLog();
        string id = $"CS-DISPENSE:{Guid.NewGuid()}";
        await log.UpsertRecordAsync(MakeEntry(id));

        List<CSDispenseSummaryEntry> all = await log.GetAllRecordsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].RecordId, Is.EqualTo(id));
    }

    [Test]
    public async Task OrderedNewestFirst()
    {
        ICSDispenseLogGrain log = GetLog();
        DateTime older = DateTime.UtcNow.AddHours(-3);
        DateTime newer = DateTime.UtcNow;

        string idOld = $"CS-DISPENSE:{Guid.NewGuid()}";
        string idNew = $"CS-DISPENSE:{Guid.NewGuid()}";
        await log.UpsertRecordAsync(MakeEntry(idOld, dt: older));
        await log.UpsertRecordAsync(MakeEntry(idNew, dt: newer));

        List<CSDispenseSummaryEntry> all = await log.GetAllRecordsAsync();
        Assert.That(all[0].RecordId, Is.EqualTo(idNew));
        Assert.That(all[1].RecordId, Is.EqualTo(idOld));
    }

    [Test]
    public async Task GetByDrugFilters()
    {
        ICSDispenseLogGrain log = GetLog();
        await log.UpsertRecordAsync(MakeEntry($"CS-DISPENSE:{Guid.NewGuid()}", drugId: "DRUG-MOR"));
        await log.UpsertRecordAsync(MakeEntry($"CS-DISPENSE:{Guid.NewGuid()}", drugId: "DRUG-OXY"));
        await log.UpsertRecordAsync(MakeEntry($"CS-DISPENSE:{Guid.NewGuid()}", drugId: "DRUG-MOR"));

        List<CSDispenseSummaryEntry> morphineRecords = await log.GetRecordsByDrugAsync("DRUG-MOR");
        Assert.That(morphineRecords, Has.Count.EqualTo(2));
        Assert.That(morphineRecords.All(r => r.DrugId == "DRUG-MOR"), Is.True);
    }

    [Test]
    public async Task GetByScheduleFilters()
    {
        ICSDispenseLogGrain log = GetLog();
        await log.UpsertRecordAsync(MakeEntry($"CS-DISPENSE:{Guid.NewGuid()}", schedule: DEADrugSchedule.ScheduleII));
        await log.UpsertRecordAsync(MakeEntry($"CS-DISPENSE:{Guid.NewGuid()}", schedule: DEADrugSchedule.ScheduleII));
        await log.UpsertRecordAsync(MakeEntry($"CS-DISPENSE:{Guid.NewGuid()}", schedule: DEADrugSchedule.ScheduleIV));

        List<CSDispenseSummaryEntry> ciiRecords = await log.GetRecordsByScheduleAsync(DEADrugSchedule.ScheduleII);
        Assert.That(ciiRecords, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task GetByDateRange()
    {
        ICSDispenseLogGrain log = GetLog();
        DateTime now = DateTime.UtcNow;
        await log.UpsertRecordAsync(MakeEntry($"CS-DISPENSE:{Guid.NewGuid()}", dt: now.AddDays(-10)));
        await log.UpsertRecordAsync(MakeEntry($"CS-DISPENSE:{Guid.NewGuid()}", dt: now.AddDays(-3)));
        await log.UpsertRecordAsync(MakeEntry($"CS-DISPENSE:{Guid.NewGuid()}", dt: now.AddDays(-1)));
        await log.UpsertRecordAsync(MakeEntry($"CS-DISPENSE:{Guid.NewGuid()}", dt: now.AddDays(1)));

        List<CSDispenseSummaryEntry> inRange = await log.GetRecordsByDateRangeAsync(
            now.AddDays(-5), now);
        Assert.That(inRange, Has.Count.EqualTo(2)); // -3 and -1 days
    }

    [Test]
    public async Task RemoveIdempotent()
    {
        ICSDispenseLogGrain log = GetLog();
        string id = $"CS-DISPENSE:{Guid.NewGuid()}";
        await log.UpsertRecordAsync(MakeEntry(id));

        await log.RemoveRecordAsync(id);
        await log.RemoveRecordAsync(id); // second remove is no-op

        List<CSDispenseSummaryEntry> all = await log.GetAllRecordsAsync();
        Assert.That(all, Is.Empty);
    }
}
