// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.ViewModels;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace NewVistas.Wpf_UI_Tests;

[TestFixture]
public class PceViewModelTests : ViewModelTestBase
{
    private PceViewModel _vm = null!;

    [SetUp]
    public override void Setup()
    {
        base.Setup();
        _vm = new PceViewModel(GrainService, ApiClient, PatientContext);
    }

    [Test]
    public async Task LoadAsync_PopulatesCollection()
    {
        SelectPatient("PAT-001");
        MockWorkflowGrain.GetEncounterListAsync(50)
            .Returns(new List<PceVisitEntry> { new() { ServiceCategory = "AMBULATORY" } });

        await _vm.LoadAsync();

        Assert.That(_vm.Encounters, Has.Count.EqualTo(1));
        Assert.That(_vm.Error, Is.Null);
    }

    [Test]
    public async Task LoadAsync_SetsErrorOnFailure()
    {
        SelectPatient("PAT-001");
        MockWorkflowGrain.GetEncounterListAsync(50)
            .ThrowsAsync(new Exception("Timeout"));

        await _vm.LoadAsync();

        Assert.That(_vm.Error, Is.EqualTo("Timeout"));
    }

    [Test]
    public async Task LoadAsync_RequiresPatient()
    {
        await _vm.LoadAsync();

        Assert.That(_vm.Encounters, Has.Count.EqualTo(0));
        Assert.That(_vm.Error, Is.Null);
    }
}
