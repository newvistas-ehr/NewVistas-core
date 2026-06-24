// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

// ─── VaProductGrain Tests ─────────────────────────────────────────────────────

[TestFixture]
public class VaProductGrainTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    [Test]
    public async Task VaProductGrain_CanLoadAndRetrieveProduct()
    {
        IVaProductGrain grain = _cluster.GrainFactory.GetGrain<IVaProductGrain>("TEST-1001");

        await grain.LoadProductAsync(
            name: "ATROPINE SO4 0.4MG/ML INJ",
            vaGenericIen: "1",
            vaGenericName: "ATROPINE",
            dosageFormIen: "63",
            dosageFormName: "INJECTION",
            strength: "0.4",
            strengthUnitIen: "20",
            strengthUnitName: "MG/ML",
            printName: "ATROPINE SO4 0.4MG/ML INJ",
            primaryDrugClassCode: "AU300",
            primaryDrugClassName: "PARASYMPATHOMIMETICS",
            formularyIndicator: true,
            formularyRestrictions: null,
            controlledSubstanceSchedule: null,
            copayTier: null,
            maxSingleDose: 2m,
            minSingleDose: 0.1m,
            maxDailyDose: 8m,
            minDailyDose: null,
            maxCumulativeDose: null,
            doseUnit: "MG",
            isDosageCheckExcluded: false,
            isHazardousWaste: false,
            rxNormCode: "1190692",
            vuid: "4019591",
            isActive: true,
            inactivationDate: null,
            ingredients: [new DrugIngredient { IngredientIen = "101", Name = "ATROPINE SULFATE", Strength = "0.4", StrengthUnit = "MG/ML" }],
            ndcCodes: [new NdcEntry { NdcCode = "0002-1200-01", IsActive = true }],
            secondaryDrugClassCodes: []);

        VaProductState state = await grain.GetProductAsync();
        Assert.That(state.Name, Is.EqualTo("ATROPINE SO4 0.4MG/ML INJ"));
        Assert.That(state.VaGenericName, Is.EqualTo("ATROPINE"));
        Assert.That(state.DosageFormName, Is.EqualTo("INJECTION"));
        Assert.That(state.Strength, Is.EqualTo("0.4"));
        Assert.That(state.StrengthUnitName, Is.EqualTo("MG/ML"));
        Assert.That(state.PrimaryDrugClassCode, Is.EqualTo("AU300"));
        Assert.That(state.FormularyIndicator, Is.True);
        Assert.That(state.RxNormCode, Is.EqualTo("1190692"));
        Assert.That(state.Ingredients, Has.Count.EqualTo(1));
        Assert.That(state.Ingredients[0].Name, Is.EqualTo("ATROPINE SULFATE"));
        Assert.That(state.NdcCodes, Has.Count.EqualTo(1));
        Assert.That(state.NdcCodes[0].NdcCode, Is.EqualTo("0002-1200-01"));
        Assert.That(state.MaxSingleDose, Is.EqualTo(2m));
        Assert.That(state.IsActive, Is.True);
    }

    [Test]
    public async Task VaProductGrain_IsFormulary()
    {
        IVaProductGrain grain = _cluster.GrainFactory.GetGrain<IVaProductGrain>("TEST-1002");

        await LoadMinimalProductAsync(grain, "WARFARIN NA 5MG TAB", formulary: true);

        Assert.That(await grain.IsFormularyAsync(), Is.True);
    }

    [Test]
    public async Task VaProductGrain_NotFormulary()
    {
        IVaProductGrain grain = _cluster.GrainFactory.GetGrain<IVaProductGrain>("TEST-1003");

        await LoadMinimalProductAsync(grain, "AMIODARONE HCL 200MG TAB", formulary: false);

        Assert.That(await grain.IsFormularyAsync(), Is.False);
    }

    [Test]
    public async Task VaProductGrain_CanUpdateFormularyStatus()
    {
        IVaProductGrain grain = _cluster.GrainFactory.GetGrain<IVaProductGrain>("TEST-1004");

        await LoadMinimalProductAsync(grain, "SERTRALINE HCL 50MG TAB", formulary: false);
        Assert.That(await grain.IsFormularyAsync(), Is.False);

        await grain.UpdateFormularyStatusAsync(true, "Requires prior authorization", "Tier 2");

        Assert.That(await grain.IsFormularyAsync(), Is.True);
        VaProductState state = await grain.GetProductAsync();
        Assert.That(state.FormularyRestrictions, Is.EqualTo("Requires prior authorization"));
        Assert.That(state.CopayTier, Is.EqualTo("Tier 2"));
    }

    [Test]
    public async Task VaProductGrain_CanDeactivateAndActivate()
    {
        IVaProductGrain grain = _cluster.GrainFactory.GetGrain<IVaProductGrain>("TEST-1005");

        await LoadMinimalProductAsync(grain, "CEPHALEXIN 250MG CAP", formulary: true);
        Assert.That(await grain.IsActiveAsync(), Is.True);

        DateTime deactivationDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await grain.DeactivateAsync(deactivationDate);

        Assert.That(await grain.IsActiveAsync(), Is.False);
        VaProductState state = await grain.GetProductAsync();
        Assert.That(state.InactivationDate, Is.EqualTo(deactivationDate));

        await grain.ActivateAsync();
        Assert.That(await grain.IsActiveAsync(), Is.True);
    }

    [Test]
    public async Task VaProductGrain_DosageStrengthSummary()
    {
        IVaProductGrain grain = _cluster.GrainFactory.GetGrain<IVaProductGrain>("TEST-1006");

        await grain.LoadProductAsync(
            "METOPROLOL TARTRATE 25MG TAB", "3", "METOPROLOL", string.Empty, "TABLET",
            "25", string.Empty, "MG", "METOPROLOL 25MG TAB", "CV200", "ANTIHYPERTENSIVE AGENTS",
            true, null, null, null, null, null, null, null, null, null,
            false, false, null, null, true, null,
            new List<DrugIngredient>(), new List<NdcEntry>(), new List<string>());

        string summary = await grain.GetDosageStrengthSummaryAsync();
        Assert.That(summary, Is.EqualTo("TABLET 25 MG"));
    }

    [Test]
    public async Task VaProductGrain_CprsFormat()
    {
        IVaProductGrain grain = _cluster.GrainFactory.GetGrain<IVaProductGrain>("TEST-1007");

        await grain.LoadProductAsync(
            "LISINOPRIL 10MG TAB", "4", "LISINOPRIL", string.Empty, "TABLET",
            "10", string.Empty, "MG", "LISINOPRIL 10MG TAB", "CV200", "ANTIHYPERTENSIVE AGENTS",
            true, null, null, null, null, null, null, null, null, null,
            false, false, null, null, true, null,
            new List<DrugIngredient>(), new List<NdcEntry>(), new List<string>());

        string cprs = await grain.GetCprsFormatAsync();
        Assert.That(cprs, Is.EqualTo("TABLET^CV200^10^MG"));
    }

    [Test]
    public async Task VaProductGrain_CanAddNdcEntry()
    {
        IVaProductGrain grain = _cluster.GrainFactory.GetGrain<IVaProductGrain>("TEST-1008");
        await LoadMinimalProductAsync(grain, "GENTAMICIN SO4 40MG/ML INJ", formulary: true);

        await grain.AddNdcEntryAsync(new NdcEntry
        {
            NdcCode = "0002-7232-50",
            TradeName = "GARAMYCIN",
            Manufacturer = "SCHERING",
            IsActive = true
        });

        // Adding same NDC again should not duplicate
        await grain.AddNdcEntryAsync(new NdcEntry { NdcCode = "0002-7232-50" });

        VaProductState state = await grain.GetProductAsync();
        Assert.That(state.NdcCodes, Has.Count.EqualTo(1));
        Assert.That(state.NdcCodes[0].TradeName, Is.EqualTo("GARAMYCIN"));
    }

    private static async Task LoadMinimalProductAsync(IVaProductGrain grain, string name, bool formulary)
    {
        await grain.LoadProductAsync(
            name, string.Empty, string.Empty, string.Empty, "TABLET",
            string.Empty, string.Empty, string.Empty, name, string.Empty, string.Empty,
            formulary, null, null, null,
            null, null, null, null, null, null,
            false, false, null, null, true, null,
            new List<DrugIngredient>(), new List<NdcEntry>(), new List<string>());
    }
}

