// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Bunit;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class PatientPortalPageTests : BlazorTestBase
{
    [Test]
    public void PatientPortal_RendersPageTitle()
    {
        var cut = Ctx.Render<PatientPortal>();
        Assert.That(cut.Markup, Does.Contain("Patient Portal"));
    }

    [Test]
    public void PatientPortal_RendersLookupBar()
    {
        var cut = Ctx.Render<PatientPortal>();
        var input = cut.Find("input.lookup-input");
        Assert.That(input, Is.Not.Null);
    }

    [Test]
    public void PatientPortal_LoadButtonDisabledWhenEmpty()
    {
        var cut = Ctx.Render<PatientPortal>();
        var button = cut.Find("button.btn-primary");
        Assert.That(button.HasAttribute("disabled"), Is.True);
    }
}
