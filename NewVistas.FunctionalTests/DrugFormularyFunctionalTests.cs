// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional / end-to-end tests for the Drug Formulary / National Drug File (NDF).
///
/// These tests exercise complete workflows that span multiple grains:
///   - Loading the catalog (classes → generics → products)
///   - Cross-reference lookups (NDC code → product → generic)
///   - Formulary management (check status, update, re-verify)
///   - Drug class hierarchy navigation
///   - CPRS-format data retrieval for order entry integration
///
/// Each test is fully self-contained: it loads its own test data
/// using unique IEN strings (GUID-based) to avoid cross-test interference.
/// </summary>
[TestFixture]
public class DrugFormularyCatalogWorkflowTests
{
    private TestCluster _cluster = default!;

    private const string ProductIndexKey = "NDF-PRODUCT-INDEX";
    private const string GenericIndexKey = "NDF-GENERIC-INDEX";
    private const string ClassIndexKey = "NDF-CLASS-INDEX";

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ─── Workflow 1: Full Catalog Load ────────────────────────────────────────

    /// <summary>
    /// Verifies the complete catalog loading workflow:
    /// drug classes → VA generics → VA products, then confirms
    /// all three indexes are populated and queryable.
    ///
    /// This mirrors the NDF installation process in VistA where
    /// KIDS distributes updated class, generic, and product globals.
    /// </summary>
    [Test]
    public async Task NdfCatalog_FullLoad_AllThreeIndexesQueryable()
    {
        string run = Guid.NewGuid().ToString("N")[..8];

        // Step 1: Load drug classes
        IDrugClassIndexGrain classIndex =
            _cluster.GrainFactory.GetGrain<IDrugClassIndexGrain>(ClassIndexKey);

        await classIndex.LoadClassesAsync(
        [
            new DrugClassEntry { Code = $"TS{run}", Name = "TEST ANTI-INFECTIVE", IsActive = true },
            new DrugClassEntry { Code = $"TS{run}A", Name = "TEST AMINOGLYCOSIDES", ParentCode = $"TS{run}", IsActive = true },
            new DrugClassEntry { Code = $"TS{run}B", Name = "TEST PENICILLINS", ParentCode = $"TS{run}", IsActive = true },
        ]);

        // Step 2: Load VA generics
        string genIen1 = $"GEN-{run}-1";
        string genIen2 = $"GEN-{run}-2";

        IVaGenericIndexGrain genericIndex =
            _cluster.GrainFactory.GetGrain<IVaGenericIndexGrain>(GenericIndexKey);

        await genericIndex.LoadGenericsAsync(
        [
            new VaGenericEntry { Ien = genIen1, Name = $"TESTOMYCIN-{run}", IsActive = true, Vuid = $"VUID-{run}-1" },
            new VaGenericEntry { Ien = genIen2, Name = $"TESTACILLIN-{run}", IsActive = true, Vuid = $"VUID-{run}-2" },
        ]);

        // Step 3: Load VA products referencing those generics and classes
        string prodIen1 = $"PROD-{run}-1";
        string prodIen2 = $"PROD-{run}-2";
        string prodIen3 = $"PROD-{run}-3";

        IVaProductIndexGrain productIndex =
            _cluster.GrainFactory.GetGrain<IVaProductIndexGrain>(ProductIndexKey);

        await productIndex.LoadProductsAsync(
        [
            new VaProductIndexEntry
            {
                Ien = prodIen1,
                Name = $"TESTOMYCIN 40MG/ML INJ-{run}",
                VaGenericIen = genIen1, VaGenericName = $"TESTOMYCIN-{run}",
                DosageFormName = "INJECTION", Strength = "40", StrengthUnitName = "MG/ML",
                PrimaryDrugClassCode = $"TS{run}A", PrimaryDrugClassName = "TEST AMINOGLYCOSIDES",
                FormularyIndicator = true, IsActive = true,
                NdcCodes = [$"0001-{run[..4]}-01"]
            },
            new VaProductIndexEntry
            {
                Ien = prodIen2,
                Name = $"TESTACILLIN 250MG CAP-{run}",
                VaGenericIen = genIen2, VaGenericName = $"TESTACILLIN-{run}",
                DosageFormName = "CAPSULE", Strength = "250", StrengthUnitName = "MG",
                PrimaryDrugClassCode = $"TS{run}B", PrimaryDrugClassName = "TEST PENICILLINS",
                FormularyIndicator = true, IsActive = true,
                NdcCodes = [$"0002-{run[..4]}-01"]
            },
            new VaProductIndexEntry
            {
                Ien = prodIen3,
                Name = $"TESTACILLIN 500MG CAP-{run}",
                VaGenericIen = genIen2, VaGenericName = $"TESTACILLIN-{run}",
                DosageFormName = "CAPSULE", Strength = "500", StrengthUnitName = "MG",
                PrimaryDrugClassCode = $"TS{run}B", PrimaryDrugClassName = "TEST PENICILLINS",
                FormularyIndicator = false, IsActive = true,
                NdcCodes = [$"0002-{run[..4]}-05"]
            },
        ]);

        // Verify class index
        NdfClassIndexStatus classStatus = await classIndex.GetStatusAsync();
        Assert.That(classStatus.IsLoaded, Is.True);

        DrugClassEntry? classEntry = await classIndex.GetClassAsync($"TS{run}A");
        Assert.That(classEntry, Is.Not.Null);
        Assert.That(classEntry!.Name, Is.EqualTo("TEST AMINOGLYCOSIDES"));

        // Verify generic index
        NdfGenericIndexStatus genericStatus = await genericIndex.GetStatusAsync();
        Assert.That(genericStatus.IsLoaded, Is.True);

        VaGenericEntry? genericEntry = await genericIndex.GetGenericAsync(genIen1);
        Assert.That(genericEntry, Is.Not.Null);
        Assert.That(genericEntry!.Name, Is.EqualTo($"TESTOMYCIN-{run}"));

        // Verify product index
        NdfProductIndexStatus productStatus = await productIndex.GetStatusAsync();
        Assert.That(productStatus.IsLoaded, Is.True);

        VaProductIndexEntry? productEntry = await productIndex.GetProductAsync(prodIen1);
        Assert.That(productEntry, Is.Not.Null);
        Assert.That(productEntry!.Name, Does.Contain("TESTOMYCIN"));
    }

