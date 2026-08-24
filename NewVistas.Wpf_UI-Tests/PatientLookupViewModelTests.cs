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
public class PatientLookupViewModelTests : ViewModelTestBase
{
    private PatientLookupViewModel _vm = null!;

    [SetUp]
    public override void Setup()
    {
        base.Setup();
        _vm = new PatientLookupViewModel(GrainService, PatientContext, AuthService);
    }

    [Test]
    public async Task LoadAsync_PopulatesCollection()
    {
        MockWorkflowGrain.GetPatientAsync()
            .Returns(new PatientState { Name = "Smith, Jane", Sex = "F" });

        await _vm.SelectPatientCommand.ExecuteAsync(new PatientListItem { PatientId = "PAT-001" });

        Assert.That(_vm.Patient, Is.Not.Null);
        Assert.That(_vm.Patient!.Name, Is.EqualTo("Smith, Jane"));
        Assert.That(_vm.Error, Is.Null);
    }

    [Test]
    public async Task LoadAsync_SetsErrorOnFailure()
    {
        MockWorkflowGrain.GetPatientAsync()
            .ThrowsAsync(new Exception("Patient not found"));

        await _vm.SelectPatientCommand.ExecuteAsync(new PatientListItem { PatientId = "PAT-001" });

        Assert.That(_vm.Error, Is.EqualTo("Patient not found"));
    }

    [Test]
    public async Task LoadAsync_RequiresPatient()
    {
        await _vm.SelectPatientCommand.ExecuteAsync(null);

        Assert.That(_vm.Patient, Is.Null);
        Assert.That(_vm.Error, Is.Null);
    }
}
