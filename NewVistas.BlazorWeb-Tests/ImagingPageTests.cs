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
public class ImagingPageTests : BlazorTestBase
{
    [Test]
    public void Imaging_RendersPageTitle()
    {
        var cut = Ctx.Render<Imaging>();
        Assert.That(cut.Markup, Does.Contain("Imaging"));
    }

    [Test]
    public void Imaging_RendersLookupBar()
    {
        var cut = Ctx.Render<Imaging>();
        var input = cut.Find("input.lookup-input");
        Assert.That(input, Is.Not.Null);
        Assert.That(input.GetAttribute("placeholder"), Is.EqualTo("Patient ID"));
    }

    [Test]
    public void Imaging_LoadButton_DisabledWhenEmpty()
    {
        var cut = Ctx.Render<Imaging>();
        var button = cut.Find("button.btn-primary");
        Assert.That(button.HasAttribute("disabled"), Is.True);
    }

    [Test]
    public async Task Imaging_LoadsDataFromGrain()
    {
        var images = new List<ImagingSummary>
        {
            new() { ImageId = "IMG-001", ObjectType = "XRAY",
                     ProcedureDescription = "Chest X-Ray", Status = "VIEWABLE",
                     CaptureDate = DateTime.Today, ImageCount = 2 }
        };
        MockWorkflowGrain.GetImagesAsync(Arg.Any<int>()).Returns(images);

        var cut = Ctx.Render<Imaging>();
        cut.Find("input.lookup-input").Input("PATIENT-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await MockWorkflowGrain.Received(1).GetImagesAsync(Arg.Any<int>());
        Assert.That(cut.Markup, Does.Contain("Chest X-Ray"));
        Assert.That(cut.Markup, Does.Contain("XRAY"));
    }

    [Test]
    public async Task Imaging_ShowsEmptyState()
    {
        MockWorkflowGrain.GetImagesAsync(Arg.Any<int>()).Returns(new List<ImagingSummary>());

        var cut = Ctx.Render<Imaging>();
        cut.Find("input.lookup-input").Input("PATIENT-002");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("No images found"));
    }

    [Test]
    public async Task Imaging_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetImagesAsync(Arg.Any<int>()).Returns<List<ImagingSummary>>(
            _ => throw new Exception("PACS unavailable"));

        var cut = Ctx.Render<Imaging>();
        cut.Find("input.lookup-input").Input("PATIENT-003");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Error"));
        Assert.That(cut.Markup, Does.Contain("PACS unavailable"));
    }
}
