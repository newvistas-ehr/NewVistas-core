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
public class BloodBankViewModelTests : ViewModelTestBase
{
    private BloodBankViewModel _vm = null!;

    [SetUp]
    public override void Setup()
    {
        base.Setup();
        _vm = new BloodBankViewModel(GrainService, PatientContext);
    }

    [Test]
    public async Task LoadDataAsync_LoadsPatient()
    {
        SelectPatient("PAT-001");
        MockWorkflowGrain.GetBloodBankPatientAsync().Returns(Task.FromResult(new BloodBankPatientState()));
        MockWorkflowGrain.GetCrossmatchesAsync().Returns(Task.FromResult(new List<CrossmatchIndexEntry>()));
        MockWorkflowGrain.GetTransfusionHistoryAsync().Returns(Task.FromResult(new List<TransfusionIndexEntry>()));

        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.That(_vm.Patient, Is.Not.Null);
    }

    [Test]
    public async Task LoadDataAsync_LoadsCrossmatchesAndTransfusions()
    {
        SelectPatient("PAT-001");
        MockWorkflowGrain.GetBloodBankPatientAsync().Returns(Task.FromResult(new BloodBankPatientState()));
        MockWorkflowGrain.GetCrossmatchesAsync().Returns(Task.FromResult(new List<CrossmatchIndexEntry> { new() }));
        MockWorkflowGrain.GetTransfusionHistoryAsync().Returns(Task.FromResult(new List<TransfusionIndexEntry> { new(), new() }));

        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.That(_vm.Crossmatches, Has.Count.EqualTo(1));
        Assert.That(_vm.Transfusions, Has.Count.EqualTo(2));
    }

    [Test]
    public void CannotLoad_WithoutPatient()
    {
        Assert.That(_vm.LoadCommand.CanExecute(null), Is.False);
    }
}
