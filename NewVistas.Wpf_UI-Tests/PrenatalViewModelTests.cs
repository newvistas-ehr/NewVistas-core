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
public class PrenatalViewModelTests : ViewModelTestBase
{
    private PrenatalViewModel _vm = null!;

    [SetUp]
    public override void Setup()
    {
        base.Setup();
        _vm = new PrenatalViewModel(GrainService, PatientContext);
    }

    [Test]
    public async Task LoadDataAsync_LoadsPregnancies()
    {
        SelectPatient("PAT-001");
        MockWorkflowGrain.GetPregnanciesAsync()
            .Returns(Task.FromResult(new List<PregnancyIndexEntry> { new() { PregnancyId = "P1", Status = PregnancyStatus.Delivered } }));

        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.That(_vm.Pregnancies, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task LoadDataAsync_AutoSelectsActivePregnancy()
    {
        SelectPatient("PAT-001");
        MockWorkflowGrain.GetPregnanciesAsync()
            .Returns(Task.FromResult(new List<PregnancyIndexEntry>
            {
                new() { PregnancyId = "P1", Status = PregnancyStatus.Active }
            }));
        MockWorkflowGrain.GetPregnancyAsync("P1").Returns(Task.FromResult(new PregnancyState()));
        MockWorkflowGrain.GetPrenatalVisitsAsync("P1")
            .Returns(Task.FromResult(new List<PrenatalVisitIndexEntry>()));

        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.That(_vm.SelectedPregnancyId, Is.EqualTo("P1"));
    }

    [Test]
    public void CannotLoad_WithoutPatient()
    {
        Assert.That(_vm.LoadCommand.CanExecute(null), Is.False);
    }
}