    // ─── Workflow 2: NDC Cross-Reference Lookup ───────────────────────────────

    /// <summary>
    /// Verifies the NDC → VA Product cross-reference lookup workflow.
    /// A barcode scan at the point of care provides an NDC code;
    /// this must resolve to the VA Product for BCMA/pharmacy validation.
    ///
    /// Mirrors ^PSNDF(50.67,"NDC") cross-reference in VistA and
    /// the CIRN^PSNAPIS entry point used for interoperability.
    /// </summary>
    [Test]
    public async Task NdfCatalog_NdcLookup_ResolvesToCorrectProduct()
    {
        string run = Guid.NewGuid().ToString("N")[..8];
        string ndcCode = $"1234-{run[..4]}-99";
        string prodIen = $"PROD-NDC-{run}";

        IVaProductIndexGrain productIndex =
            _cluster.GrainFactory.GetGrain<IVaProductIndexGrain>(ProductIndexKey);

        await productIndex.LoadProductsAsync(
        [
            new VaProductIndexEntry
            {
                Ien = prodIen,
                Name = $"WARFARIN NA 5MG TAB-{run}",
                VaGenericIen = $"GEN-{run}", VaGenericName = "WARFARIN",
                DosageFormName = "TABLET", Strength = "5", StrengthUnitName = "MG",
                PrimaryDrugClassCode = "CV000", PrimaryDrugClassName = "CARDIOVASCULAR AGENTS",
                FormularyIndicator = true, IsActive = true,
                RxNormCode = "855332",
                NdcCodes = [ndcCode, $"9999-{run[..4]}-01"]
            }
        ]);

        // NDC lookup
        VaProductIndexEntry? byNdc = await productIndex.GetByNdcAsync(ndcCode);
        Assert.That(byNdc, Is.Not.Null);
        Assert.That(byNdc!.Ien, Is.EqualTo(prodIen));
        Assert.That(byNdc.Name, Does.Contain("WARFARIN"));
        Assert.That(byNdc.FormularyIndicator, Is.True);

        // Second NDC on same product also resolves
        VaProductIndexEntry? bySecondNdc = await productIndex.GetByNdcAsync($"9999-{run[..4]}-01");
        Assert.That(bySecondNdc, Is.Not.Null);
        Assert.That(bySecondNdc!.Ien, Is.EqualTo(prodIen));

        // Non-existent NDC returns null
        VaProductIndexEntry? notFound = await productIndex.GetByNdcAsync("0000-0000-00");
        Assert.That(notFound, Is.Null);
    }

