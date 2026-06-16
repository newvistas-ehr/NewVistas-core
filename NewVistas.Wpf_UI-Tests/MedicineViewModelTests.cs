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
public class MedicineViewModelTests : ViewModelTestBase
{
    private MedicineViewModel _vm = null!;

    [SetUp]
    public override void Setup()
    {
        base.Setup();
        _vm = new MedicineViewModel(GrainService, ApiClient, PatientContext);
    }

    [Test]
    public async Task LoadDataAsync_LoadsAllProcedures()
    {
        SelectPatient("PAT-001");
        var list = new List<MedProcedureIndexEntry> { new() { ProcedureId = "P1" } };
        MockWorkflowGrain.GetMedProceduresAsync().Returns(Task.FromResult(list));

        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.That(_vm.Procedures, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task LoadDataAsync_ShowCompletedOnly_LoadsCompletedProcedures()
    {
        SelectPatient("PAT-001");
        _vm.ShowCompletedOnly = true;
        MockWorkflowGrain.GetCompletedMedProceduresAsync().Returns(Task.FromResult(new List<MedProcedureIndexEntry>()));

        await _vm.LoadCommand.ExecuteAsync(null);

        await MockWorkflowGrain.Received(1).GetCompletedMedProceduresAsync();
    }

    [Test]
    public void CannotLoad_WithoutPatient()
    {
        Assert.That(_vm.LoadCommand.CanExecute(null), Is.False);
    }
}
