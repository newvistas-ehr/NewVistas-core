// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Bunit;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class NotesPageTests : BlazorTestBase
{
    [Test]
    public void Notes_RendersPageTitle()
    {
        var cut = Ctx.Render<Notes>();

        Assert.That(cut.Markup, Does.Contain("Progress Notes"));
    }

    [Test]
    public void Notes_PromptsToSelectPatientWhenNoneChosen()
    {
        var cut = Ctx.Render<Notes>();

        Assert.That(cut.Markup, Does.Contain("Select a patient"));
    }

    [Test]
    public void Notes_ShowsSelectedPatientInBar()
    {
        SelectPatient("PATIENT-001", "SMITH, JOHN");

        var cut = Ctx.Render<Notes>();

        Assert.That(cut.Markup, Does.Contain("Change patient"));
    }

    [Test]
    public void Notes_LoadsDataFromGrain()
    {
        var notes = new List<TiuNoteSummary>
        {
            new() { DocumentId = "NOTE-001", DocumentType = "PROGRESS NOTE", Subject = "Follow-up visit",
                     AuthorName = "Dr. Smith", Status = "COMPLETED", ReferenceDate = DateTime.UtcNow },
            new() { DocumentId = "NOTE-002", DocumentType = "DISCHARGE SUMMARY", Subject = "Discharge",
                     AuthorName = "Dr. Jones", Status = "UNSIGNED", ReferenceDate = DateTime.UtcNow }
        };
        MockWorkflowGrain.GetNotesAsync(null, 100).Returns(notes);

        SelectPatient("PATIENT-001");
        var cut = Ctx.Render<Notes>();

        Assert.That(cut.Markup, Does.Contain("Follow-up visit"));
        Assert.That(cut.Markup, Does.Contain("Discharge"));
    }

    [Test]
    public void Notes_ShowsEmptyState()
    {
        MockWorkflowGrain.GetNotesAsync(null, 100).Returns(new List<TiuNoteSummary>());

        SelectPatient("PATIENT-002");
        var cut = Ctx.Render<Notes>();

        Assert.That(cut.Markup, Does.Contain("No notes found"));
    }

    [Test]
    public void Notes_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetNotesAsync(null, 100).Returns<List<TiuNoteSummary>>(
            _ => throw new Exception("Silo unreachable"));

        SelectPatient("PATIENT-003");
        var cut = Ctx.Render<Notes>();

        Assert.That(cut.Markup, Does.Contain("Error loading notes"));
        Assert.That(cut.Markup, Does.Contain("Silo unreachable"));
    }
}