// ─── VaProductIndexGrain Tests ────────────────────────────────────────────────

[TestFixture]
public class VaProductIndexGrainTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IVaProductIndexGrain GetIndex() =>
        _cluster.GrainFactory.GetGrain<IVaProductIndexGrain>("NDF-PRODUCT-INDEX");

    private async Task LoadTestProductsAsync()
    {
        await GetIndex().LoadProductsAsync(
        [
            new VaProductIndexEntry {
                Ien = "1001", Name = "ATROPINE SO4 0.4MG/ML INJ",
                VaGenericIen = "1", VaGenericName = "ATROPINE",
                DosageFormName = "INJECTION", Strength = "0.4", StrengthUnitName = "MG/ML",
                PrimaryDrugClassCode = "AU300", PrimaryDrugClassName = "PARASYMPATHOMIMETICS",
                FormularyIndicator = true, IsActive = true,
                NdcCodes = ["0002-1200-01"]
            },
            new VaProductIndexEntry {
                Ien = "2001", Name = "AMOXICILLIN 500MG CAP",
                VaGenericIen = "2", VaGenericName = "AMOXICILLIN",
                DosageFormName = "CAPSULE", Strength = "500", StrengthUnitName = "MG",
                PrimaryDrugClassCode = "AM200", PrimaryDrugClassName = "CEPHALOSPORINS",
                FormularyIndicator = true, IsActive = true,
                NdcCodes = ["0093-3108-01"]
            },
            new VaProductIndexEntry {
                Ien = "3001", Name = "METOPROLOL TARTRATE 25MG TAB",
                VaGenericIen = "3", VaGenericName = "METOPROLOL",
                DosageFormName = "TABLET", Strength = "25", StrengthUnitName = "MG",
                PrimaryDrugClassCode = "CV200", PrimaryDrugClassName = "ANTIHYPERTENSIVE AGENTS",
                FormularyIndicator = true, IsActive = true,
                NdcCodes = ["0006-0059-54"]
            },
            new VaProductIndexEntry {
                Ien = "3002", Name = "METOPROLOL TARTRATE 50MG TAB",
                VaGenericIen = "3", VaGenericName = "METOPROLOL",
                DosageFormName = "TABLET", Strength = "50", StrengthUnitName = "MG",
                PrimaryDrugClassCode = "CV200", PrimaryDrugClassName = "ANTIHYPERTENSIVE AGENTS",
                FormularyIndicator = true, IsActive = true,
                NdcCodes = ["0006-0060-54"]
            },
            new VaProductIndexEntry {
                Ien = "9001", Name = "AMIODARONE HCL 200MG TAB",
                VaGenericIen = "9", VaGenericName = "AMIODARONE",
                DosageFormName = "TABLET", Strength = "200", StrengthUnitName = "MG",
                PrimaryDrugClassCode = "CV400", PrimaryDrugClassName = "ANTIARRHYTHMIC AGENTS",
                FormularyIndicator = false, IsActive = true,
                NdcCodes = ["0187-0090-90"]
            },
        ]);
    }

    [Test]
    public async Task ProductIndex_LoadAndStatus()
    {
        await LoadTestProductsAsync();

        NdfProductIndexStatus status = await GetIndex().GetStatusAsync();
        Assert.That(status.IsLoaded, Is.True);
        Assert.That(status.TotalProducts, Is.EqualTo(5));
        Assert.That(status.FormularyProducts, Is.EqualTo(4));
        Assert.That(status.ActiveProducts, Is.EqualTo(5));
    }

    [Test]
    public async Task ProductIndex_GetByIen()
    {
        await LoadTestProductsAsync();

        VaProductIndexEntry? entry = await GetIndex().GetProductAsync("3001");
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.Name, Is.EqualTo("METOPROLOL TARTRATE 25MG TAB"));
        Assert.That(entry.VaGenericName, Is.EqualTo("METOPROLOL"));
    }

    [Test]
    public async Task ProductIndex_GetByIen_NotFound()
    {
        await LoadTestProductsAsync();

        VaProductIndexEntry? entry = await GetIndex().GetProductAsync("DOES-NOT-EXIST");
        Assert.That(entry, Is.Null);
    }

    [Test]
    public async Task ProductIndex_GetByNdc()
    {
        await LoadTestProductsAsync();

        VaProductIndexEntry? entry = await GetIndex().GetByNdcAsync("0093-3108-01");
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.Name, Is.EqualTo("AMOXICILLIN 500MG CAP"));
    }

    [Test]
    public async Task ProductIndex_GetByNdc_NotFound()
    {
        await LoadTestProductsAsync();

        VaProductIndexEntry? entry = await GetIndex().GetByNdcAsync("0000-0000-00");
        Assert.That(entry, Is.Null);
    }

    [Test]
    public async Task ProductIndex_GetByGeneric_TwoMetoprololProducts()
    {
        await LoadTestProductsAsync();

        List<VaProductIndexEntry> results = await GetIndex().GetByGenericAsync("3");
        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results.All(r => r.VaGenericName == "METOPROLOL"), Is.True);
    }

    [Test]
    public async Task ProductIndex_SearchByName()
    {
        await LoadTestProductsAsync();

        List<VaProductIndexEntry> results = await GetIndex()
            .SearchAsync("metoprolol", formularyOnly: false, drugClassCode: null, activeOnly: true, maxResults: 50);

        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results.All(r => r.VaGenericName == "METOPROLOL"), Is.True);
    }

    [Test]
    public async Task ProductIndex_SearchCaseInsensitive()
    {
        await LoadTestProductsAsync();

        List<VaProductIndexEntry> results = await GetIndex()
            .SearchAsync("AMOXICILLIN", formularyOnly: false, drugClassCode: null, activeOnly: true, maxResults: 50);

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].Ien, Is.EqualTo("2001"));
    }

    [Test]
    public async Task ProductIndex_SearchFormularyOnly()
    {
        await LoadTestProductsAsync();

        // Amiodarone is non-formulary; searching all should include it
        List<VaProductIndexEntry> all = await GetIndex()
            .SearchAsync("amiodarone", formularyOnly: false, drugClassCode: null, activeOnly: true, maxResults: 50);
        Assert.That(all, Has.Count.EqualTo(1));

        // formularyOnly should exclude it
        List<VaProductIndexEntry> formularyResults = await GetIndex()
            .SearchAsync("amiodarone", formularyOnly: true, drugClassCode: null, activeOnly: true, maxResults: 50);
        Assert.That(formularyResults, Has.Count.EqualTo(0));
    }

    [Test]
    public async Task ProductIndex_SearchByDrugClass()
    {
        await LoadTestProductsAsync();

        List<VaProductIndexEntry> results = await GetIndex().GetByDrugClassAsync("CV200");
        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results.All(r => r.PrimaryDrugClassCode == "CV200"), Is.True);
    }

    [Test]
    public async Task ProductIndex_Clear()
    {
        await LoadTestProductsAsync();

        await GetIndex().ClearAsync();
        NdfProductIndexStatus status = await GetIndex().GetStatusAsync();
        Assert.That(status.IsLoaded, Is.False);
        Assert.That(status.TotalProducts, Is.EqualTo(0));
    }
}

