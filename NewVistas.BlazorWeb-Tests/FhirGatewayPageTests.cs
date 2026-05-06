// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Bunit;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class FhirGatewayPageTests : BlazorTestBase
{
    [Test]
    public void FhirGateway_RendersPageTitle()
    {
        var cut = Ctx.Render<FhirGateway>();
        Assert.That(cut.Markup, Does.Contain("FHIR R4 Gateway"));
    }

    [Test]
    public void FhirGateway_RendersAboutTab()
    {
        var cut = Ctx.Render<FhirGateway>();
        Assert.That(cut.Markup, Does.Contain("FHIR Browser"));
    }

    [Test]
    public void FhirGateway_RendersResourceSelect()
    {
        var cut = Ctx.Render<FhirGateway>();
        Assert.That(cut.Markup, Does.Contain("Patient"));
        Assert.That(cut.Markup, Does.Contain("Condition"));
    }
}
