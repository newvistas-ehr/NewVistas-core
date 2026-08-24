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
public class SubstanceAbuseTreatmentViewModelTests : ViewModelTestBase
{
    private SubstanceAbuseTreatmentViewModel _vm = null!;

    [SetUp]
    public override void Setup()
    {
        base.Setup();
        _vm = new SubstanceAbuseTreatmentViewModel(GrainService, PatientContext);
    }

    [Test]
    public async Task LoadDataAsync_LoadsEpisodes()
    {
        SelectPatient("PAT-001");
        MockWorkflowGrain.GetSATreatmentEpisodesAsync()
            .Returns(Task.FromResult(new List<SATreatmentEpisodeIndexEntry>
            {
                new() { EpisodeId = "E1", Status = SATreatmentStatus.Discharged }
            }));

        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.That(_vm.Episodes, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task LoadDataAsync_AutoLoadsVisitsForActiveEpisode()
    {
        SelectPatient("PAT-001");
        MockWorkflowGrain.GetSATreatmentEpisodesAsync()
            .Returns(Task.FromResult(new List<SATreatmentEpisodeIndexEntry>
            {
                new() { EpisodeId = "E1", Status = SATreatmentStatus.Active }
            }));
        MockWorkflowGrain.GetSAVisitsAsync("E1")
            .Returns(Task.FromResult(new List<SAVisitIndexEntry> { new() }));

        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.That(_vm.SelectedEpisodeId, Is.EqualTo("E1"));
        Assert.That(_vm.Visits, Has.Count.EqualTo(1));
    }

    [Test]
    public void CannotLoad_WithoutPatient()
    {
        Assert.That(_vm.LoadCommand.CanExecute(null), Is.False);
    }
}
