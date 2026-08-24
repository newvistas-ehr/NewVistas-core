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
public class HomeTelehealthViewModelTests : ViewModelTestBase
{
    private HomeTelehealthViewModel _vm = null!;

    [SetUp]
    public override void Setup()
    {
        base.Setup();
        _vm = new HomeTelehealthViewModel(GrainService, PatientContext);
    }

    [Test]
    public async Task LoadDataAsync_LoadsPatient()
    {
        SelectPatient("PAT-001");
        MockWorkflowGrain.GetHtPatientAsync().Returns(Task.FromResult(new HomeTelehealthPatientState()));
        MockWorkflowGrain.GetHtReadingsAsync(null, null, 50).Returns(Task.FromResult(new List<HtReadingIndexEntry>()));
        MockWorkflowGrain.GetHtAlertsAsync(null).Returns(Task.FromResult(new List<HtAlertIndexEntry>()));

        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.That(_vm.Patient, Is.Not.Null);
    }

    [Test]
    public async Task LoadDataAsync_LoadsReadingsAndAlerts()
    {
        SelectPatient("PAT-001");
        MockWorkflowGrain.GetHtPatientAsync().Returns(Task.FromResult(new HomeTelehealthPatientState()));
        MockWorkflowGrain.GetHtReadingsAsync(null, null, 50)
            .Returns(Task.FromResult(new List<HtReadingIndexEntry> { new(), new() }));
        MockWorkflowGrain.GetHtAlertsAsync(null)
            .Returns(Task.FromResult(new List<HtAlertIndexEntry> { new() }));

        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.That(_vm.Readings, Has.Count.EqualTo(2));
        Assert.That(_vm.Alerts, Has.Count.EqualTo(1));
    }

    [Test]
    public void CannotLoad_WithoutPatient()
    {
        Assert.That(_vm.LoadCommand.CanExecute(null), Is.False);
    }
}
