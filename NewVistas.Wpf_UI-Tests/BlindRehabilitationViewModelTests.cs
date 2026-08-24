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
public class BlindRehabilitationViewModelTests : ViewModelTestBase
{
    private BlindRehabilitationViewModel _vm = null!;

    [SetUp]
    public override void Setup()
    {
        base.Setup();
        _vm = new BlindRehabilitationViewModel(GrainService, PatientContext);
    }

    [Test]
    public async Task LoadDataAsync_LoadsPatientAndAdmissions()
    {
        SelectPatient("PAT-001");
        MockWorkflowGrain.GetBRPatientAsync().Returns(Task.FromResult(new BRPatientState()));
        MockWorkflowGrain.GetBRAdmissionsAsync().Returns(Task.FromResult(new List<BRAdmissionIndexEntry> { new() }));
        MockWorkflowGrain.GetBROutpatientVisitsAsync().Returns(Task.FromResult(new List<BROutpatientVisitIndexEntry>()));

        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.That(_vm.Patient, Is.Not.Null);
        Assert.That(_vm.Admissions, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task LoadDataAsync_LoadsOutpatientVisits()
    {
        SelectPatient("PAT-001");
        MockWorkflowGrain.GetBRPatientAsync().Returns(Task.FromResult(new BRPatientState()));
        MockWorkflowGrain.GetBRAdmissionsAsync().Returns(Task.FromResult(new List<BRAdmissionIndexEntry>()));
        MockWorkflowGrain.GetBROutpatientVisitsAsync().Returns(Task.FromResult(new List<BROutpatientVisitIndexEntry> { new(), new() }));

        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.That(_vm.OutpatientVisits, Has.Count.EqualTo(2));
    }

    [Test]
    public void CannotLoad_WithoutPatient()
    {
        Assert.That(_vm.LoadCommand.CanExecute(null), Is.False);
    }
}