    // ─── Workflow 3: Generic → All Products Lookup ────────────────────────────

    /// <summary>
    /// Verifies retrieving all VA Product formulations for a given VA Generic.
    /// This is how a clinician browses all available strengths and forms
    /// of a drug (e.g., "show me all METOPROLOL products").
    ///
    /// Mirrors VAP^PSNAPIS in VistA — returns all products for a generic
    /// with name, dosage form, class, and tier information.
    /// </summary>
    [Test]
    public async Task NdfCatalog_GenericToProducts_ReturnsAllFormulations()
    {
        string run = Guid.NewGuid().ToString("N")[..8];
        string genIen = $"GEN-METRO-{run}";

        IVaProductIndexGrain productIndex =
            _cluster.GrainFactory.GetGrain<IVaProductIndexGrain>(ProductIndexKey);

        await productIndex.LoadProductsAsync(
        [
            new VaProductIndexEntry {
                Ien = $"METRO-25-{run}", Name = $"METOPROLOL TARTRATE 25MG TAB-{run}",
                VaGenericIen = genIen, VaGenericName = "METOPROLOL",
                DosageFormName = "TABLET", Strength = "25", StrengthUnitName = "MG",
                PrimaryDrugClassCode = "CV200", PrimaryDrugClassName = "ANTIHYPERTENSIVE AGENTS",
                FormularyIndicator = true, IsActive = true, NdcCodes = []
            },
            new VaProductIndexEntry {
                Ien = $"METRO-50-{run}", Name = $"METOPROLOL TARTRATE 50MG TAB-{run}",
                VaGenericIen = genIen, VaGenericName = "METOPROLOL",
                DosageFormName = "TABLET", Strength = "50", StrengthUnitName = "MG",
                PrimaryDrugClassCode = "CV200", PrimaryDrugClassName = "ANTIHYPERTENSIVE AGENTS",
                FormularyIndicator = true, IsActive = true, NdcCodes = []
            },
            new VaProductIndexEntry {
                Ien = $"METRO-100-{run}", Name = $"METOPROLOL TARTRATE 100MG TAB-{run}",
                VaGenericIen = genIen, VaGenericName = "METOPROLOL",
                DosageFormName = "TABLET", Strength = "100", StrengthUnitName = "MG",
                PrimaryDrugClassCode = "CV200", PrimaryDrugClassName = "ANTIHYPERTENSIVE AGENTS",
                FormularyIndicator = false, IsActive = true, NdcCodes = []
            },
            new VaProductIndexEntry {
                Ien = $"METRO-INJ-{run}", Name = $"METOPROLOL TARTRATE 1MG/ML INJ-{run}",
                VaGenericIen = genIen, VaGenericName = "METOPROLOL",
                DosageFormName = "INJECTION", Strength = "1", StrengthUnitName = "MG/ML",
                PrimaryDrugClassCode = "CV200", PrimaryDrugClassName = "ANTIHYPERTENSIVE AGENTS",
                FormularyIndicator = true, IsActive = true, NdcCodes = []
            },
            // Unrelated product — should NOT appear in results
            new VaProductIndexEntry {
                Ien = $"LISIN-{run}", Name = $"LISINOPRIL 10MG TAB-{run}",
                VaGenericIen = $"GEN-LISIN-{run}", VaGenericName = "LISINOPRIL",
                DosageFormName = "TABLET", Strength = "10", StrengthUnitName = "MG",
                PrimaryDrugClassCode = "CV200", FormularyIndicator = true, IsActive = true, NdcCodes = []
            }
        ]);

        List<VaProductIndexEntry> metoprololProducts =
            await productIndex.GetByGenericAsync(genIen);

        Assert.That(metoprololProducts, Has.Count.EqualTo(4));
        Assert.That(metoprololProducts.All(p => p.VaGenericIen == genIen), Is.True);
        Assert.That(metoprololProducts.Select(p => p.DosageFormName),
            Does.Contain("INJECTION"));
    }