// ─── VaGenericIndexGrain Tests ────────────────────────────────────────────────

[TestFixture]
public class VaGenericIndexGrainTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IVaGenericIndexGrain GetIndex() =>
        _cluster.GrainFactory.GetGrain<IVaGenericIndexGrain>("NDF-GENERIC-INDEX");

    [Test]
    public async Task GenericIndex_LoadAndStatus()
    {
        await GetIndex().LoadGenericsAsync(
        [
            new VaGenericEntry { Ien = "1", Name = "ATROPINE", IsActive = true },
            new VaGenericEntry { Ien = "2", Name = "AMOXICILLIN", IsActive = true },
            new VaGenericEntry { Ien = "3", Name = "METOPROLOL", IsActive = true },
            new VaGenericEntry { Ien = "99", Name = "OBSOLETE DRUG", IsActive = false },
        ]);

        NdfGenericIndexStatus status = await GetIndex().GetStatusAsync();
        Assert.That(status.IsLoaded, Is.True);
        Assert.That(status.TotalGenerics, Is.EqualTo(4));
        Assert.That(status.ActiveGenerics, Is.EqualTo(3));
    }

    [Test]
    public async Task GenericIndex_GetByIen()
    {
        await GetIndex().LoadGenericsAsync(
        [
            new VaGenericEntry { Ien = "5", Name = "SERTRALINE", IsActive = true, Vuid = "4021053" },
        ]);

        VaGenericEntry? entry = await GetIndex().GetGenericAsync("5");
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.Name, Is.EqualTo("SERTRALINE"));
        Assert.That(entry.Vuid, Is.EqualTo("4021053"));
    }

    [Test]
    public async Task GenericIndex_SearchByName()
    {
        await GetIndex().LoadGenericsAsync(
        [
            new VaGenericEntry { Ien = "20", Name = "AMOXICILLIN", IsActive = true },
            new VaGenericEntry { Ien = "21", Name = "AMOXICILLIN/CLAVULANATE", IsActive = true },
            new VaGenericEntry { Ien = "22", Name = "AMPICILLIN", IsActive = true },
        ]);

        List<VaGenericEntry> results = await GetIndex().SearchAsync("amox", activeOnly: true, maxResults: 50);
        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results.All(r => r.Name.StartsWith("AMOX", StringComparison.OrdinalIgnoreCase)), Is.True);
    }
}

