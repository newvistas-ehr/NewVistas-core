// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional / end-to-end tests for the Pharmacy Data Management subsystem.
///
/// Tests exercise complete workflows across:
///   - IDrugGrain / IDrugIndexGrain         (File #50 — facility drug catalog)
///   - IPharmacyOrderableItemGrain          (File #50.7 — CPRS ordering entity)
///   - IOrderableItemIndexGrain             (searchable OI index)
///   - IMedicationRouteIndexGrain           (File #51.23 — self-seeding, 55 routes)
///   - IDoseUnitIndexGrain                  (File #51.24 — self-seeding, 54 units)
///
/// No extra DI singletons are required — there are no silo-level cache services
/// in this subsystem, only persistent grain state and index grains.
/// </summary>
[TestFixture]
public class DrugFileWorkflowTests
{
    private TestCluster _cluster = default!;

    private const string DrugIndexKey = "DRUG-INDEX";
    private const string OiIndexKey = "OI-INDEX";
    private const string MedRouteKey = "MED-ROUTE-INDEX";
    private const string DoseUnitKey = "DOSE-UNIT-INDEX";

    // ─── Test Cluster Configuration ───────────────────────────────────────────

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ─── DRUG INDEX TESTS ─────────────────────────────────────────────────────

    /// <summary>
    /// Test 1: Loading a batch of drugs into the index persists them
    /// and makes them retrievable by IEN.
    /// </summary>
    [Test]
    public async Task DrugIndex_LoadDrugs_PersistsAndSearchable()
    {
        string suffix = Guid.NewGuid().ToString("N")[..8];
        var indexGrain = _cluster.GrainFactory.GetGrain<IDrugIndexGrain>(DrugIndexKey + suffix);

        List<DrugIndexEntry> entries =
        [
            new() { Ien = $"D1-{suffix}", LocalName = $"ASPIRIN TAB 325MG-{suffix}", VaGenericName = "ASPIRIN", PrimaryDrugClassCode = "CN104", PharmacyType = PharmacyType.Outpatient, IsActive = true },
            new() { Ien = $"D2-{suffix}", LocalName = $"METFORMIN HCL TAB 500MG-{suffix}", VaGenericName = "METFORMIN", PrimaryDrugClassCode = "HS502", PharmacyType = PharmacyType.Both, IsActive = true },
        ];

        await indexGrain.LoadDrugsAsync(entries);

        DrugIndexStatus status = await indexGrain.GetStatusAsync();
        Assert.That(status.IsLoaded, Is.True);
        Assert.That(status.TotalDrugs, Is.EqualTo(2));

        DrugIndexEntry? retrieved = await indexGrain.GetDrugAsync($"D1-{suffix}");
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved!.LocalName, Does.Contain(suffix));
    }

    /// <summary>
    /// Test 2: SearchAsync finds drugs by name substring (case-insensitive).
    /// </summary>
    [Test]
    public async Task DrugIndex_Search_ByNameSubstring()
    {
        string suffix = Guid.NewGuid().ToString("N")[..8];
        var indexGrain = _cluster.GrainFactory.GetGrain<IDrugIndexGrain>(DrugIndexKey + suffix);

        await indexGrain.LoadDrugsAsync(
        [
            new() { Ien = $"D1-{suffix}", LocalName = $"LISINOPRIL TAB 10MG-{suffix}",  VaGenericName = "LISINOPRIL",   PharmacyType = PharmacyType.Outpatient, IsActive = true },
            new() { Ien = $"D2-{suffix}", LocalName = $"LISINOPRIL TAB 20MG-{suffix}",  VaGenericName = "LISINOPRIL",   PharmacyType = PharmacyType.Outpatient, IsActive = true },
            new() { Ien = $"D3-{suffix}", LocalName = $"FUROSEMIDE TAB 40MG-{suffix}",  VaGenericName = "FUROSEMIDE",   PharmacyType = PharmacyType.Both,       IsActive = true },
        ]);

        List<DrugIndexEntry> results = await indexGrain.SearchAsync("LISINOPRIL");
        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results.All(r => r.VaGenericName == "LISINOPRIL"), Is.True);
    }

    /// <summary>
    /// Test 3: GetByVaProductAsync returns all drugs linked to a specific VA Product IEN.
    /// </summary>
    [Test]
    public async Task DrugIndex_GetByVaProduct_ReturnsCorrectDrugs()
    {
        string suffix = Guid.NewGuid().ToString("N")[..8];
        string vaProductIen = $"VP-{suffix}";
        var indexGrain = _cluster.GrainFactory.GetGrain<IDrugIndexGrain>(DrugIndexKey + suffix);

        await indexGrain.LoadDrugsAsync(
        [
            new() { Ien = $"D1-{suffix}", LocalName = $"DRUG A-{suffix}", VaProductIen = vaProductIen, PharmacyType = PharmacyType.Outpatient, IsActive = true },
            new() { Ien = $"D2-{suffix}", LocalName = $"DRUG B-{suffix}", VaProductIen = vaProductIen, PharmacyType = PharmacyType.UnitDose,   IsActive = true },
            new() { Ien = $"D3-{suffix}", LocalName = $"DRUG C-{suffix}", VaProductIen = "OTHER",      PharmacyType = PharmacyType.Outpatient, IsActive = true },
        ]);

        List<DrugIndexEntry> results = await indexGrain.GetByVaProductAsync(vaProductIen);
        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results.All(r => r.VaProductIen == vaProductIen), Is.True);
    }

    // ─── DRUG GRAIN TESTS ─────────────────────────────────────────────────────

    /// <summary>
    /// Test 4: DrugGrain saves and retrieves the full drug state.
    /// </summary>
    [Test]
    public async Task DrugGrain_SaveAndRetrieve_FullState()
    {
        string ien = $"DRUG-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IDrugGrain>(ien);

        DrugState drug = new()
        {
            Ien = ien,
            LocalName = "WARFARIN SODIUM TAB 5MG",
            VaGenericIen = "GEN-WAR",
            VaGenericName = "WARFARIN",
            PrimaryDrugClassCode = "BL110",
            PharmacyType = PharmacyType.Outpatient,
            IsActive = true,
            IsNationalFormulary = true,
            MaxDaysSupply = 90,
            DispenseUnit = "TABLET"
        };

        await grain.SaveDrugAsync(drug);
        DrugState retrieved = await grain.GetDrugAsync();

        Assert.That(retrieved.LocalName, Is.EqualTo("WARFARIN SODIUM TAB 5MG"));
        Assert.That(retrieved.VaGenericName, Is.EqualTo("WARFARIN"));
        Assert.That(retrieved.PrimaryDrugClassCode, Is.EqualTo("BL110"));
        Assert.That(retrieved.MaxDaysSupply, Is.EqualTo(90));
        Assert.That(retrieved.IsNationalFormulary, Is.True);
    }

    /// <summary>
    /// Test 5: DeactivateAsync marks the drug inactive and sets the inactivation date.
    /// </summary>
    [Test]
    public async Task DrugGrain_Deactivate_SetsInactiveStatus()
    {
        string ien = $"DRUG-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IDrugGrain>(ien);

        await grain.SaveDrugAsync(new DrugState { Ien = ien, LocalName = "TEST DRUG", PharmacyType = PharmacyType.Outpatient, IsActive = true });

        DateTime inactivationDate = new(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        await grain.DeactivateAsync(inactivationDate);

        DrugState result = await grain.GetDrugAsync();
        Assert.That(result.IsActive, Is.False);
        Assert.That(result.InactivationDate, Is.EqualTo(inactivationDate));
    }

    /// <summary>
    /// Test 6: AddPossibleDosageAsync appends a dosage and deduplicates.
    /// </summary>
    [Test]
    public async Task DrugGrain_AddPossibleDosage_AppendsToList()
    {
        string ien = $"DRUG-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IDrugGrain>(ien);

        await grain.SaveDrugAsync(new DrugState { Ien = ien, LocalName = "ASPIRIN TAB 325MG", PharmacyType = PharmacyType.Outpatient });

        await grain.AddPossibleDosageAsync(new PossibleDosage { Dose = "325MG", Unit = "TABLET", IsDefault = true });
        await grain.AddPossibleDosageAsync(new PossibleDosage { Dose = "650MG", Unit = "TABLET" });
        // Duplicate — should not be added
        await grain.AddPossibleDosageAsync(new PossibleDosage { Dose = "325MG", Unit = "TABLET" });

        DrugState result = await grain.GetDrugAsync();
        Assert.That(result.PossibleDosages, Has.Count.EqualTo(2));
        Assert.That(result.PossibleDosages.Any(d => d.IsDefault), Is.True);
    }

    // ─── ORDERABLE ITEM GRAIN TESTS ───────────────────────────────────────────

    /// <summary>
    /// Test 7: PharmacyOrderableItemGrain saves and retrieves the full state.
    /// </summary>
    [Test]
    public async Task OrderableItem_SaveAndRetrieve_FullState()
    {
        string ien = $"OI-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IPharmacyOrderableItemGrain>(ien);

        OrderableItemState item = new()
        {
            Ien = ien,
            Name = "LISINOPRIL TAB",
            PharmacyType = PharmacyType.Outpatient,
            VaGenericIen = "GEN-LIS",
            VaGenericName = "LISINOPRIL",
            DosageFormName = "TABLET",
            PrimaryDrugClassCode = "CV800",
            IsActive = true,
            IsNationalFormulary = true,
            OutpatientDefaultDaysSupply = 90
        };

        await grain.SaveItemAsync(item);
        OrderableItemState retrieved = await grain.GetItemAsync();

        Assert.That(retrieved.Name, Is.EqualTo("LISINOPRIL TAB"));
        Assert.That(retrieved.VaGenericName, Is.EqualTo("LISINOPRIL"));
        Assert.That(retrieved.DosageFormName, Is.EqualTo("TABLET"));
        Assert.That(retrieved.OutpatientDefaultDaysSupply, Is.EqualTo(90));
        Assert.That(retrieved.IsNationalFormulary, Is.True);
    }

    /// <summary>
    /// Test 8: AddLinkedDrugAsync appends a drug IEN to the orderable item.
    /// </summary>
    [Test]
    public async Task OrderableItem_AddLinkedDrug_AppearsinList()
    {
        string ien = $"OI-{Guid.NewGuid():N}";
        string drugIen = $"DRUG-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IPharmacyOrderableItemGrain>(ien);

        await grain.SaveItemAsync(new OrderableItemState { Ien = ien, Name = "ASPIRIN TAB", PharmacyType = PharmacyType.Outpatient });
        await grain.AddLinkedDrugAsync(drugIen);

        OrderableItemState result = await grain.GetItemAsync();
        Assert.That(result.LinkedDrugIens, Contains.Item(drugIen));
    }

    /// <summary>
    /// Test 9: AddLinkedDrugAsync deduplicates — adding the same drug IEN twice
    /// results in only one entry.
    /// </summary>
    [Test]
    public async Task OrderableItem_LinkDeduplication_NoDuplicateDrugs()
    {
        string ien = $"OI-{Guid.NewGuid():N}";
        string drugIen = $"DRUG-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IPharmacyOrderableItemGrain>(ien);

        await grain.SaveItemAsync(new OrderableItemState { Ien = ien, Name = "METFORMIN TAB", PharmacyType = PharmacyType.Both });
        await grain.AddLinkedDrugAsync(drugIen);
        await grain.AddLinkedDrugAsync(drugIen);  // duplicate

        OrderableItemState result = await grain.GetItemAsync();
        Assert.That(result.LinkedDrugIens, Has.Count.EqualTo(1));
    }

    // ─── ORDERABLE ITEM INDEX TESTS ───────────────────────────────────────────

    /// <summary>
    /// Test 10: SearchAsync on the OI index finds items by name substring.
    /// </summary>
    [Test]
    public async Task OiIndex_Search_ByNameSubstring()
    {
        string suffix = Guid.NewGuid().ToString("N")[..8];
        var indexGrain = _cluster.GrainFactory.GetGrain<IOrderableItemIndexGrain>(OiIndexKey + suffix);

        await indexGrain.LoadItemsAsync(
        [
            new() { Ien = $"OI1-{suffix}", Name = $"ASPIRIN TAB-{suffix}",     VaGenericName = "ASPIRIN",     PharmacyType = PharmacyType.Outpatient, IsActive = true },
            new() { Ien = $"OI2-{suffix}", Name = $"ASPIRIN EC TAB-{suffix}",  VaGenericName = "ASPIRIN",     PharmacyType = PharmacyType.Outpatient, IsActive = true },
            new() { Ien = $"OI3-{suffix}", Name = $"METFORMIN TAB-{suffix}",   VaGenericName = "METFORMIN",   PharmacyType = PharmacyType.Both,       IsActive = true },
        ]);

        List<OrderableItemIndexEntry> results = await indexGrain.SearchAsync("ASPIRIN");
        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results.All(r => r.VaGenericName == "ASPIRIN"), Is.True);
    }

    /// <summary>
    /// Test 11: GetByVaGenericAsync returns all orderable items for a given VA Generic IEN.
    /// </summary>
    [Test]
    public async Task OiIndex_GetByVaGeneric_ReturnsMatchingItems()
    {
        string suffix = Guid.NewGuid().ToString("N")[..8];
        string vaGenericIen = $"GEN-{suffix}";
        var indexGrain = _cluster.GrainFactory.GetGrain<IOrderableItemIndexGrain>(OiIndexKey + suffix);

        await indexGrain.LoadItemsAsync(
        [
            new() { Ien = $"OI1-{suffix}", Name = "ITEM A", VaGenericIen = vaGenericIen, PharmacyType = PharmacyType.Outpatient, IsActive = true },
            new() { Ien = $"OI2-{suffix}", Name = "ITEM B", VaGenericIen = vaGenericIen, PharmacyType = PharmacyType.UnitDose,   IsActive = true },
            new() { Ien = $"OI3-{suffix}", Name = "ITEM C", VaGenericIen = "OTHER",      PharmacyType = PharmacyType.Outpatient, IsActive = true },
        ]);

        List<OrderableItemIndexEntry> results = await indexGrain.GetByVaGenericAsync(vaGenericIen);
        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results.All(r => r.VaGenericIen == vaGenericIen), Is.True);
    }

    // ─── MEDICATION ROUTE INDEX TESTS ─────────────────────────────────────────

    /// <summary>
    /// Test 12: MedicationRouteIndexGrain is auto-seeded on first activation
    /// and returns exactly 55 routes.
    /// </summary>
    [Test]
    public async Task MedicationRouteIndex_IsAutoSeeded_OnActivation()
    {
        var grain = _cluster.GrainFactory.GetGrain<IMedicationRouteIndexGrain>(MedRouteKey);

        bool isLoaded = await grain.IsLoadedAsync();
        Assert.That(isLoaded, Is.True);

        List<MedicationRoute> routes = await grain.GetAllRoutesAsync();
        Assert.That(routes, Has.Count.EqualTo(55));
    }

    /// <summary>
    /// Test 13: GetRouteByNameAsync returns the expected entry for known routes.
    /// </summary>
    [Test]
    public async Task MedicationRouteIndex_GetByName_ReturnsKnownRoute()
    {
        var grain = _cluster.GrainFactory.GetGrain<IMedicationRouteIndexGrain>(MedRouteKey);

        MedicationRoute? oral = await grain.GetRouteByNameAsync("ORAL");
        Assert.That(oral, Is.Not.Null);
        Assert.That(oral!.Ien, Is.EqualTo("22"));
        Assert.That(oral.Vuid, Is.EqualTo("4500642"));

        MedicationRoute? iv = await grain.GetRouteByNameAsync("INTRAVENOUS");
        Assert.That(iv, Is.Not.Null);
        Assert.That(iv!.Ien, Is.EqualTo("17"));

        // Case-insensitive lookup
        MedicationRoute? sc = await grain.GetRouteByNameAsync("subcutaneous");
        Assert.That(sc, Is.Not.Null);
        Assert.That(sc!.Ien, Is.EqualTo("25"));
    }

    // ─── DOSE UNIT INDEX TESTS ────────────────────────────────────────────────

    /// <summary>
    /// Test 14: DoseUnitIndexGrain is auto-seeded on first activation
    /// and returns exactly 54 dose units.
    /// </summary>
    [Test]
    public async Task DoseUnitIndex_IsAutoSeeded_OnActivation()
    {
        var grain = _cluster.GrainFactory.GetGrain<IDoseUnitIndexGrain>(DoseUnitKey);

        bool isLoaded = await grain.IsLoadedAsync();
        Assert.That(isLoaded, Is.True);

        List<DoseUnit> units = await grain.GetAllUnitsAsync();
        Assert.That(units, Has.Count.EqualTo(54));
    }

    /// <summary>
    /// Test 15: GetUnitByNameAsync returns the expected entry for known units.
    /// </summary>
    [Test]
    public async Task DoseUnitIndex_GetByName_ReturnsKnownUnit()
    {
        var grain = _cluster.GrainFactory.GetGrain<IDoseUnitIndexGrain>(DoseUnitKey);

        DoseUnit? mg = await grain.GetUnitByNameAsync("MILLIGRAM(S)");
        Assert.That(mg, Is.Not.Null);
        Assert.That(mg!.Ien, Is.EqualTo("19"));
        Assert.That(mg.Abbreviation, Is.EqualTo("MILLIGRAMS"));
        Assert.That(mg.IsCounting, Is.False);

        DoseUnit? tab = await grain.GetUnitByNameAsync("TABLET(S)");
        Assert.That(tab, Is.Not.Null);
        Assert.That(tab!.Ien, Is.EqualTo("42"));
        Assert.That(tab.IsCounting, Is.True);

        DoseUnit? ml = await grain.GetUnitByNameAsync("MILLILITER(S)");
        Assert.That(ml, Is.Not.Null);
        Assert.That(ml!.Ien, Is.EqualTo("24"));
    }

    // ─── END-TO-END WORKFLOW TEST ─────────────────────────────────────────────

    /// <summary>
    /// Test 16: End-to-end workflow — create a drug, create an orderable item,
    /// link them, verify the link persists, and search both indexes.
    /// Mirrors the real VistA PSS workflow where a pharmacist creates a drug
    /// entry (File #50) and links it to an orderable item (File #50.7) for CPRS.
    /// </summary>
    [Test]
    public async Task Workflow_DrugLinkedToOrderableItem_EndToEnd()
    {
        string suffix = Guid.NewGuid().ToString("N")[..8];
        string drugIen = $"DRUG-WF-{suffix}";
        string oiIen   = $"OI-WF-{suffix}";

        // ── Step 1: Create the facility drug entry ──────────────────────────
        var drugGrain = _cluster.GrainFactory.GetGrain<IDrugGrain>(drugIen);

        DrugState drug = new()
        {
            Ien = drugIen,
            LocalName = $"ATORVASTATIN TAB 40MG-{suffix}",
            VaGenericIen = $"GEN-ATOR-{suffix}",
            VaGenericName = "ATORVASTATIN",
            PrimaryDrugClassCode = "CV350",
            PharmacyType = PharmacyType.Outpatient,
            IsActive = true,
            IsNationalFormulary = true,
            DispenseUnit = "TABLET",
            MaxDaysSupply = 90
        };

        await drugGrain.SaveDrugAsync(drug);
        await drugGrain.AddPossibleDosageAsync(new PossibleDosage { Dose = "40MG", Unit = "TABLET", IsDefault = true });

        // ── Step 2: Create the orderable item ──────────────────────────────
        var oiGrain = _cluster.GrainFactory.GetGrain<IPharmacyOrderableItemGrain>(oiIen);

        OrderableItemState oi = new()
        {
            Ien = oiIen,
            Name = $"ATORVASTATIN TAB-{suffix}",
            PharmacyType = PharmacyType.Outpatient,
            VaGenericIen = $"GEN-ATOR-{suffix}",
            VaGenericName = "ATORVASTATIN",
            DosageFormName = "TABLET",
            PrimaryDrugClassCode = "CV350",
            IsActive = true,
            IsNationalFormulary = true,
            OutpatientDefaultDaysSupply = 90
        };

        await oiGrain.SaveItemAsync(oi);
        await oiGrain.AddDosageAsync(new OrderableDosage { Dose = "40MG", Unit = "TABLET", Route = "ORAL", IsDefault = true });
        await oiGrain.AddDosageAsync(new OrderableDosage { Dose = "80MG", Unit = "TABLET", Route = "ORAL" });

        // ── Step 3: Link the drug to the orderable item ────────────────────
        await oiGrain.AddLinkedDrugAsync(drugIen);

        // ── Step 4: Verify the OI has the drug linked ──────────────────────
        OrderableItemState oiResult = await oiGrain.GetItemAsync();
        Assert.That(oiResult.LinkedDrugIens, Contains.Item(drugIen));
        Assert.That(oiResult.AvailableDosages, Has.Count.EqualTo(2));

        // ── Step 5: Verify drug detail is correct ──────────────────────────
        DrugState drugResult = await drugGrain.GetDrugAsync();
        Assert.That(drugResult.PossibleDosages, Has.Count.EqualTo(1));
        Assert.That(drugResult.PossibleDosages[0].IsDefault, Is.True);

        // ── Step 6: Load both index grains and verify search ───────────────
        string drugIdxKey = DrugIndexKey + suffix;
        string oiIdxKey   = OiIndexKey + suffix;

        var drugIndex = _cluster.GrainFactory.GetGrain<IDrugIndexGrain>(drugIdxKey);
        await drugIndex.LoadDrugsAsync(
        [
            new() { Ien = drugIen, LocalName = $"ATORVASTATIN TAB 40MG-{suffix}", VaGenericIen = $"GEN-ATOR-{suffix}", VaGenericName = "ATORVASTATIN", PrimaryDrugClassCode = "CV350", PharmacyType = PharmacyType.Outpatient, IsActive = true, IsNationalFormulary = true }
        ]);

        var oiIndex = _cluster.GrainFactory.GetGrain<IOrderableItemIndexGrain>(oiIdxKey);
        await oiIndex.LoadItemsAsync(
        [
            new() { Ien = oiIen, Name = $"ATORVASTATIN TAB-{suffix}", VaGenericIen = $"GEN-ATOR-{suffix}", VaGenericName = "ATORVASTATIN", PrimaryDrugClassCode = "CV350", PharmacyType = PharmacyType.Outpatient, IsActive = true, IsNationalFormulary = true }
        ]);

        List<DrugIndexEntry> drugSearchResults = await drugIndex.SearchAsync("ATORVASTATIN");
        Assert.That(drugSearchResults, Has.Count.EqualTo(1));
        Assert.That(drugSearchResults[0].Ien, Is.EqualTo(drugIen));

        List<OrderableItemIndexEntry> oiSearchResults = await oiIndex.GetByVaGenericAsync($"GEN-ATOR-{suffix}");
        Assert.That(oiSearchResults, Has.Count.EqualTo(1));
        Assert.That(oiSearchResults[0].Ien, Is.EqualTo(oiIen));
    }
}