    // ─── Workflow 4: Formulary Management ────────────────────────────────────

    /// <summary>
    /// Verifies the formulary management workflow:
    /// a product starts as non-formulary, gets added to the formulary
    /// with a tier and restriction, and the individual grain reflects the change.
    ///
    /// Mirrors the national formulary update process where VA Central
    /// Office changes a drug's formulary status and restrictions.
    /// </summary>
    [Test]
    public async Task NdfCatalog_FormularyManagement_UpdatesReflectedInGrain()
    {
        string run = Guid.NewGuid().ToString("N")[..8];
        string prodIen = $"PROD-FORM-{run}";

        // Load product as non-formulary
        IVaProductGrain productGrain =
            _cluster.GrainFactory.GetGrain<IVaProductGrain>(prodIen);

        await productGrain.LoadProductAsync(
            name: $"EXPENSIVE DRUG 10MG TAB-{run}",
            vaGenericIen: $"GEN-{run}",
            vaGenericName: "EXPENSIVE DRUG",
            dosageFormIen: string.Empty,
            dosageFormName: "TABLET",
            strength: "10",
            strengthUnitIen: string.Empty,
            strengthUnitName: "MG",
            printName: $"EXPENSIVE DRUG 10MG TAB-{run}",
            primaryDrugClassCode: "CN104",
            primaryDrugClassName: "ANTIDEPRESSANTS,SSRI",
            formularyIndicator: false,
            formularyRestrictions: null,
            controlledSubstanceSchedule: null,
            copayTier: null,
            maxSingleDose: null, minSingleDose: null,
            maxDailyDose: null, minDailyDose: null, maxCumulativeDose: null,
            doseUnit: null,
            isDosageCheckExcluded: false, isHazardousWaste: false,
            rxNormCode: null, vuid: null,
            isActive: true, inactivationDate: null,
            ingredients: [], ndcCodes: [], secondaryDrugClassCodes: []);

        // Verify initial state: not formulary
        Assert.That(await productGrain.IsFormularyAsync(), Is.False);
        Assert.That(await productGrain.GetFormularyRestrictionsAsync(), Is.Null);

        // VA Central Office adds it to the formulary with restrictions
        await productGrain.UpdateFormularyStatusAsync(
            inFormulary: true,
            restrictions: "Requires prior authorization from Mental Health",
            copayTier: "Tier 2");

        // Verify updated formulary state
        Assert.That(await productGrain.IsFormularyAsync(), Is.True);
        Assert.That(await productGrain.GetFormularyRestrictionsAsync(),
            Is.EqualTo("Requires prior authorization from Mental Health"));

        VaProductState state = await productGrain.GetProductAsync();
        Assert.That(state.CopayTier, Is.EqualTo("Tier 2"));
        Assert.That(state.FormularyIndicator, Is.True);

        // Later: remove from formulary
        await productGrain.UpdateFormularyStatusAsync(false, null, null);
        Assert.That(await productGrain.IsFormularyAsync(), Is.False);
    }

