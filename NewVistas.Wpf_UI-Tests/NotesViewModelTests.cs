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
public class NotesViewModelTests : ViewModelTestBase
{
    [Test]
    public async Task LoadAsync_PopulatesNotes()
    {
        var testData = new List<TiuNoteSummary>
        {
            new() { DocumentId = "N1", DocumentType = "PROGRESS NOTE", Status = "COMPLETED" },
            new() { DocumentId = "N2", DocumentType = "DISCHARGE SUMMARY", Status = "UNSIGNED" }
        };
        MockWorkflowGrain.GetNotesAsync(null, 50).Returns(testData);
        SelectPatient("PATIENT-001");
        var vm = new NotesViewModel(GrainService, ApiClient, PatientContext);

        await vm.LoadAsync();

        Assert.That(vm.Notes, Has.Count.EqualTo(2));
        Assert.That(vm.IsLoading, Is.False);
        Assert.That(vm.Error, Is.Null);
    }

    [Test]
    public async Task LoadAsync_SetsErrorOnFailure()
    {
        MockWorkflowGrain.GetNotesAsync(null, 50).Throws(new Exception("Grain error"));
        SelectPatient("PATIENT-001");
        var vm = new NotesViewModel(GrainService, ApiClient, PatientContext);

        await vm.LoadAsync();

        Assert.That(vm.Error, Is.Not.Null);
        Assert.That(vm.IsLoading, Is.False);
    }

    [Test]
    public void LoadAsync_RequiresPatient()
    {
        var vm = new NotesViewModel(GrainService, ApiClient, PatientContext);
        Assert.That(vm.LoadCommand.CanExecute(null), Is.False);
    }
}
