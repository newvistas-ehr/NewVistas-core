// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NewVistas.BlazorWeb.Components.Pages;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class PharmacyHubPageTests : BlazorTestBase
{
    public override void Setup()
    {
        base.Setup();
        // bUnit's BunitContext already provides a NavigationManager
    }

    [Test]
    public void PharmacyHub_RendersPageTitle()
    {
        var cut = Ctx.Render<PharmacyHub>();
        Assert.That(cut.Markup, Does.Contain("Pharmacy Operations Center"));
    }

    [Test]
    public void PharmacyHub_RendersModuleCards()
    {
        var cut = Ctx.Render<PharmacyHub>();
        Assert.That(cut.Markup, Does.Contain("Outpatient Pharmacy"));
        Assert.That(cut.Markup, Does.Contain("Inpatient Medications"));
        Assert.That(cut.Markup, Does.Contain("Drug Accountability"));
    }

    [Test]
    public void PharmacyHub_RendersWorkflowCards()
    {
        var cut = Ctx.Render<PharmacyHub>();
        Assert.That(cut.Markup, Does.Contain("Fill New Prescription"));
        Assert.That(cut.Markup, Does.Contain("Process Refill Request"));
    }
}