    // ─── Workflow 5: Drug Class Hierarchy Navigation ──────────────────────────

    /// <summary>
    /// Verifies the drug class hierarchy workflow:
    /// load a multi-level class hierarchy, navigate parent → children,
    /// then find all products in a class.
    ///
    /// Mirrors VistA drug class browsing used in CPRS formulary lookups.
    /// VA classes are 2-level: e.g., CV (parent) → CV200 (child).
    /// </summary>
    [Test]
    public async Task NdfCatalog_DrugClassHierarchy_NavigatesParentToChildToProducts()
    {
        string run = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        string parentCode = $"XX{run[..4]}";
        string child1Code = $"XX{run[..4]}1";
        string child2Code = $"XX{run[..4]}2";

        IDrugClassIndexGrain classIndex =
            _cluster.GrainFactory.GetGrain<IDrugClassIndexGrain>(ClassIndexKey);

        await classIndex.LoadClassesAsync(
        [
            new DrugClassEntry { Code = parentCode, Name = $"PARENT CLASS-{run}", IsActive = true },
            new DrugClassEntry { Code = child1Code, Name = $"CHILD CLASS A-{run}", ParentCode = parentCode, IsActive = true },
            new DrugClassEntry { Code = child2Code, Name = $"CHILD CLASS B-{run}", ParentCode = parentCode, IsActive = true },
        ]);

        IVaProductIndexGrain productIndex =
            _cluster.GrainFactory.GetGrain<IVaProductIndexGrain>(ProductIndexKey);

        await productIndex.LoadProductsAsync(
        [
            new VaProductIndexEntry {
                Ien = $"PROD-C1-{run}", Name = $"CLASS A DRUG 10MG TAB-{run}",
                VaGenericIen = $"G{run}1", VaGenericName = "CLASS A DRUG",
                DosageFormName = "TABLET", Strength = "10", StrengthUnitName = "MG",
                PrimaryDrugClassCode = child1Code, PrimaryDrugClassName = $"CHILD CLASS A-{run}",
                FormularyIndicator = true, IsActive = true, NdcCodes = []
            },
            new VaProductIndexEntry {
                Ien = $"PROD-C2A-{run}", Name = $"CLASS B DRUG 5MG CAP-{run}",
                VaGenericIen = $"G{run}2", VaGenericName = "CLASS B DRUG",
                DosageFormName = "CAPSULE", Strength = "5", StrengthUnitName = "MG",
                PrimaryDrugClassCode = child2Code, PrimaryDrugClassName = $"CHILD CLASS B-{run}",
                FormularyIndicator = true, IsActive = true, NdcCodes = []
            },
            new VaProductIndexEntry {
                Ien = $"PROD-C2B-{run}", Name = $"CLASS B DRUG 10MG CAP-{run}",
                VaGenericIen = $"G{run}2", VaGenericName = "CLASS B DRUG",
                DosageFormName = "CAPSULE", Strength = "10", StrengthUnitName = "MG",
                PrimaryDrugClassCode = child2Code, PrimaryDrugClassName = $"CHILD CLASS B-{run}",
                FormularyIndicator = false, IsActive = true, NdcCodes = []
            },
        ]);

        // Navigate: parent → children
        List<DrugClassEntry> children = await classIndex.GetChildClassesAsync(parentCode);
        Assert.That(children, Has.Count.EqualTo(2));
        Assert.That(children.Select(c => c.Code), Does.Contain(child1Code));
        Assert.That(children.Select(c => c.Code), Does.Contain(child2Code));

        // Navigate: class → products
        List<VaProductIndexEntry> child2Products =
            await productIndex.GetByDrugClassAsync(child2Code);
        Assert.That(child2Products, Has.Count.EqualTo(2));
        Assert.That(child2Products.All(p => p.PrimaryDrugClassCode == child2Code), Is.True);

        // Verify child class details
        DrugClassEntry? child1 = await classIndex.GetClassAsync(child1Code);
        Assert.That(child1, Is.Not.Null);
        Assert.That(child1!.ParentCode, Is.EqualTo(parentCode));
    }

