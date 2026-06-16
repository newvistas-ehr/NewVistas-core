// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
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
