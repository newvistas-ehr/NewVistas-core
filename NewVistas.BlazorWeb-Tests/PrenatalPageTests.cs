// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Bunit;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class PrenatalPageTests : BlazorTestBase
{
    [Test]
    public void Prenatal_RendersPageTitle()
    {
        var cut = Ctx.Render<Prenatal>();
        Assert.That(cut.Markup, Does.Contain("Prenatal"));
    }

    [Test]
    public void Prenatal_RendersPatientBar()
    {
        var cut = Ctx.Render<Prenatal>();
        var input = cut.Find("input[placeholder='Patient ID']");
        Assert.That(input, Is.Not.Null);
    }

    [Test]
    public void Prenatal_RendersTabs()
    {
        var cut = Ctx.Render<Prenatal>();
        Assert.That(cut.Markup, Does.Contain("Pregnancies"));
        Assert.That(cut.Markup, Does.Contain("Prenatal Visits"));
    }
}