    // ─── Workflow 6: CPRS Order Entry Integration ─────────────────────────────

    /// <summary>
    /// Verifies CPRS-format data retrieval workflow for order entry integration.
    /// When a clinician orders a drug in CPRS, the order dialog calls
    /// $$CPRS^PSNAPIS to get "dosform^class^strength^units" for display.
    ///
    /// Also verifies the dosage strength summary used on medication labels.
    /// </summary>
    [Test]
    public async Task NdfCatalog_CprsFormat_CorrectlyFormatsForOrderEntry()
    {
        string run = Guid.NewGuid().ToString("N")[..8];
        string prodIen = $"PROD-CPRS-{run}";

        IVaProductGrain productGrain =
            _cluster.GrainFactory.GetGrain<IVaProductGrain>(prodIen);

        await productGrain.LoadProductAsync(
            name: "GENTAMICIN SO4 40MG/ML INJ",
            vaGenericIen: $"GEN-GENT-{run}",
            vaGenericName: "GENTAMICIN",
            dosageFormIen: "INJ",
            dosageFormName: "INJECTION",
            strength: "40",
            strengthUnitIen: "MGML",
            strengthUnitName: "MG/ML",
            printName: "GENTAMICIN 40MG/ML INJ",
            primaryDrugClassCode: "AM100",
            primaryDrugClassName: "AMINOGLYCOSIDES",
            formularyIndicator: true,
            formularyRestrictions: null,
            controlledSubstanceSchedule: null,
            copayTier: "Tier 1",
            maxSingleDose: 160m,
            minSingleDose: 1m,
            maxDailyDose: 480m,
            minDailyDose: 3m,
            maxCumulativeDose: null,
            doseUnit: "MG",
            isDosageCheckExcluded: false,
            isHazardousWaste: false,
            rxNormCode: "1665005",
            vuid: "4019700",
            isActive: true,
            inactivationDate: null,
            ingredients:
            [
                new DrugIngredient
                {
                    IngredientIen = "ING-001",
                    Name = "GENTAMICIN SULFATE",
                    Strength = "40",
                    StrengthUnit = "MG/ML"
                }
            ],
            ndcCodes:
            [
                new NdcEntry { NdcCode = "0002-7232-50", TradeName = "GARAMYCIN", IsActive = true }
            ],
            secondaryDrugClassCodes: []);

        // Verify CPRS format: "dosform^class^strength^units"
        string cprs = await productGrain.GetCprsFormatAsync();
        Assert.That(cprs, Is.EqualTo("INJECTION^AM100^40^MG/ML"));

        // Verify dosage strength summary for medication label
        string summary = await productGrain.GetDosageStrengthSummaryAsync();
        Assert.That(summary, Is.EqualTo("INJECTION 40 MG/ML"));

        // Verify full state including dosing limits
        VaProductState state = await productGrain.GetProductAsync();
        Assert.That(state.MaxSingleDose, Is.EqualTo(160m));
        Assert.That(state.MaxDailyDose, Is.EqualTo(480m));
        Assert.That(state.DoseUnit, Is.EqualTo("MG"));
        Assert.That(state.CopayTier, Is.EqualTo("Tier 1"));
        Assert.That(state.Ingredients, Has.Count.EqualTo(1));
        Assert.That(state.Ingredients[0].Name, Is.EqualTo("GENTAMICIN SULFATE"));
        Assert.That(state.NdcCodes[0].TradeName, Is.EqualTo("GARAMYCIN"));
        Assert.That(state.Vuid, Is.EqualTo("4019700"));
    }

