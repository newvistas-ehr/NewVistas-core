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
public class EventCaptureViewModelTests : ViewModelTestBase
{
    private EventCaptureViewModel _vm = null!;

    [SetUp]
    public override void Setup()
    {
        base.Setup();
        _vm = new EventCaptureViewModel(GrainService, ApiClient, PatientContext);
    }

    [Test]
    public async Task LoadDataAsync_LoadsEncounters()
    {
        SelectPatient("PAT-001");
        var list = new List<EventCaptureIndexEntry> { new() { EncounterId = "EC1" } };
        MockWorkflowGrain.GetEventCaptureEncountersAsync(100).Returns(Task.FromResult(list));

        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.That(_vm.Encounters, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task LoadDataAsync_Passes100MaxResults()
    {
        SelectPatient("PAT-001");
        MockWorkflowGrain.GetEventCaptureEncountersAsync(100).Returns(Task.FromResult(new List<EventCaptureIndexEntry>()));

        await _vm.LoadCommand.ExecuteAsync(null);

        await MockWorkflowGrain.Received(1).GetEventCaptureEncountersAsync(100);
    }

    [Test]
    public void CannotLoad_WithoutPatient()
    {
        Assert.That(_vm.LoadCommand.CanExecute(null), Is.False);
    }
}
