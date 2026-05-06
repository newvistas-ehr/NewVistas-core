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
public class BlindRehabilitationPageTests : BlazorTestBase
{
    [Test]
    public void BlindRehabilitation_RendersPageTitle()
    {
        var cut = Ctx.Render<BlindRehabilitation>();

        Assert.That(cut.Markup, Does.Contain("Blind Rehabilitation"));
    }

    [Test]
    public void BlindRehabilitation_RendersLookupBar()
    {
        var cut = Ctx.Render<BlindRehabilitation>();

        var input = cut.Find("input.lookup-input");
        Assert.That(input, Is.Not.Null);
        Assert.That(input.GetAttribute("placeholder"), Is.EqualTo("Patient ID"));
    }

    [Test]
    public void BlindRehabilitation_LoadButton_DisabledWhenEmpty()
    {
        var cut = Ctx.Render<BlindRehabilitation>();

        var button = cut.Find("button.btn-primary");
        Assert.That(button.HasAttribute("disabled"), Is.True);
    }

    [Test]
    public async Task BlindRehabilitation_LoadsDataFromGrain()
    {
        var brState = new BRPatientState
        {
            PatientId = "PAT-001",
            EligibilityStatus = BREligibilityStatus.LegallyBlind,
            RightEyeDistance = "20/200",
            LeftEyeDistance = "20/400",
            PrimaryDiagnosis = "AMD"
        };
        MockWorkflowGrain.GetBRPatientAsync().Returns(brState);

        var cut = Ctx.Render<BlindRehabilitation>();

        cut.Find("input.lookup-input").Input("PAT-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await MockWorkflowGrain.Received(1).GetBRPatientAsync();
        Assert.That(cut.Markup, Does.Contain("20/200"));
        Assert.That(cut.Markup, Does.Contain("AMD"));
    }

    [Test]
    public async Task BlindRehabilitation_ShowsEmptyState()
    {
        var brState = new BRPatientState
        {
            PatientId = "PAT-002",
            EligibilityStatus = BREligibilityStatus.Unknown
        };
        MockWorkflowGrain.GetBRPatientAsync().Returns(brState);

        var cut = Ctx.Render<BlindRehabilitation>();

        cut.Find("input.lookup-input").Input("PAT-002");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("No diagnosis recorded"));
    }

    [Test]
    public async Task BlindRehabilitation_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetBRPatientAsync().Returns<BRPatientState>(
            _ => throw new Exception("Connection timeout"));

        var cut = Ctx.Render<BlindRehabilitation>();

        cut.Find("input.lookup-input").Input("PAT-003");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Connection timeout"));
    }
}