    // ─── Workflow 7: Product Lifecycle (Activate/Deactivate) ─────────────────

    /// <summary>
    /// Verifies the product lifecycle workflow:
    /// a product is active, gets discontinued (deactivated), then
    /// a replacement product is added and the old one stays queryable
    /// for historical reference.
    ///
    /// Mirrors VistA NDF inactivation when a manufacturer discontinues
    /// a product — the record is retained but marked inactive.
    /// </summary>
    [Test]
    public async Task NdfCatalog_ProductLifecycle_DiscontinuedProductRetainedForHistory()
    {
        string run = Guid.NewGuid().ToString("N")[..8];
        string oldProdIen = $"PROD-OLD-{run}";
        string newProdIen = $"PROD-NEW-{run}";
        string genIen = $"GEN-LIFECYCLE-{run}";

        IVaProductGrain oldProduct =
            _cluster.GrainFactory.GetGrain<IVaProductGrain>(oldProdIen);

        await oldProduct.LoadProductAsync(
            $"OLDMYCIN 100MG TAB-{run}", genIen, "OLDMYCIN",
            string.Empty, "TABLET", "100", string.Empty, "MG",
            $"OLDMYCIN 100MG TAB-{run}", "AM100", "AMINOGLYCOSIDES",
            true, null, null, null, null, null, null, null, null, null,
            false, false, null, null, true, null,
            [], [], []);

        Assert.That(await oldProduct.IsActiveAsync(), Is.True);

        // Product discontinued — manufacturer stops making it
        DateTime discontinuedDate = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        await oldProduct.DeactivateAsync(discontinuedDate);

        Assert.That(await oldProduct.IsActiveAsync(), Is.False);
        VaProductState oldState = await oldProduct.GetProductAsync();
        Assert.That(oldState.InactivationDate, Is.EqualTo(discontinuedDate));
        Assert.That(oldState.Name, Does.Contain("OLDMYCIN")); // Still retrievable

        // Replacement product added
        IVaProductGrain newProduct =
            _cluster.GrainFactory.GetGrain<IVaProductGrain>(newProdIen);

        await newProduct.LoadProductAsync(
            $"NEWMYCIN 100MG TAB-{run}", genIen, "OLDMYCIN",
            string.Empty, "TABLET", "100", string.Empty, "MG",
            $"NEWMYCIN 100MG TAB-{run}", "AM100", "AMINOGLYCOSIDES",
            true, null, null, null, null, null, null, null, null, null,
            false, false, null, null, true, null,
            [], [], []);

        Assert.That(await newProduct.IsActiveAsync(), Is.True);

        // Load both into the index — old product is inactive, new is active
        IVaProductIndexGrain productIndex =
            _cluster.GrainFactory.GetGrain<IVaProductIndexGrain>(ProductIndexKey);

        await productIndex.LoadProductsAsync(
        [
            new VaProductIndexEntry {
                Ien = oldProdIen, Name = $"OLDMYCIN 100MG TAB-{run}",
                VaGenericIen = genIen, VaGenericName = "OLDMYCIN",
                DosageFormName = "TABLET", Strength = "100", StrengthUnitName = "MG",
                PrimaryDrugClassCode = "AM100", PrimaryDrugClassName = "AMINOGLYCOSIDES",
                FormularyIndicator = true, IsActive = false, NdcCodes = []
            },
            new VaProductIndexEntry {
                Ien = newProdIen, Name = $"NEWMYCIN 100MG TAB-{run}",
                VaGenericIen = genIen, VaGenericName = "OLDMYCIN",
                DosageFormName = "TABLET", Strength = "100", StrengthUnitName = "MG",
                PrimaryDrugClassCode = "AM100", PrimaryDrugClassName = "AMINOGLYCOSIDES",
                FormularyIndicator = true, IsActive = true, NdcCodes = []
            },
        ]);

        // Searching activeOnly:true returns only the replacement
        List<VaProductIndexEntry> activeResults = await productIndex.SearchAsync(
            "NEWMYCIN", formularyOnly: false, drugClassCode: null, activeOnly: true, maxResults: 50);
        Assert.That(activeResults, Has.Count.EqualTo(1));
        Assert.That(activeResults[0].Ien, Is.EqualTo(newProdIen));

        // Searching activeOnly:false returns both
        List<VaProductIndexEntry> allResults = await productIndex.SearchAsync(
            "MYCIN", formularyOnly: false, drugClassCode: null, activeOnly: false, maxResults: 50);
        List<VaProductIndexEntry> thisRunResults = allResults
            .Where(r => r.Ien == oldProdIen || r.Ien == newProdIen)
            .ToList();
        Assert.That(thisRunResults, Has.Count.EqualTo(2));
    }