// ─── DrugClassIndexGrain Tests ────────────────────────────────────────────────

[TestFixture]
public class DrugClassIndexGrainTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IDrugClassIndexGrain GetIndex() =>
        _cluster.GrainFactory.GetGrain<IDrugClassIndexGrain>("NDF-CLASS-INDEX");

    private async Task LoadTestClassesAsync()
    {
        await GetIndex().LoadClassesAsync(
        [
            new DrugClassEntry { Code = "AM100", Name = "AMINOGLYCOSIDES", IsActive = true },
            new DrugClassEntry { Code = "AM110", Name = "AMINOGLYCOSIDES,OTHER", ParentCode = "AM100", IsActive = true },
            new DrugClassEntry { Code = "AM200", Name = "CEPHALOSPORINS", IsActive = true },
            new DrugClassEntry { Code = "AM210", Name = "CEPHALOSPORINS,1ST GEN", ParentCode = "AM200", IsActive = true },
            new DrugClassEntry { Code = "CV000", Name = "CARDIOVASCULAR AGENTS", IsActive = true },
            new DrugClassEntry { Code = "CV200", Name = "ANTIHYPERTENSIVE AGENTS", ParentCode = "CV000", IsActive = true },
            new DrugClassEntry { Code = "CV400", Name = "ANTIARRHYTHMIC AGENTS", ParentCode = "CV000", IsActive = true },
        ]);
    }

    [Test]
    public async Task ClassIndex_LoadAndStatus()
    {
        await LoadTestClassesAsync();

        NdfClassIndexStatus status = await GetIndex().GetStatusAsync();
        Assert.That(status.IsLoaded, Is.True);
        Assert.That(status.TotalClasses, Is.EqualTo(7));
    }

    [Test]
    public async Task ClassIndex_GetByCode()
    {
        await LoadTestClassesAsync();

        DrugClassEntry? entry = await GetIndex().GetClassAsync("AM100");
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.Name, Is.EqualTo("AMINOGLYCOSIDES"));
    }

    [Test]
    public async Task ClassIndex_GetByCode_CaseInsensitive()
    {
        await LoadTestClassesAsync();

        DrugClassEntry? entry = await GetIndex().GetClassAsync("cv200");
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.Name, Is.EqualTo("ANTIHYPERTENSIVE AGENTS"));
    }

    [Test]
    public async Task ClassIndex_GetByCode_NotFound()
    {
        await LoadTestClassesAsync();

        DrugClassEntry? entry = await GetIndex().GetClassAsync("ZZ999");
        Assert.That(entry, Is.Null);
    }

    [Test]
    public async Task ClassIndex_GetAllClasses_OrderedByCode()
    {
        await LoadTestClassesAsync();

        List<DrugClassEntry> all = await GetIndex().GetAllClassesAsync();
        Assert.That(all, Has.Count.EqualTo(7));
        // Verify ordered by code
        Assert.That(all[0].Code, Is.EqualTo("AM100"));
        Assert.That(all[1].Code, Is.EqualTo("AM110"));
    }

    [Test]
    public async Task ClassIndex_SearchByCodePrefix()
    {
        await LoadTestClassesAsync();

        List<DrugClassEntry> results = await GetIndex().SearchAsync("CV", maxResults: 50);
        Assert.That(results, Has.Count.EqualTo(3));
        Assert.That(results.All(r => r.Code.StartsWith("CV")), Is.True);
    }

    [Test]
    public async Task ClassIndex_SearchByName()
    {
        await LoadTestClassesAsync();

        List<DrugClassEntry> results = await GetIndex().SearchAsync("antihypertensive", maxResults: 50);
        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].Code, Is.EqualTo("CV200"));
    }

    [Test]
    public async Task ClassIndex_GetChildClasses()
    {
        await LoadTestClassesAsync();

        // Children of CV000 should be CV200 and CV400
        List<DrugClassEntry> children = await GetIndex().GetChildClassesAsync("CV000");
        Assert.That(children, Has.Count.EqualTo(2));
        Assert.That(children.Select(c => c.Code), Does.Contain("CV200"));
        Assert.That(children.Select(c => c.Code), Does.Contain("CV400"));
    }

    [Test]
    public async Task ClassIndex_IsLoaded()
    {
        await LoadTestClassesAsync();
        Assert.That(await GetIndex().IsLoadedAsync(), Is.True);
    }
}

