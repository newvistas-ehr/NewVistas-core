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
/// Tests for system-level ViewModels that have simple load patterns (single index grain call).
/// </summary>
[TestFixture]
public class SystemViewModelTests : ViewModelTestBase
{
    // ── Clinical Case Registries ──────────────────────────────────────────

    [Test]
    public async Task ClinicalCaseRegistries_LoadAsync_PopulatesEntries()
    {
        var mockIndex = Substitute.For<IClinicalRegistrySiteIndexGrain>();
        mockIndex.GetRecentEnrollmentsAsync(50).Returns(new List<CCREntrySummary>
        {
            new() { PatientId = "P1" }
        });
        MockGrainFactory.GetGrain<IClinicalRegistrySiteIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockIndex);

        var vm = new ClinicalCaseRegistriesViewModel(ApiClient, GrainService);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.That(vm.Entries, Has.Count.EqualTo(1));
    }

    // ── Controlled Substances ─────────────────────────────────────────────

    [Test]
    public async Task ControlledSubstances_LoadAsync_PopulatesInspectionsAndDispenses()
    {
        var mockInsp = Substitute.For<ICSInspectionLogGrain>();
        var mockDisp = Substitute.For<ICSDispenseLogGrain>();
        mockInsp.GetAllInspectionsAsync().Returns(new List<CSInspectionSummaryEntry> { new() });
        mockDisp.GetAllRecordsAsync().Returns(new List<CSDispenseSummaryEntry> { new(), new() });
        MockGrainFactory.GetGrain<ICSInspectionLogGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockInsp);
        MockGrainFactory.GetGrain<ICSDispenseLogGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockDisp);

        var vm = new ControlledSubstancesViewModel(ApiClient, GrainService);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.That(vm.Inspections, Has.Count.EqualTo(1));
        Assert.That(vm.DispenseRecords, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task ControlledSubstances_EmptyLocation_DoesNotLoad()
    {
        var vm = new ControlledSubstancesViewModel(ApiClient, GrainService) { LocationId = "" };
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.That(vm.Inspections, Has.Count.EqualTo(0));
    }

    // ── Engineering ───────────────────────────────────────────────────────

    [Test]
    public async Task Engineering_LoadAsync_PopulatesWorkOrders()
    {
        var mockIndex = Substitute.For<IEngineeringWorkOrderIndexGrain>();
        mockIndex.GetActiveAsync(200).Returns(new List<WorkOrderIndexEntry>
        {
            new() { WorkOrderId = "WO1" }
        });
        MockGrainFactory.GetGrain<IEngineeringWorkOrderIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockIndex);

        var vm = new EngineeringViewModel(ApiClient, GrainService);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.That(vm.WorkOrders, Has.Count.EqualTo(1));
    }

    // ── GPRA Reporting ────────────────────────────────────────────────────

    [Test]
    public async Task GpraReporting_LoadAsync_PopulatesReports()
    {
        var mockIndex = Substitute.For<IGpraReportIndexGrain>();
        mockIndex.GetAllAsync().Returns(new List<GpraReportIndexEntry> { new() });
        MockGrainFactory.GetGrain<IGpraReportIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockIndex);

        var vm = new GpraReportingViewModel(ApiClient, GrainService);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.That(vm.Reports, Has.Count.EqualTo(1));
    }

    // ── Infection Control ─────────────────────────────────────────────────

    [Test]
    public async Task InfectionControl_LoadAsync_PopulatesCasesAndOutbreaks()
    {
        var mockCaseIdx = Substitute.For<IHAICaseIndexGrain>();
        var mockOutbreakIdx = Substitute.For<IOutbreakIndexGrain>();
        mockCaseIdx.GetAllCasesAsync().Returns(new List<HAICaseSummary> { new() });
        mockOutbreakIdx.GetAllOutbreaksAsync().Returns(new List<OutbreakSummary> { new() });
        MockGrainFactory.GetGrain<IHAICaseIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockCaseIdx);
        MockGrainFactory.GetGrain<IOutbreakIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockOutbreakIdx);

        var vm = new InfectionControlViewModel(ApiClient, GrainService);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.That(vm.Cases, Has.Count.EqualTo(1));
        Assert.That(vm.Outbreaks, Has.Count.EqualTo(1));
    }

    // ── PCC Surveillance ──────────────────────────────────────────────────

    [Test]
    public async Task PccSurveillance_LoadAsync_PopulatesMatches()
    {
        var mockIndex = Substitute.For<IPccSurveillanceMatchIndexGrain>();
        mockIndex.GetAllAsync().Returns(new List<PccSurveillanceMatchIndexEntry> { new() });
        MockGrainFactory.GetGrain<IPccSurveillanceMatchIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockIndex);

        var vm = new PccSurveillanceViewModel(ApiClient, GrainService);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.That(vm.Matches, Has.Count.EqualTo(1));
    }

    // ── Polytrauma/TBI ────────────────────────────────────────────────────

    [Test]
    public async Task PolytraumaTBI_LoadAsync_PopulatesPatients()
    {
        var mockIndex = Substitute.For<IPolytraumaRegistryIndexGrain>();
        mockIndex.GetActivePatientAsync().Returns(new List<PolytraumaRegistrySummaryEntry> { new() });
        MockGrainFactory.GetGrain<IPolytraumaRegistryIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockIndex);

        var vm = new PolytraumaTBIViewModel(ApiClient, GrainService);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.That(vm.Patients, Has.Count.EqualTo(1));
    }

    // ── Quality Management ────────────────────────────────────────────────

    [Test]
    public async Task QualityManagement_LoadAsync_PopulatesIncidents()
    {
        var mockIndex = Substitute.For<IQMIncidentIndexGrain>();
        mockIndex.GetAllIncidentsAsync().Returns(new List<QMIncidentIndexEntry> { new() });
        MockGrainFactory.GetGrain<IQMIncidentIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockIndex);

        var vm = new QualityManagementViewModel(ApiClient, GrainService);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.That(vm.Incidents, Has.Count.EqualTo(1));
    }

    // ── Release of Information ────────────────────────────────────────────

    [Test]
    public async Task ReleaseOfInformation_LoadAsync_PopulatesRequests()
    {
        var mockIndex = Substitute.For<IROIRequestIndexGrain>();
        mockIndex.GetAllRequestsAsync().Returns(new List<ROIRequestIndexEntry> { new() });
        MockGrainFactory.GetGrain<IROIRequestIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockIndex);

        var vm = new ReleaseOfInformationViewModel(ApiClient, GrainService);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.That(vm.Requests, Has.Count.EqualTo(1));
    }

    // ── Research / IRB ────────────────────────────────────────────────────

    [Test]
    public async Task ResearchIRB_LoadAsync_PopulatesStudies()
    {
        var mockIndex = Substitute.For<IResearchStudyIndexGrain>();
        mockIndex.GetAllStudiesAsync().Returns(new List<IrbStudyIndexEntry> { new() });
        MockGrainFactory.GetGrain<IResearchStudyIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockIndex);

        var vm = new ResearchIRBViewModel(ApiClient, GrainService);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.That(vm.Studies, Has.Count.EqualTo(1));
    }

    // ── Suicide Prevention ────────────────────────────────────────────────

    [Test]
    public async Task SuicidePrevention_LoadAsync_HighRiskOnly()
    {
        var mockIndex = Substitute.For<ISuicidePreventionIndexGrain>();
        mockIndex.GetHighRiskPatientsAsync().Returns(new List<PatientHighRiskSummary> { new() });
        MockGrainFactory.GetGrain<ISuicidePreventionIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockIndex);

        var vm = new SuicidePreventionViewModel(ApiClient, GrainService) { ShowHighRiskOnly = true };
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.That(vm.Patients, Has.Count.EqualTo(1));
        await mockIndex.Received().GetHighRiskPatientsAsync();
    }

    [Test]
    public async Task SuicidePrevention_LoadAsync_AllPatients()
    {
        var mockIndex = Substitute.For<ISuicidePreventionIndexGrain>();
        mockIndex.GetAllPatientsAsync().Returns(new List<PatientHighRiskSummary> { new(), new() });
        MockGrainFactory.GetGrain<ISuicidePreventionIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockIndex);

        var vm = new SuicidePreventionViewModel(ApiClient, GrainService) { ShowHighRiskOnly = false };
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.That(vm.Patients, Has.Count.EqualTo(2));
        await mockIndex.Received().GetAllPatientsAsync();
    }

    // ── Transplant ────────────────────────────────────────────────────────

    [Test]
    public async Task Transplant_LoadAsync_PopulatesWaitlistAndDonors()
    {
        var mockWaitlist = Substitute.For<ITransplantWaitlistIndexGrain>();
        var mockDonor = Substitute.For<ITransplantDonorIndexGrain>();
        mockWaitlist.GetActiveWaitlistAsync().Returns(new List<TransplantWaitlistEntry> { new() });
        mockDonor.GetAvailableDonorsAsync().Returns(new List<TransplantDonorSummaryEntry> { new(), new() });
        MockGrainFactory.GetGrain<ITransplantWaitlistIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockWaitlist);
        MockGrainFactory.GetGrain<ITransplantDonorIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockDonor);

        var vm = new TransplantViewModel(ApiClient, GrainService);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.That(vm.Waitlist, Has.Count.EqualTo(1));
        Assert.That(vm.Donors, Has.Count.EqualTo(2));
    }

    // ── Voluntary Service ─────────────────────────────────────────────────

    [Test]
    public async Task VoluntaryService_LoadAsync_PopulatesVolunteers()
    {
        var mockIndex = Substitute.For<IVolunteerIndexGrain>();
        mockIndex.GetAllAsync().Returns(new List<VolunteerIndexEntry> { new() });
        MockGrainFactory.GetGrain<IVolunteerIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockIndex);

        var vm = new VoluntaryServiceViewModel(ApiClient, GrainService);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.That(vm.Volunteers, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task VoluntaryService_LoadAsync_WithSearch()
    {
        var mockIndex = Substitute.For<IVolunteerIndexGrain>();
        mockIndex.SearchAsync("Smith").Returns(new List<VolunteerIndexEntry> { new() });
        MockGrainFactory.GetGrain<IVolunteerIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockIndex);

        var vm = new VoluntaryServiceViewModel(ApiClient, GrainService) { SearchText = "Smith" };
        await vm.LoadCommand.ExecuteAsync(null);

        await mockIndex.Received().SearchAsync("Smith");
    }

    // ── Placeholder ViewModels ────────────────────────────────────────────

    [Test]
    public void PatientAdvocate_CanConstruct()
    {
        var vm = new PatientAdvocateViewModel(ApiClient, GrainService);
        Assert.That(vm, Is.Not.Null);
    }

    [Test]
    public void RecordTracking_CanConstruct()
    {
        var vm = new RecordTrackingViewModel(ApiClient, GrainService);
        Assert.That(vm, Is.Not.Null);
    }
}