    // ─── Workflow 8: Formulary Filter in Search ───────────────────────────────

    /// <summary>
    /// Verifies that formulary-only search correctly filters products.
    /// A pharmacy staff member wants to see only VA National Formulary
    /// drugs when entering a prescription.
    ///
    /// Mirrors the "NF" (National Formulary) filter in PSNLOOK.m.
    /// </summary>
    [Test]
    public async Task NdfCatalog_FormularyFilter_SearchReturnsOnlyFormularyDrugs()
    {
        string run = Guid.NewGuid().ToString("N")[..8];
        string genIen = $"GEN-FORM-{run}";

        IVaProductIndexGrain productIndex =
            _cluster.GrainFactory.GetGrain<IVaProductIndexGrain>(ProductIndexKey);

        await productIndex.LoadProductsAsync(
        [
            new VaProductIndexEntry {
                Ien = $"FORM-YES-{run}", Name = $"FORMULARY DRUG 10MG TAB-{run}",
                VaGenericIen = genIen, VaGenericName = $"FORMULARY DRUG-{run}",
                DosageFormName = "TABLET", Strength = "10", StrengthUnitName = "MG",
                PrimaryDrugClassCode = "CN104", FormularyIndicator = true, IsActive = true,
                NdcCodes = []
            },
            new VaProductIndexEntry {
                Ien = $"FORM-NO-{run}", Name = $"NONFORMULARY DRUG 20MG TAB-{run}",
                VaGenericIen = genIen, VaGenericName = $"FORMULARY DRUG-{run}",
                DosageFormName = "TABLET", Strength = "20", StrengthUnitName = "MG",
                PrimaryDrugClassCode = "CN104", FormularyIndicator = false, IsActive = true,
                NdcCodes = []
            },
        ]);

        // Search without filter returns both
        List<VaProductIndexEntry> allResults = await productIndex.SearchAsync(
            $"FORMULARY DRUG-{run}", formularyOnly: false, drugClassCode: null, activeOnly: true, maxResults: 50);
        List<VaProductIndexEntry> thisRun = allResults
            .Where(r => r.VaGenericIen == genIen).ToList();
        Assert.That(thisRun, Has.Count.EqualTo(2));

        // Formulary-only filter returns only the formulary product
        List<VaProductIndexEntry> formularyResults = await productIndex.SearchAsync(
            $"FORMULARY DRUG-{run}", formularyOnly: true, drugClassCode: null, activeOnly: true, maxResults: 50);
        List<VaProductIndexEntry> formularyThisRun = formularyResults
            .Where(r => r.VaGenericIen == genIen).ToList();
        Assert.That(formularyThisRun, Has.Count.EqualTo(1));
        Assert.That(formularyThisRun[0].FormularyIndicator, Is.True);
    }
}

// ─── Test Silo Configurator ───────────────────────────────────────────────────

