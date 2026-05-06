// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
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
    public void Notes_RendersLookupBar()
    {
        var cut = Ctx.Render<Notes>();

        var input = cut.Find("input.lookup-input");
        Assert.That(input, Is.Not.Null);
        Assert.That(input.GetAttribute("placeholder"), Is.EqualTo("Patient ID"));
    }

    [Test]
    public void Notes_LoadButton_DisabledWhenEmpty()
    {
        var cut = Ctx.Render<Notes>();

        var button = cut.Find("button.btn-primary");
        Assert.That(button.HasAttribute("disabled"), Is.True);
    }

    [Test]
    public async Task Notes_LoadsDataFromGrain()
    {
        var notes = new List<TiuNoteSummary>
        {
            new() { DocumentId = "NOTE-001", DocumentType = "PROGRESS NOTE", Subject = "Follow-up visit",
                     AuthorName = "Dr. Smith", Status = "COMPLETED", ReferenceDate = DateTime.UtcNow },
            new() { DocumentId = "NOTE-002", DocumentType = "DISCHARGE SUMMARY", Subject = "Discharge",
                     AuthorName = "Dr. Jones", Status = "UNSIGNED", ReferenceDate = DateTime.UtcNow }
        };
        MockWorkflowGrain.GetNotesAsync(null, 100).Returns(notes);

        var cut = Ctx.Render<Notes>();

        cut.Find("input.lookup-input").Input("PATIENT-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await MockWorkflowGrain.Received(1).GetNotesAsync(null, 100);

        Assert.That(cut.Markup, Does.Contain("Follow-up visit"));
        Assert.That(cut.Markup, Does.Contain("Discharge"));
    }

    [Test]
    public async Task Notes_ShowsEmptyState()
    {
        MockWorkflowGrain.GetNotesAsync(null, 100).Returns(new List<TiuNoteSummary>());

        var cut = Ctx.Render<Notes>();

        cut.Find("input.lookup-input").Input("PATIENT-002");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("No notes found"));
    }

    [Test]
    public async Task Notes_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetNotesAsync(null, 100).Returns<List<TiuNoteSummary>>(
            _ => throw new Exception("Silo unreachable"));

        var cut = Ctx.Render<Notes>();

        cut.Find("input.lookup-input").Input("PATIENT-003");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Error loading notes"));
        Assert.That(cut.Markup, Does.Contain("Silo unreachable"));
    }
}
