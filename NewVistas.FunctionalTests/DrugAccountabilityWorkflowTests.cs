// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

[TestFixture]
public class DrugAccountabilityWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IDrugAccountabilityGrain GetDaGrain(string locationId, string drugId) =>
        _cluster.GrainFactory.GetGrain<IDrugAccountabilityGrain>($"DA:{locationId}:{drugId}");

    private IDrugAccountabilityLocationGrain GetLocGrain(string locationId) =>
        _cluster.GrainFactory.GetGrain<IDrugAccountabilityLocationGrain>($"DA-LOC:{locationId}");

    // ── 1 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task DrugAccountabilityGrain_Initialise_SetsBalanceToZero()
    {
        string locId = $"LOC-{Guid.NewGuid()}";
        string drugId = $"DRUG-{Guid.NewGuid()}";
        IDrugAccountabilityGrain grain = GetDaGrain(locId, drugId);

        await grain.InitialiseAsync(locId, "Test Location", drugId, "Test Drug", null, false, false, "TABLET", 50);

        decimal balance = await grain.GetBalanceAsync();
        Assert.That(balance, Is.EqualTo(0));
    }

    // ── 2 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task DrugAccountabilityGrain_ReceiveStock_IncreasesBalance()
    {
        string locId = $"LOC-{Guid.NewGuid()}";
        string drugId = $"DRUG-{Guid.NewGuid()}";
        IDrugAccountabilityGrain grain = GetDaGrain(locId, drugId);
        await grain.InitialiseAsync(locId, "Test Location", drugId, "Test Drug", null, false, false, "TABLET", 50);

        await grain.ReceiveStockAsync(200, "LOT-001", null, "USR-1", "Pharmacist", null);

        decimal balance = await grain.GetBalanceAsync();
        Assert.That(balance, Is.EqualTo(200));
    }

    // ── 3 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task DrugAccountabilityGrain_DispenseToPatient_DecreasesBalance()
    {
        string locId = $"LOC-{Guid.NewGuid()}";
        string drugId = $"DRUG-{Guid.NewGuid()}";
        IDrugAccountabilityGrain grain = GetDaGrain(locId, drugId);
        await grain.InitialiseAsync(locId, "Test Location", drugId, "Test Drug", null, false, false, "TABLET", 50);
        await grain.ReceiveStockAsync(100, null, null, null, null, null);

        await grain.DispenseToPatientAsync(30, "P-001", "RX-001", "USR-1", "Pharmacist");

        Assert.That(await grain.GetBalanceAsync(), Is.EqualTo(70));
    }

    // ── 4 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task DrugAccountabilityGrain_RecordReturn_IncreasesBalance()
    {
        string locId = $"LOC-{Guid.NewGuid()}";
        string drugId = $"DRUG-{Guid.NewGuid()}";
        IDrugAccountabilityGrain grain = GetDaGrain(locId, drugId);
        await grain.InitialiseAsync(locId, "Test Location", drugId, "Test Drug", null, false, false, "TABLET", 50);
        await grain.ReceiveStockAsync(100, null, null, null, null, null);
        await grain.DispenseToPatientAsync(30, "P-001", "RX-001", null, null);

        await grain.RecordReturnAsync(10, "P-001", "RX-001", "USR-1", "Pharmacist", "Partial return");

        Assert.That(await grain.GetBalanceAsync(), Is.EqualTo(80));
    }

    // ── 5 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task DrugAccountabilityGrain_RecordWaste_DecreasesBalance_RecordsWitness()
    {
        string locId = $"LOC-{Guid.NewGuid()}";
        string drugId = $"DRUG-{Guid.NewGuid()}";
        IDrugAccountabilityGrain grain = GetDaGrain(locId, drugId);
        await grain.InitialiseAsync(locId, "Test Location", drugId, "Morphine", null, true, true, "TABLET", 20);
        await grain.ReceiveStockAsync(50, "LOT-M01", null, null, null, null);

        await grain.RecordWasteAsync(5, "NURSE-1", "Alice Jones RN", "PHARM-1", "Jane Smith RPh", "Broken tablets");

        decimal balance = await grain.GetBalanceAsync();
        List<DrugAccountabilityTransaction> txns = await grain.GetTransactionHistoryAsync();
        DrugAccountabilityTransaction waste = txns.Last();

        Assert.That(balance, Is.EqualTo(45));
        Assert.That(waste.TransactionType, Is.EqualTo("WASTE"));
        Assert.That(waste.WitnessName, Is.EqualTo("Alice Jones RN"));
        Assert.That(waste.Quantity, Is.EqualTo(-5));
    }

    // ── 6 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task DrugAccountabilityGrain_TransferTo_DecreasesBalance()
    {
        string locId = $"LOC-{Guid.NewGuid()}";
        string drugId = $"DRUG-{Guid.NewGuid()}";
        IDrugAccountabilityGrain grain = GetDaGrain(locId, drugId);
        await grain.InitialiseAsync(locId, "Test Location", drugId, "Test Drug", null, false, false, "TABLET", 50);
        await grain.ReceiveStockAsync(100, null, null, null, null, null);

        await grain.TransferToAsync(25, "WARD-4W", "USR-1", "Pharmacist", "Par replenishment");

        Assert.That(await grain.GetBalanceAsync(), Is.EqualTo(75));
    }

    // ── 7 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task DrugAccountabilityGrain_RecordInventoryCount_SetsBalanceToCountedQty()
    {
        string locId = $"LOC-{Guid.NewGuid()}";
        string drugId = $"DRUG-{Guid.NewGuid()}";
        IDrugAccountabilityGrain grain = GetDaGrain(locId, drugId);
        await grain.InitialiseAsync(locId, "Test Location", drugId, "Test Drug", null, false, false, "TABLET", 50);
        await grain.ReceiveStockAsync(100, null, null, null, null, null);
        await grain.DispenseToPatientAsync(10, null, null, null, null);

        // System says 90; count reveals 88 (shortage of 2)
        await grain.RecordInventoryCountAsync(88, "USR-1", "Pharmacist", "Quarterly count");

        Assert.That(await grain.GetBalanceAsync(), Is.EqualTo(88));
    }

    // ── 8 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task DrugAccountabilityGrain_RecordInventoryCount_CreatesAdjustmentTransaction()
    {
        string locId = $"LOC-{Guid.NewGuid()}";
        string drugId = $"DRUG-{Guid.NewGuid()}";
        IDrugAccountabilityGrain grain = GetDaGrain(locId, drugId);
        await grain.InitialiseAsync(locId, "Test Location", drugId, "Test Drug", null, false, false, "TABLET", 50);
        await grain.ReceiveStockAsync(100, null, null, null, null, null);

        await grain.RecordInventoryCountAsync(103, "USR-1", "Pharmacist", "Overage found");

        List<DrugAccountabilityTransaction> txns = await grain.GetTransactionHistoryAsync();
        DrugAccountabilityTransaction adj = txns.Last();
        Assert.That(adj.TransactionType, Is.EqualTo("INVENTORY_COUNT"));
        Assert.That(adj.Quantity, Is.EqualTo(3)); // overage = +3
    }

    // ── 9 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task DrugAccountabilityGrain_GetTransactionHistory_ReturnsAll()
    {
        string locId = $"LOC-{Guid.NewGuid()}";
        string drugId = $"DRUG-{Guid.NewGuid()}";
        IDrugAccountabilityGrain grain = GetDaGrain(locId, drugId);
        await grain.InitialiseAsync(locId, "Test Location", drugId, "Test Drug", null, false, false, "TABLET", 50);
        await grain.ReceiveStockAsync(100, null, null, null, null, null);
        await grain.DispenseToPatientAsync(10, null, null, null, null);
        await grain.RecordReturnAsync(5, null, null, null, null, null);

        List<DrugAccountabilityTransaction> txns = await grain.GetTransactionHistoryAsync();

        Assert.That(txns, Has.Count.EqualTo(3));
    }

    // ── 10 ───────────────────────────────────────────────────────────────────
    [Test]
    public async Task DrugAccountabilityGrain_MultipleTransactions_BalanceTracksCorrectly()
    {
        string locId = $"LOC-{Guid.NewGuid()}";
        string drugId = $"DRUG-{Guid.NewGuid()}";
        IDrugAccountabilityGrain grain = GetDaGrain(locId, drugId);
        await grain.InitialiseAsync(locId, "Test Location", drugId, "Test Drug", null, false, false, "TABLET", 50);

        await grain.ReceiveStockAsync(200, null, null, null, null, null);  // +200 → 200
        await grain.DispenseToPatientAsync(40, null, null, null, null);    // -40  → 160
        await grain.RecordWasteAsync(5, null, null, null, null, null);      // -5   → 155
        await grain.RecordReturnAsync(10, null, null, null, null, null);   // +10  → 165
        await grain.TransferToAsync(15, "OTHER", null, null, null);        // -15  → 150

        Assert.That(await grain.GetBalanceAsync(), Is.EqualTo(150));
    }

    // ── 11 ───────────────────────────────────────────────────────────────────
    [Test]
    public async Task LocationGrain_AddDrug_AppearsInGetAll()
    {
        string locId = $"LOC-{Guid.NewGuid()}";
        IDrugAccountabilityLocationGrain locGrain = GetLocGrain(locId);
        await locGrain.InitialiseLocationAsync(locId, "Test Location", "DISPENSING");

        await locGrain.AddOrUpdateDrugAsync(new DrugBalanceSummary
        {
            DrugId = "D-001", DrugName = "Aspirin 81mg", CurrentBalance = 200,
            UnitOfMeasure = "TABLET", IsControlled = false
        });

        List<DrugBalanceSummary> drugs = await locGrain.GetAllDrugsAsync();
        Assert.That(drugs, Has.Count.EqualTo(1));
        Assert.That(drugs[0].DrugId, Is.EqualTo("D-001"));
    }

    // ── 12 ───────────────────────────────────────────────────────────────────
    [Test]
    public async Task LocationGrain_GetLowStock_FiltersCorrectly()
    {
        string locId = $"LOC-{Guid.NewGuid()}";
        IDrugAccountabilityLocationGrain locGrain = GetLocGrain(locId);
        await locGrain.InitialiseLocationAsync(locId, "Test Location", "DISPENSING");

        await locGrain.AddOrUpdateDrugAsync(new DrugBalanceSummary
        {
            DrugId = "D-LOW", DrugName = "Low Drug", CurrentBalance = 10, ReorderPoint = 50,
            UnitOfMeasure = "TABLET", IsControlled = false
        });
        await locGrain.AddOrUpdateDrugAsync(new DrugBalanceSummary
        {
            DrugId = "D-OK", DrugName = "OK Drug", CurrentBalance = 200, ReorderPoint = 50,
            UnitOfMeasure = "TABLET", IsControlled = false
        });

        List<DrugBalanceSummary> lowStock = await locGrain.GetLowStockDrugsAsync();
        Assert.That(lowStock, Has.Count.EqualTo(1));
        Assert.That(lowStock[0].DrugId, Is.EqualTo("D-LOW"));
    }

    // ── 13 ───────────────────────────────────────────────────────────────────
    [Test]
    public async Task LocationGrain_GetControlled_FiltersControlledSubstances()
    {
        string locId = $"LOC-{Guid.NewGuid()}";
        IDrugAccountabilityLocationGrain locGrain = GetLocGrain(locId);
        await locGrain.InitialiseLocationAsync(locId, "Test Location", "VAULT");

        await locGrain.AddOrUpdateDrugAsync(new DrugBalanceSummary
        {
            DrugId = "D-CTRL", DrugName = "Morphine", CurrentBalance = 100,
            UnitOfMeasure = "TABLET", IsControlled = true
        });
        await locGrain.AddOrUpdateDrugAsync(new DrugBalanceSummary
        {
            DrugId = "D-NORM", DrugName = "Aspirin", CurrentBalance = 500,
            UnitOfMeasure = "TABLET", IsControlled = false
        });

        List<DrugBalanceSummary> controlled = await locGrain.GetControlledSubstancesAsync();
        Assert.That(controlled, Has.Count.EqualTo(1));
        Assert.That(controlled[0].DrugId, Is.EqualTo("D-CTRL"));
    }

    // ── 14 ───────────────────────────────────────────────────────────────────
    [Test]
    public async Task LocationGrain_AddOrUpdate_ReplacesExistingDrug()
    {
        string locId = $"LOC-{Guid.NewGuid()}";
        IDrugAccountabilityLocationGrain locGrain = GetLocGrain(locId);
        await locGrain.InitialiseLocationAsync(locId, "Test Location", "DISPENSING");

        await locGrain.AddOrUpdateDrugAsync(new DrugBalanceSummary
        {
            DrugId = "D-001", DrugName = "Aspirin", CurrentBalance = 100, UnitOfMeasure = "TABLET"
        });
        await locGrain.AddOrUpdateDrugAsync(new DrugBalanceSummary
        {
            DrugId = "D-001", DrugName = "Aspirin", CurrentBalance = 75, UnitOfMeasure = "TABLET"
        });

        List<DrugBalanceSummary> drugs = await locGrain.GetAllDrugsAsync();
        Assert.That(drugs, Has.Count.EqualTo(1));
        Assert.That(drugs[0].CurrentBalance, Is.EqualTo(75));
    }

    // ── 15 ───────────────────────────────────────────────────────────────────
    [Test]
    public async Task Workflow_ReceiveDispenseWaste_BalanceCorrect()
    {
        string locId = $"LOC-{Guid.NewGuid()}";
        string drugId = $"DRUG-{Guid.NewGuid()}";
        IDrugAccountabilityGrain grain = GetDaGrain(locId, drugId);
        IDrugAccountabilityLocationGrain locGrain = GetLocGrain(locId);

        await grain.InitialiseAsync(locId, "Vault", drugId, "Oxycodone 5mg", "00603-4992-21", true, true, "TABLET", 100);
        await locGrain.InitialiseLocationAsync(locId, "Vault", "VAULT");

        await grain.ReceiveStockAsync(500, "LOT-OXY-1", null, "PHARM-1", "Jane RPh", "Initial");
        await grain.DispenseToPatientAsync(60, "P-TEST", "RX-TEST", "PHARM-1", "Jane RPh");
        await grain.RecordWasteAsync(2, "NURSE-W", "Witness RN", "PHARM-1", "Jane RPh", "Wasted");

        DrugAccountabilityState state = await grain.GetStateAsync();
        await locGrain.AddOrUpdateDrugAsync(new DrugBalanceSummary
        {
            DrugId = drugId, DrugName = state.DrugName, CurrentBalance = state.CurrentBalance,
            UnitOfMeasure = state.UnitOfMeasure, IsControlled = state.IsControlled,
            ReorderPoint = state.ReorderPoint
        });

        decimal balance = await grain.GetBalanceAsync();
        List<DrugBalanceSummary> locDrugs = await locGrain.GetAllDrugsAsync();

        Assert.That(balance, Is.EqualTo(438));
        Assert.That(locDrugs[0].CurrentBalance, Is.EqualTo(438));
        Assert.That(locDrugs[0].IsControlled, Is.True);
    }

    // ── 16 ───────────────────────────────────────────────────────────────────
    [Test]
    public async Task Workflow_DispenseThenReturn_BalanceRestored()
    {
        string locId = $"LOC-{Guid.NewGuid()}";
        string drugId = $"DRUG-{Guid.NewGuid()}";
        IDrugAccountabilityGrain grain = GetDaGrain(locId, drugId);
        await grain.InitialiseAsync(locId, "Dispensing", drugId, "Metformin 500mg", null, false, false, "TABLET", 200);

        await grain.ReceiveStockAsync(300, null, null, null, null, null);
        await grain.DispenseToPatientAsync(90, "P-002", "RX-002", null, null);
        await grain.RecordReturnAsync(90, "P-002", "RX-002", null, null, "Dose change");

        Assert.That(await grain.GetBalanceAsync(), Is.EqualTo(300));

        List<DrugAccountabilityTransaction> txns = await grain.GetTransactionHistoryAsync();
        Assert.That(txns.Any(t => t.TransactionType == "RETURN"), Is.True);
    }
}
