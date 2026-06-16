// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Bunit;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class SubstanceAbuseTreatmentPageTests : BlazorTestBase
{
    [Test]
    public void SubstanceAbuseTreatment_RendersPageTitle()
    {
        var cut = Ctx.Render<SubstanceAbuseTreatment>();
        Assert.That(cut.Markup, Does.Contain("Substance Abuse Treatment"));
    }

    [Test]
    public void SubstanceAbuseTreatment_RendersPatientBar()
    {
        var cut = Ctx.Render<SubstanceAbuseTreatment>();
        var input = cut.Find("input[placeholder='Patient ID']");
        Assert.That(input, Is.Not.Null);
    }

    [Test]
    public void SubstanceAbuseTreatment_RendersTabs()
    {
        var cut = Ctx.Render<SubstanceAbuseTreatment>();
        Assert.That(cut.Markup, Does.Contain("Treatment Episodes"));
        Assert.That(cut.Markup, Does.Contain("Treatment Visits"));
    }
}
