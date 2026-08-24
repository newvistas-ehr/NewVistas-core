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
public class AuditTrailViewModelTests : ViewModelTestBase
{
    private AuditTrailViewModel _vm = null!;

    [SetUp]
    public override void Setup()
    {
        base.Setup();
        _vm = new AuditTrailViewModel(GrainService, PatientContext);
    }

    [Test]
    public async Task LoadDataAsync_LoadsRecentEvents()
    {
        SelectPatient("PAT-001");
        var events = new List<AuditEventSummary> { new() { EventId = "E1" } };
        MockWorkflowGrain.GetRecentAuditEventsAsync(200).Returns(Task.FromResult(events));

        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.That(_vm.Events, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task LoadDataAsync_WithDomainFilter_CallsFilteredMethod()
    {
        SelectPatient("PAT-001");
        _vm.DomainFilter = "ORDERS";
        MockWorkflowGrain.GetAuditEventsAsync("ORDERS", null, null, 200)
            .Returns(Task.FromResult(new List<AuditEventSummary>()));

        await _vm.LoadCommand.ExecuteAsync(null);

        await MockWorkflowGrain.Received(1).GetAuditEventsAsync("ORDERS", null, null, 200);
    }

    [Test]
    public async Task SelectEvent_LoadsEventDetail()
    {
        SelectPatient("PAT-001");
        var summary = new AuditEventSummary { EventId = "E1" };
        var detail = new AuditEventState { EventId = "E1" };
        MockWorkflowGrain.GetAuditEventAsync("E1").Returns(Task.FromResult(detail));

        await _vm.SelectEventCommand.ExecuteAsync(summary);

        Assert.That(_vm.SelectedEvent, Is.Not.Null);
        Assert.That(_vm.SelectedEvent!.EventId, Is.EqualTo("E1"));
    }
}
