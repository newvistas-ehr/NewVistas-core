// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.ViewModels;
using NSubstitute;

namespace NewVistas.Wpf_UI_Tests;

[TestFixture]
public class PharmacyPosViewModelTests : ViewModelTestBase
{
    private PharmacyPosViewModel _vm = null!;

    [SetUp]
    public override void Setup()
    {
        base.Setup();
        _vm = new PharmacyPosViewModel(GrainService, ApiClient, PatientContext);
    }

    [Test]
    public async Task LoadDataAsync_LoadsAllClaims()
    {
        SelectPatient("PAT-001");
        MockWorkflowGrain.GetPosClaimsAsync()
            .Returns(Task.FromResult(new List<PosClaimIndexEntry> { new() }));

        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.That(_vm.Claims, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task LoadDataAsync_ShowPaidOnly_FiltersByStatus()
    {
        SelectPatient("PAT-001");
        _vm.ShowPaidOnly = true;
        MockWorkflowGrain.GetPosClaimsByStatusAsync(PosClaimStatus.Paid)
            .Returns(Task.FromResult(new List<PosClaimIndexEntry>()));

        await _vm.LoadCommand.ExecuteAsync(null);

        await MockWorkflowGrain.Received(1).GetPosClaimsByStatusAsync(PosClaimStatus.Paid);
    }

    [Test]
    public void CannotLoad_WithoutPatient()
    {
        Assert.That(_vm.LoadCommand.CanExecute(null), Is.False);
    }
}
