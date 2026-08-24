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
public class OncologyViewModelTests : ViewModelTestBase
{
    private OncologyViewModel _vm = null!;

    [SetUp]
    public override void Setup()
    {
        base.Setup();
        _vm = new OncologyViewModel(GrainService, PatientContext);
    }

    [Test]
    public async Task LoadDataAsync_LoadsTumors()
    {
        SelectPatient("PAT-001");
        MockWorkflowGrain.GetOncologyTumorsAsync()
            .Returns(Task.FromResult(new List<OncologyTumorIndexEntry> { new() }));
        MockWorkflowGrain.GetOncologyTreatmentsAsync()
            .Returns(Task.FromResult(new List<OncologyTreatmentIndexEntry>()));

        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.That(_vm.Tumors, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task LoadDataAsync_LoadsTreatments()
    {
        SelectPatient("PAT-001");
        MockWorkflowGrain.GetOncologyTumorsAsync()
            .Returns(Task.FromResult(new List<OncologyTumorIndexEntry>()));
        MockWorkflowGrain.GetOncologyTreatmentsAsync()
            .Returns(Task.FromResult(new List<OncologyTreatmentIndexEntry> { new(), new() }));

        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.That(_vm.Treatments, Has.Count.EqualTo(2));
    }

    [Test]
    public void CannotLoad_WithoutPatient()
    {
        Assert.That(_vm.LoadCommand.CanExecute(null), Is.False);
    }
}