// ─── DrugGrain Multi-Class Tests ──────────────────────────────────────────────

[TestFixture]
public class DrugGrainMultiClassTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    [Test]
    public async Task SaveDrug_RoundTripsSecondaryDrugClassCodes()
    {
        IDrugGrain grain = _cluster.GrainFactory.GetGrain<IDrugGrain>("DRUG-MC-1");

        await grain.SaveDrugAsync(new DrugState
        {
            LocalName = "ASPIRIN/CAFFEINE TAB",
            PrimaryDrugClassCode = "CN103",
            PrimaryDrugClassName = "NONOPIOID ANALGESICS",
            SecondaryDrugClassCodes = ["CN309", "BL117"]
        });

        DrugState state = await grain.GetDrugAsync();
        Assert.That(state.PrimaryDrugClassCode, Is.EqualTo("CN103"));
        Assert.That(state.SecondaryDrugClassCodes, Has.Count.EqualTo(2));
        Assert.That(state.SecondaryDrugClassCodes, Does.Contain("CN309"));
        Assert.That(state.SecondaryDrugClassCodes, Does.Contain("BL117"));
    }

    [Test]
    public async Task GetDrugClass_ReturnsPrimaryAndSecondaryClasses()
    {
        IDrugGrain grain = _cluster.GrainFactory.GetGrain<IDrugGrain>("DRUG-MC-2");

        await grain.SaveDrugAsync(new DrugState
        {
            LocalName = "WARFARIN/MULTI TAB",
            PrimaryDrugClassCode = "BL110",
            PrimaryDrugClassName = "ANTICOAGULANTS",
            SecondaryDrugClassCodes = ["CV000"]
        });

        DrugClassInfo info = await grain.GetDrugClassAsync();
        Assert.That(info.PrimaryDrugClassCode, Is.EqualTo("BL110"));
        Assert.That(info.PrimaryDrugClassName, Is.EqualTo("ANTICOAGULANTS"));
        Assert.That(info.SecondaryDrugClassCodes, Does.Contain("CV000"));

        // AllClassCodes is the union (primary first, then distinct secondaries).
        Assert.That(info.AllClassCodes, Has.Count.EqualTo(2));
        Assert.That(info.AllClassCodes[0], Is.EqualTo("BL110"));
        Assert.That(info.AllClassCodes, Does.Contain("CV000"));
    }

    [Test]
    public async Task GetDrugClass_DeduplicatesPrimaryAndEmptyCodes()
    {
        IDrugGrain grain = _cluster.GrainFactory.GetGrain<IDrugGrain>("DRUG-MC-3");

        await grain.SaveDrugAsync(new DrugState
        {
            LocalName = "DUP CLASS TAB",
            PrimaryDrugClassCode = "GA301",
            PrimaryDrugClassName = "PROTON PUMP INHIBITORS",
            // Includes a duplicate of the primary and a blank entry which must be filtered.
            SecondaryDrugClassCodes = ["GA301", "", "GA309"]
        });

        DrugClassInfo info = await grain.GetDrugClassAsync();
        Assert.That(info.AllClassCodes, Has.Count.EqualTo(2));
        Assert.That(info.AllClassCodes, Does.Contain("GA301"));
        Assert.That(info.AllClassCodes, Does.Contain("GA309"));
        Assert.That(info.AllClassCodes, Does.Not.Contain(string.Empty));
    }

    [Test]
    public async Task GetDrugClass_SingleClassDrug_ReturnsOnlyPrimary()
    {
        IDrugGrain grain = _cluster.GrainFactory.GetGrain<IDrugGrain>("DRUG-MC-4");

        await grain.SaveDrugAsync(new DrugState
        {
            LocalName = "SINGLE CLASS TAB",
            PrimaryDrugClassCode = "CV200",
            PrimaryDrugClassName = "ANTIHYPERTENSIVE AGENTS"
        });

        DrugClassInfo info = await grain.GetDrugClassAsync();
        Assert.That(info.SecondaryDrugClassCodes, Is.Empty);
        Assert.That(info.AllClassCodes, Has.Count.EqualTo(1));
        Assert.That(info.AllClassCodes[0], Is.EqualTo("CV200"));
    }
}

// ─── Test Silo Configurator ──────────────────────────────────────────────────

