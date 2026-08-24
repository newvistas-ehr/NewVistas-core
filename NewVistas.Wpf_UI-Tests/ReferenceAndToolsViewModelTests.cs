// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.ViewModels;
using NSubstitute;

namespace NewVistas.Wpf_UI_Tests;

/// <summary>
/// Tests for reference/lookup and tools ViewModels: ICD-10, Drug File, Drug Formulary,
/// Lexicon, MPI, BedManagement, EmergencyDept, MailMan, etc.
/// </summary>
[TestFixture]
public class ReferenceAndToolsViewModelTests : ViewModelTestBase
{
    // ── ICD-10 Browser ────────────────────────────────────────────────────

    [Test]
    public async Task Icd10Browser_Search_PopulatesResults()
    {
        var mockIndex = Substitute.For<IIcd10IndexGrain>();
        mockIndex.SearchAsync("diabetes", false, 50).Returns(new List<Icd10IndexEntry>
        {
            new() { Code = "E11", ShortDescription = "Type 2 diabetes" }
        });
        MockGrainFactory.GetGrain<IIcd10IndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockIndex);

        var vm = new Icd10BrowserViewModel(GrainService) { SearchTerm = "diabetes" };
        await vm.SearchCommand.ExecuteAsync(null);

        Assert.That(vm.Results, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Icd10Browser_SelectCode_LoadsDetail()
    {
        var mockGrain = Substitute.For<IIcd10Grain>();
        mockGrain.GetCodeAsync().Returns(new Icd10State { Code = "E11" });
        MockGrainFactory.GetGrain<IIcd10Grain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockGrain);

        var vm = new Icd10BrowserViewModel(GrainService);
        await vm.SelectCodeCommand.ExecuteAsync(new Icd10IndexEntry { Code = "E11" });

        Assert.That(vm.SelectedCode, Is.Not.Null);
        Assert.That(vm.SelectedCode!.Code, Is.EqualTo("E11"));
    }

    [Test]
    public async Task Icd10Browser_EmptySearch_DoesNothing()
    {
        var vm = new Icd10BrowserViewModel(GrainService) { SearchTerm = "" };
        await vm.SearchCommand.ExecuteAsync(null);
        Assert.That(vm.Results, Has.Count.EqualTo(0));
    }

    // ── Drug File ─────────────────────────────────────────────────────────

    [Test]
    public async Task DrugFile_SearchDrugs_PopulatesResults()
    {
        var mockIndex = Substitute.For<IDrugIndexGrain>();
        mockIndex.SearchAsync("aspirin", null, true, 50).Returns(new List<DrugIndexEntry>
        {
            new() { Ien = "123", LocalName = "ASPIRIN TAB" }
        });
        MockGrainFactory.GetGrain<IDrugIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockIndex);

        var vm = new DrugFileViewModel(GrainService) { SearchTerm = "aspirin" };
        await vm.SearchDrugsCommand.ExecuteAsync(null);

        Assert.That(vm.Results, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task DrugFile_SelectDrug_LoadsDetail()
    {
        var mockGrain = Substitute.For<IDrugGrain>();
        mockGrain.GetDrugAsync().Returns(new DrugState { LocalName = "ASPIRIN" });
        MockGrainFactory.GetGrain<IDrugGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockGrain);

        var vm = new DrugFileViewModel(GrainService);
        await vm.SelectDrugCommand.ExecuteAsync(new DrugIndexEntry { Ien = "123" });

        Assert.That(vm.SelectedDrug, Is.Not.Null);
    }

    [Test]
    public async Task DrugFile_EmptySearch_DoesNothing()
    {
        var vm = new DrugFileViewModel(GrainService) { SearchTerm = "" };
        await vm.SearchDrugsCommand.ExecuteAsync(null);
        Assert.That(vm.Results, Has.Count.EqualTo(0));
    }

    // ── Drug Formulary ────────────────────────────────────────────────────

    [Test]
    public async Task DrugFormulary_Search_PopulatesResults()
    {
        var mockIndex = Substitute.For<IVaProductIndexGrain>();
        mockIndex.SearchAsync("metformin", false, null, true, 50).Returns(new List<VaProductIndexEntry>
        {
            new() { Ien = "456" }
        });
        MockGrainFactory.GetGrain<IVaProductIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockIndex);

        var vm = new DrugFormularyViewModel(GrainService) { SearchTerm = "metformin" };
        await vm.SearchCommand.ExecuteAsync(null);

