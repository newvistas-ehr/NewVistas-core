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
public class HealthSummaryViewModelTests : ViewModelTestBase
{
    private HealthSummaryViewModel _vm = null!;

    [SetUp]
    public override void Setup()
    {
        base.Setup();
        _vm = new HealthSummaryViewModel(GrainService, PatientContext);
    }

    [Test]
    public async Task LoadDataAsync_LoadsSummaries()
    {
        SelectPatient("PAT-001");
        var list = new List<HealthSummaryIndexEntry> { new() { ReportId = "R1" } };
        MockWorkflowGrain.GetHealthSummaryListAsync().Returns(Task.FromResult(list));

        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.That(_vm.Summaries, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task SelectSummary_LoadsDetail()
    {
        SelectPatient("PAT-001");
        var entry = new HealthSummaryIndexEntry { ReportId = "R1" };
        var detail = new HealthSummaryState { ReportId = "R1" };
        MockWorkflowGrain.GetHealthSummaryAsync("R1").Returns(Task.FromResult(detail));

        await _vm.SelectSummaryCommand.ExecuteAsync(entry);

        Assert.That(_vm.SummaryDetail, Is.Not.Null);
        Assert.That(_vm.SummaryDetail!.ReportId, Is.EqualTo("R1"));
    }

    [Test]
    public void CannotLoad_WithoutPatient()
    {
        Assert.That(_vm.LoadCommand.CanExecute(null), Is.False);
    }
}
