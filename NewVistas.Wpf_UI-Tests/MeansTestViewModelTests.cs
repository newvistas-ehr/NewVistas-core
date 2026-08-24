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
public class MeansTestViewModelTests : ViewModelTestBase
{
    private MeansTestViewModel _vm = null!;

    [SetUp]
    public override void Setup()
    {
        base.Setup();
        _vm = new MeansTestViewModel(GrainService, PatientContext);
    }

    [Test]
    public async Task LoadAsync_PopulatesCollection()
    {
        SelectPatient("PAT-001");
        MockWorkflowGrain.GetMeansTestsAsync()
            .Returns(new List<MeansTestSummary> { new() { TestType = "ANNUAL" } });

        await _vm.LoadAsync();

        Assert.That(_vm.MeansTests, Has.Count.EqualTo(1));
        Assert.That(_vm.MeansTests[0].TestType, Is.EqualTo("ANNUAL"));
        Assert.That(_vm.Error, Is.Null);
    }

    [Test]
    public async Task LoadAsync_SetsErrorOnFailure()
    {
        SelectPatient("PAT-001");
        MockWorkflowGrain.GetMeansTestsAsync()
            .ThrowsAsync(new Exception("DB connection lost"));

        await _vm.LoadAsync();

        Assert.That(_vm.Error, Is.EqualTo("DB connection lost"));
    }

    [Test]
    public async Task LoadAsync_RequiresPatient()
    {
        await _vm.LoadAsync();

        Assert.That(_vm.MeansTests, Has.Count.EqualTo(0));
        Assert.That(_vm.Error, Is.Null);
    }
}