        Assert.That(vm.Results, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task DrugFormulary_SelectProduct_LoadsDetail()
    {
        var mockGrain = Substitute.For<IVaProductGrain>();
        mockGrain.GetProductAsync().Returns(new VaProductState { Name = "METFORMIN" });
        MockGrainFactory.GetGrain<IVaProductGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockGrain);

        var vm = new DrugFormularyViewModel(GrainService);
        await vm.SelectProductCommand.ExecuteAsync(new VaProductIndexEntry { Ien = "456" });

        Assert.That(vm.SelectedProduct, Is.Not.Null);
    }

    [Test]
    public async Task DrugFormulary_EmptySearch_DoesNothing()
    {
        var vm = new DrugFormularyViewModel(GrainService) { SearchTerm = "" };
        await vm.SearchCommand.ExecuteAsync(null);
        Assert.That(vm.Results, Has.Count.EqualTo(0));
    }

    // ── Lexicon ───────────────────────────────────────────────────────────

    [Test]
    public async Task Lexicon_Search_PopulatesResults()
    {
        var mockGrain = Substitute.For<ILexiconSearchGrain>();
        mockGrain.SearchAsync("hypertension", null, 50).Returns(new List<LexiconIndexEntry> { new() });
        MockGrainFactory.GetGrain<ILexiconSearchGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockGrain);

        var vm = new LexiconViewModel(GrainService) { SearchTerm = "hypertension" };
        await vm.SearchCommand.ExecuteAsync(null);

        Assert.That(vm.SearchResults, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Lexicon_Lookup_ReturnsResult()
    {
        var mockGrain = Substitute.For<ILexiconSearchGrain>();
        mockGrain.LookupCodeAsync("I10", "ICD10").Returns(new LexiconIndexEntry { Code = "I10" });
        MockGrainFactory.GetGrain<ILexiconSearchGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockGrain);

        var vm = new LexiconViewModel(GrainService) { LookupSystem = "ICD10", LookupCode = "I10" };
        await vm.LookupCommand.ExecuteAsync(null);

        Assert.That(vm.LookupResult, Is.Not.Null);
    }

    [Test]
    public async Task Lexicon_EmptySearch_SetsError()
    {
        var vm = new LexiconViewModel(GrainService) { SearchTerm = "" };
        await vm.SearchCommand.ExecuteAsync(null);
        Assert.That(vm.Error, Is.EqualTo("Enter a search term"));
    }

    // ── MPI ───────────────────────────────────────────────────────────────

    [Test]
    public async Task Mpi_Search_PopulatesResults()
    {
        var mockGrain = Substitute.For<IMpiSearchGrain>();
        mockGrain.SearchAsync("Smith", 50).Returns(new List<MpiSearchResult> { new() });
        MockGrainFactory.GetGrain<IMpiSearchGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockGrain);

        var vm = new MasterPatientIndexViewModel(GrainService) { SearchQuery = "Smith" };
        await vm.SearchCommand.ExecuteAsync(null);

        Assert.That(vm.SearchResults, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Mpi_FindMatch_PopulatesCandidates()
    {
        var mockGrain = Substitute.For<IMpiMatchGrain>();
        mockGrain.FindCandidatesAsync("Smith", "1234", null, null, 0.0)
            .Returns(new List<MpiMatchCandidate> { new() });
        MockGrainFactory.GetGrain<IMpiMatchGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockGrain);

        var vm = new MasterPatientIndexViewModel(GrainService)
        {
            MatchName = "Smith", MatchSsn = "1234"
        };
        await vm.FindMatchCommand.ExecuteAsync(null);

        Assert.That(vm.MatchCandidates, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Mpi_EmptySearch_SetsError()
    {
        var vm = new MasterPatientIndexViewModel(GrainService) { SearchQuery = "" };
        await vm.SearchCommand.ExecuteAsync(null);
        Assert.That(vm.Error, Is.EqualTo("Enter a search query"));
    }

    // ── Bed Management ────────────────────────────────────────────────────

    [Test]
    public async Task BedManagement_Load_PopulatesBeds()
    {
        var mockCapacity = Substitute.For<IBedCapacityGrain>();
        mockCapacity.GetUnitsAsync(Arg.Any<bool>()).Returns(new List<UnitCapacitySummary>
        {
            new() { UnitId = "MED-3A", InstitutionId = "500", TotalBeds = 10, Available = 5, Occupied = 4 }
        });
        var mockUnit = Substitute.For<IInpatientUnitGrain>();
        mockUnit.GetAsync().Returns(new InpatientUnitState
        {
            UnitId = "MED-3A",
            InstitutionId = "500",
            Beds = new List<InpatientBed> { new() { BedId = "B1", State = BedLifecycleState.Available } }
        });
        MockGrainFactory.GetGrain<IBedCapacityGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockCapacity);
        MockGrainFactory.GetGrain<IInpatientUnitGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockUnit);

        var vm = new BedManagementViewModel(GrainService);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.That(vm.Beds, Has.Count.EqualTo(1));
        Assert.That(vm.Stats, Is.Not.Null);
        Assert.That(vm.Stats!.TotalBeds, Is.EqualTo(10));
    }

