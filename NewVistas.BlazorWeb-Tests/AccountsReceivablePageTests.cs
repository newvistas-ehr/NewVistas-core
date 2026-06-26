// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class AccountsReceivablePageTests : BlazorTestBase
{
    [Test]
    public void AccountsReceivable_RendersPageTitle()
    {
        var cut = Ctx.Render<AccountsReceivable>();
        Assert.That(cut.Markup, Does.Contain("Accounts Receivable"));
    }

    [Test]
    public void AccountsReceivable_RendersPatientLookup()
    {
        var cut = Ctx.Render<AccountsReceivable>();
        var input = cut.Find("input.lookup-input");
        Assert.That(input, Is.Not.Null);
    }

    [Test]
    public async Task AccountsReceivable_ShowsErrorOnGrainFailure()
    {
        var mockDebtor = Substitute.For<IARDebtorGrain>();
        mockDebtor.GetAsync().Returns<ARDebtorState>(_ => throw new Exception("Silo down"));
        MockGrainFactory.GetGrain<IARDebtorGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(mockDebtor);

        var cut = Ctx.Render<AccountsReceivable>();
        cut.Find("input.lookup-input").Change("PAT-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Error loading patient data"));
    }
}
