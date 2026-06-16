// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.ViewModels;
using NSubstitute;

namespace NewVistas.Wpf_UI_Tests;

[TestFixture]
public class IVPharmacyViewModelTests : ViewModelTestBase
{
    private IVPharmacyViewModel _vm = null!;

    [SetUp]
    public override void Setup()
    {
        base.Setup();
        _vm = new IVPharmacyViewModel(GrainService, ApiClient, PatientContext);
    }

    [Test]
    public async Task LoadDataAsync_ShowActiveOnly_LoadsActiveOrders()
    {
        SelectPatient("PAT-001");
        var list = new List<IVAdmixOrderIndexEntry> { new() { OrderId = "IV1" } };
        MockWorkflowGrain.GetActiveIVAdmixOrdersAsync().Returns(Task.FromResult(list));

        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.That(_vm.Orders, Has.Count.EqualTo(1));
        await MockWorkflowGrain.Received(1).GetActiveIVAdmixOrdersAsync();
    }

    [Test]
    public async Task LoadDataAsync_ShowAll_LoadsAllOrders()
    {
        SelectPatient("PAT-001");
        _vm.ShowActiveOnly = false;
        MockWorkflowGrain.GetIVAdmixOrdersAsync().Returns(Task.FromResult(new List<IVAdmixOrderIndexEntry>()));

        await _vm.LoadCommand.ExecuteAsync(null);

        await MockWorkflowGrain.Received(1).GetIVAdmixOrdersAsync();
    }

    [Test]
    public void DefaultShowActiveOnly_IsTrue()
    {
        Assert.That(_vm.ShowActiveOnly, Is.True);
    }
}