    [Test]
    public async Task BedManagement_MarkAvailable_CallsGrain()
    {
        var mockCapacity = Substitute.For<IBedCapacityGrain>();
        var mockUnit = Substitute.For<IInpatientUnitGrain>();
        mockCapacity.GetUnitsAsync(Arg.Any<bool>()).Returns(new List<UnitCapacitySummary>());
        MockGrainFactory.GetGrain<IBedCapacityGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockCapacity);
        MockGrainFactory.GetGrain<IInpatientUnitGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockUnit);

        var vm = new BedManagementViewModel(GrainService);
        vm.SelectedBed = new BedBoardEntry("B1", "MED-3A", "Cleaning", null, null);
        await vm.MarkAvailableCommand.ExecuteAsync(null);

        await mockUnit.Received().MarkBedCleanAsync("B1", Arg.Any<string?>());
    }

    [Test]
    public async Task BedManagement_MarkDirty_CallsGrain()
    {
        var mockCapacity = Substitute.For<IBedCapacityGrain>();
        var mockUnit = Substitute.For<IInpatientUnitGrain>();
        mockCapacity.GetUnitsAsync(Arg.Any<bool>()).Returns(new List<UnitCapacitySummary>());
        MockGrainFactory.GetGrain<IBedCapacityGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockCapacity);
        MockGrainFactory.GetGrain<IInpatientUnitGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockUnit);

        var vm = new BedManagementViewModel(GrainService);
        vm.SelectedBed = new BedBoardEntry("B1", "MED-3A", "Available", null, null);
        await vm.MarkDirtyCommand.ExecuteAsync(null);

        await mockUnit.Received().MarkBedDirtyAsync("B1");
    }

    // ── Site Parameters ───────────────────────────────────────────────────

    [Test]
    public async Task SiteParameters_LoadDisplaySettings_PopulatesCounts()
    {
        var mockGrain = Substitute.For<ISiteParametersGrain>();
        mockGrain.GetVitalsDisplayCountAsync().Returns(10);
        mockGrain.GetOrdersDisplayCountAsync().Returns(5);
        mockGrain.GetNotesDisplayCountAsync().Returns(15);
        MockGrainFactory.GetGrain<ISiteParametersGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockGrain);

        var vm = new SiteParametersViewModel(GrainService);
        await vm.LoadDisplaySettingsCommand.ExecuteAsync(null);

        Assert.That(vm.VitalsDisplayCount, Is.EqualTo(10));
        Assert.That(vm.OrdersDisplayCount, Is.EqualTo(5));
        Assert.That(vm.NotesDisplayCount, Is.EqualTo(15));
    }

    [Test]
    public async Task SiteParameters_SaveDisplaySettings_CallsGrain()
    {
        var mockGrain = Substitute.For<ISiteParametersGrain>();
        MockGrainFactory.GetGrain<ISiteParametersGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockGrain);

        var vm = new SiteParametersViewModel(GrainService)
        {
            VitalsDisplayCount = 20, OrdersDisplayCount = 10, NotesDisplayCount = 25
        };
        await vm.SaveDisplaySettingsCommand.ExecuteAsync(null);

        await mockGrain.Received().SetVitalsDisplayCountAsync(20);
        await mockGrain.Received().SetOrdersDisplayCountAsync(10);
        await mockGrain.Received().SetNotesDisplayCountAsync(25);
    }

    [Test]
    public async Task SiteParameters_LoadParameters_PopulatesList()
    {
        var mockGrain = Substitute.For<ISiteParametersGrain>();
        mockGrain.GetParametersAsync().Returns(new SiteParametersState
        {
            Parameters = new Dictionary<string, string> { { "KEY1", "VAL1" } }
        });
        MockGrainFactory.GetGrain<ISiteParametersGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockGrain);

        var vm = new SiteParametersViewModel(GrainService);
        await vm.LoadParametersCommand.ExecuteAsync(null);

        Assert.That(vm.Parameters, Has.Count.EqualTo(1));
    }

    // ── Drug Accountability ───────────────────────────────────────────────

    [Test]
    public async Task DrugAccountability_LoadInventory_PopulatesDrugs()
    {
        var mockLoc = Substitute.For<IDrugAccountabilityLocationGrain>();
        mockLoc.GetAllDrugsAsync().Returns(new List<DrugBalanceSummary> { new() });
        MockGrainFactory.GetGrain<IDrugAccountabilityLocationGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockLoc);

        var vm = new DrugAccountabilityViewModel(GrainService);
        await vm.LoadInventoryCommand.ExecuteAsync(null);

        Assert.That(vm.Drugs, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task DrugAccountability_SelectDrug_LoadsHistory()
    {
        var mockGrain = Substitute.For<IDrugAccountabilityGrain>();
        mockGrain.GetTransactionHistoryAsync().Returns(new List<DrugAccountabilityTransaction> { new() });
        MockGrainFactory.GetGrain<IDrugAccountabilityGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockGrain);

        var vm = new DrugAccountabilityViewModel(GrainService);
        await vm.SelectDrugCommand.ExecuteAsync(new DrugBalanceSummary { DrugId = "D1" });

        Assert.That(vm.TransactionHistory, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task DrugAccountability_EmptyLocation_DoesNothing()
    {
        var vm = new DrugAccountabilityViewModel(GrainService) { LocationId = "" };
        await vm.LoadInventoryCommand.ExecuteAsync(null);
        Assert.That(vm.Drugs, Has.Count.EqualTo(0));
    }

    // ── Geriatrics/EC ─────────────────────────────────────────────────────

    [Test]
    public async Task GeriatricsEC_Load_PopulatesAssessments()
    {
        var mockIdx = Substitute.For<IGECAssessmentIndexGrain>();
        mockIdx.GetAllAssessmentsAsync().Returns(new List<GECAssessmentIndexEntry> { new() });
        MockGrainFactory.GetGrain<IGECAssessmentIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockIdx);

        var vm = new GeriatricsExtendedCareViewModel(GrainService) { PatientId = "P1" };
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.That(vm.Assessments, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task GeriatricsEC_EmptyPatient_DoesNothing()
    {
        var vm = new GeriatricsExtendedCareViewModel(GrainService) { PatientId = "" };
        await vm.LoadCommand.ExecuteAsync(null);
        Assert.That(vm.Assessments, Has.Count.EqualTo(0));
    }

    // ── Incomplete Records ────────────────────────────────────────────────

    [Test]
    public async Task IncompleteRecords_Load_PopulatesDeficiencies()
    {
        var mockIdx = Substitute.For<IIncompleteRecordIndexGrain>();
        mockIdx.GetAllDeficienciesAsync().Returns(new List<IncompleteRecordEntry> { new() });
        MockGrainFactory.GetGrain<IIncompleteRecordIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockIdx);

        var vm = new IncompleteRecordsViewModel(GrainService) { ProviderId = "PROV1" };
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.That(vm.Deficiencies, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task IncompleteRecords_EmptyProvider_SetsError()
    {
        var vm = new IncompleteRecordsViewModel(GrainService) { ProviderId = "" };
        await vm.LoadCommand.ExecuteAsync(null);
        Assert.That(vm.Error, Is.EqualTo("Enter a Provider ID"));
    }

    [Test]
    public async Task IncompleteRecords_Complete_CallsGrain()
    {
        var mockGrain = Substitute.For<IIncompleteRecordGrain>();
        var mockIdx = Substitute.For<IIncompleteRecordIndexGrain>();
        mockIdx.GetAllDeficienciesAsync().Returns(new List<IncompleteRecordEntry>());
        MockGrainFactory.GetGrain<IIncompleteRecordGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockGrain);
        MockGrainFactory.GetGrain<IIncompleteRecordIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockIdx);

        var vm = new IncompleteRecordsViewModel(GrainService) { ProviderId = "PROV1" };
        vm.SelectedDeficiency = new IncompleteRecordEntry { DeficiencyId = "D1" };
        await vm.CompleteCommand.ExecuteAsync(null);

        await mockGrain.Received().CompleteAsync("ADMIN");
    }
}
