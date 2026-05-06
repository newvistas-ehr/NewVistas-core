// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.ViewModels;
using NSubstitute;

namespace NewVistas.Wpf_UI_Tests;

[TestFixture]
public class BeneficiaryTravelViewModelTests : ViewModelTestBase
{
    private BeneficiaryTravelViewModel _vm = null!;
    private IBeneficiaryTravelIndexGrain _mockIndex = null!;

    [SetUp]
    public override void Setup()
    {
        base.Setup();
        _mockIndex = Substitute.For<IBeneficiaryTravelIndexGrain>();
        MockGrainFactory.GetGrain<IBeneficiaryTravelIndexGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(_mockIndex);
        _vm = new BeneficiaryTravelViewModel(GrainService, ApiClient, PatientContext);
    }

    [Test]
    public async Task LoadDataAsync_LoadsClaims()
    {
        SelectPatient("PAT-001");
        var claims = new List<BeneficiaryTravelClaimEntry> { new() { ClaimId = "C1" } };
        _mockIndex.GetClaimsAsync().Returns(Task.FromResult(claims));

        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.That(_vm.Claims, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task FileClaimAsync_CreatesClaimAndUpdatesIndex()
    {
        SelectPatient("PAT-001");
        var mockClaim = Substitute.For<IBeneficiaryTravelClaimGrain>();
        MockGrainFactory.GetGrain<IBeneficiaryTravelClaimGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(mockClaim);
        _mockIndex.GetClaimsAsync().Returns(Task.FromResult(new List<BeneficiaryTravelClaimEntry>()));

        _vm.Mileage = "50";
        _vm.TransportMode = "POV";
        await _vm.FileClaimCommand.ExecuteAsync(null);

        await mockClaim.Received(1).CreateClaimAsync(
            "PAT-001", Arg.Any<string>(), Arg.Any<DateTime>(),
            "MILEAGE", 50m, true, "POV",
            Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<string>(), false);
    }

    [Test]
    public void CannotLoad_WithoutPatient()
    {
        Assert.That(_vm.LoadCommand.CanExecute(null), Is.False);
    }
}
