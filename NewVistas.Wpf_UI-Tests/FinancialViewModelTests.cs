// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.ViewModels;
using NSubstitute;

namespace NewVistas.Wpf_UI_Tests;

/// <summary>
/// Tests for financial system-level ViewModels: EDI Billing, Fee Basis, IFCAP,
/// Integrated Billing, DRG Grouper.
/// </summary>
[TestFixture]
public class FinancialViewModelTests : ViewModelTestBase
{
    // ── EDI Billing ───────────────────────────────────────────────────────

    [Test]
    public async Task EdiBilling_LoadClaims_PopulatesClaims()
    {
        var mockClaimIdx = Substitute.For<IEdiClaimIndexGrain>();
        var mockTxIdx = Substitute.For<IEdiTransmissionIndexGrain>();
        var mockEraIdx = Substitute.For<IEraIndexGrain>();
        mockClaimIdx.GetAllAsync().Returns(new List<EdiClaimIndexEntry> { new() });
        mockTxIdx.GetAllAsync().Returns(new List<EdiTransmissionIndexEntry>());
        mockEraIdx.GetAllAsync().Returns(new List<EraIndexEntry>());
        MockGrainFactory.GetGrain<IEdiClaimIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockClaimIdx);
        MockGrainFactory.GetGrain<IEdiTransmissionIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockTxIdx);
        MockGrainFactory.GetGrain<IEraIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockEraIdx);

        var vm = new EdiBillingViewModel(ApiClient, GrainService) { PatientId = "P1" };
        await vm.LoadClaimsCommand.ExecuteAsync(null);

        Assert.That(vm.Claims, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task EdiBilling_LoadClaims_EmptyPatient_DoesNothing()
    {
        var mockTxIdx = Substitute.For<IEdiTransmissionIndexGrain>();
        var mockEraIdx = Substitute.For<IEraIndexGrain>();
        mockTxIdx.GetAllAsync().Returns(new List<EdiTransmissionIndexEntry>());
        mockEraIdx.GetAllAsync().Returns(new List<EraIndexEntry>());
        MockGrainFactory.GetGrain<IEdiTransmissionIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockTxIdx);
        MockGrainFactory.GetGrain<IEraIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockEraIdx);

        var vm = new EdiBillingViewModel(ApiClient, GrainService) { PatientId = "" };
        await vm.LoadClaimsCommand.ExecuteAsync(null);

        Assert.That(vm.Claims, Has.Count.EqualTo(0));
    }

    [Test]
    public async Task EdiBilling_RefreshTransmissions_LoadsAll()
    {
        var mockTxIdx = Substitute.For<IEdiTransmissionIndexGrain>();
        var mockEraIdx = Substitute.For<IEraIndexGrain>();
        mockTxIdx.GetAllAsync().Returns(new List<EdiTransmissionIndexEntry> { new() { Status = "Open" } });
        mockEraIdx.GetAllAsync().Returns(new List<EraIndexEntry> { new() });
        MockGrainFactory.GetGrain<IEdiTransmissionIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockTxIdx);
        MockGrainFactory.GetGrain<IEraIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockEraIdx);

        var vm = new EdiBillingViewModel(ApiClient, GrainService);
        await vm.RefreshTransmissionsCommand.ExecuteAsync(null);

        Assert.That(vm.Transmissions, Has.Count.EqualTo(1));
        Assert.That(vm.Eras, Has.Count.EqualTo(1));
    }

    // ── Fee Basis ─────────────────────────────────────────────────────────

    [Test]
    public async Task FeeBasis_Load_PopulatesPatientAndAuthorizations()
    {
        var mockFeePatient = Substitute.For<IFeePatientGrain>();
        var mockAuthIdx = Substitute.For<IFeeAuthorizationIndexGrain>();
        var mockInvIdx = Substitute.For<IFeeInvoiceIndexGrain>();
        var mockVendorIdx = Substitute.For<IFeeVendorIndexGrain>();
        mockFeePatient.GetAsync().Returns(new FeePatientState { PatientId = "P1" });
        mockAuthIdx.GetAllAsync().Returns(new List<FeeAuthorizationIndexEntry> { new() });
        mockInvIdx.GetAllAsync().Returns(new List<FeeInvoiceIndexEntry>());
        mockVendorIdx.GetAllAsync().Returns(new List<FeeVendorIndexEntry>());
        MockGrainFactory.GetGrain<IFeePatientGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockFeePatient);
        MockGrainFactory.GetGrain<IFeeAuthorizationIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockAuthIdx);
        MockGrainFactory.GetGrain<IFeeInvoiceIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockInvIdx);
        MockGrainFactory.GetGrain<IFeeVendorIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockVendorIdx);

        var vm = new FeeBasisViewModel(ApiClient, GrainService) { PatientId = "P1" };
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.That(vm.FeePatient, Is.Not.Null);
        Assert.That(vm.Authorizations, Has.Count.EqualTo(1));
        Assert.That(vm.Loaded, Is.True);
    }

    [Test]
    public async Task FeeBasis_Load_EmptyPatient_DoesNothing()
    {
        var mockVendorIdx = Substitute.For<IFeeVendorIndexGrain>();
        mockVendorIdx.GetAllAsync().Returns(new List<FeeVendorIndexEntry>());
        MockGrainFactory.GetGrain<IFeeVendorIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockVendorIdx);

        var vm = new FeeBasisViewModel(ApiClient, GrainService) { PatientId = "" };
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.That(vm.Loaded, Is.False);
    }

    // ── IFCAP ─────────────────────────────────────────────────────────────

    [Test]
    public async Task Ifcap_LoadControlPoints_PopulatesByFiscalYear()
    {
        var mockCpIdx = Substitute.For<IControlPointIndexGrain>();
        var mockPoIdx = Substitute.For<IPurchaseOrderIndexGrain>();
        var mockVendorIdx = Substitute.For<IIfcapVendorIndexGrain>();
        mockCpIdx.GetByFiscalYearAsync(Arg.Any<int>()).Returns(new List<ControlPointIndexEntry>
        {
            new("CP1", "Test CP", "FAC1", DateTime.Now.Year, 10000m, ControlPointStatus.Active)
        });
        mockPoIdx.GetAllAsync().Returns(new List<PurchaseOrderIndexEntry>());
        mockVendorIdx.GetAllAsync().Returns(new List<IfcapVendorIndexEntry>());
        MockGrainFactory.GetGrain<IControlPointIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockCpIdx);
        MockGrainFactory.GetGrain<IPurchaseOrderIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockPoIdx);
        MockGrainFactory.GetGrain<IIfcapVendorIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockVendorIdx);

        var vm = new IfcapViewModel(ApiClient, GrainService);
        await vm.LoadControlPointsCommand.ExecuteAsync(null);

        Assert.That(vm.ControlPoints, Has.Count.EqualTo(1));
    }

    // ── Integrated Billing ────────────────────────────────────────────────

    [Test]
    public async Task IntegratedBilling_Load_PopulatesAllData()
    {
        var mockPatient = Substitute.For<IIBillingPatientGrain>();
        var mockActionIdx = Substitute.For<IIBillingActionIndexGrain>();
        var mockPolicyIdx = Substitute.For<IPersonalPolicyIndexGrain>();
        var mockClock = Substitute.For<IMeansTestBillingClockGrain>();
        mockPatient.GetAsync().Returns(new IBillingPatientState());
        mockActionIdx.GetAllAsync().Returns(new List<IBillingActionIndexEntry> { new() });
        mockPolicyIdx.GetAllAsync().Returns(new List<PersonalPolicyIndexEntry>());
        mockClock.GetAsync().Returns(new MeansTestBillingClockState());
        MockGrainFactory.GetGrain<IIBillingPatientGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockPatient);
        MockGrainFactory.GetGrain<IIBillingActionIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockActionIdx);
        MockGrainFactory.GetGrain<IPersonalPolicyIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockPolicyIdx);
        MockGrainFactory.GetGrain<IMeansTestBillingClockGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockClock);

        var vm = new IntegratedBillingViewModel(ApiClient, GrainService) { PatientId = "P1" };
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.That(vm.CopayAccount, Is.Not.Null);
        Assert.That(vm.BillingActions, Has.Count.EqualTo(1));
        Assert.That(vm.Loaded, Is.True);
    }

    // ── DRG Grouper ───────────────────────────────────────────────────────

    [Test]
    public async Task DrgGrouper_LoadAsync_PopulatesAssignments()
    {
        var mockIndex = Substitute.For<IDrgIndexGrain>();
        mockIndex.GetAllAssignmentsAsync().Returns(new List<DrgAssignmentEntry> { new() });
        MockGrainFactory.GetGrain<IDrgIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockIndex);

        var vm = new DrgGrouperViewModel(ApiClient, GrainService);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.That(vm.Assignments, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task DrgGrouper_ReviewAsync_CallsGrain()
    {
        var mockIndex = Substitute.For<IDrgIndexGrain>();
        var mockGrain = Substitute.For<IDrgAssignmentGrain>();
        mockIndex.GetAllAssignmentsAsync().Returns(new List<DrgAssignmentEntry>());
        MockGrainFactory.GetGrain<IDrgIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockIndex);
        MockGrainFactory.GetGrain<IDrgAssignmentGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockGrain);

        var vm = new DrgGrouperViewModel(ApiClient, GrainService);
        vm.SelectedAssignment = new DrgAssignmentEntry { AdmissionId = "ADM1" };
        await vm.ReviewCommand.ExecuteAsync(null);

        await mockGrain.Received().ReviewAsync("ADMIN");
    }
}
