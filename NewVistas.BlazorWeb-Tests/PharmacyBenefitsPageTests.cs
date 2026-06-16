// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Bunit;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class PharmacyBenefitsPageTests : BlazorTestBase
{
    private IPatientBenefitPlanGrain _mockPlan = null!;
    private IPriorAuthIndexGrain _mockPaIndex = null!;

    public override void Setup()
    {
        base.Setup();
        _mockPlan = Substitute.For<IPatientBenefitPlanGrain>();
        _mockPaIndex = Substitute.For<IPriorAuthIndexGrain>();
        MockGrainFactory.GetGrain<IPatientBenefitPlanGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(_mockPlan);
        MockGrainFactory.GetGrain<IPriorAuthIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(_mockPaIndex);
        MockGrainFactory.GetGrain<IFormularyIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(Substitute.For<IFormularyIndexGrain>());
    }

    [Test]
    public void PharmacyBenefits_RendersPageTitle()
    {
        var cut = Ctx.Render<PharmacyBenefits>();
        Assert.That(cut.Markup, Does.Contain("Pharmacy Benefits Management"));
    }

    [Test]
    public async Task PharmacyBenefits_LoadsPatientPlan()
    {
        _mockPlan.GetPlanAsync().Returns(new PatientBenefitPlanState
        {
            PatientId = "P-001", PlanId = "TRICARE", PlanName = "TRICARE Standard",
            InsuranceName = "TRICARE", IsActive = true,
            CopayTier1 = 5, CopayTier2 = 15, CopayTier3 = 40, AnnualDeductible = 100
        });
        _mockPaIndex.GetAllAsync().Returns(new List<PriorAuthIndexEntry>());

        var cut = Ctx.Render<PharmacyBenefits>();
        cut.Find("input.input-id").Change("P-001");
        var loadButton = cut.FindAll("button").First(b => b.TextContent.Contains("Load Patient"));
        await loadButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("TRICARE Standard"));
        Assert.That(cut.Markup, Does.Contain("ACTIVE"));
    }

    [Test]
    public async Task PharmacyBenefits_ShowsErrorOnFailure()
    {
        _mockPlan.GetPlanAsync().Returns<PatientBenefitPlanState>(
            _ => throw new Exception("Plan not found"));

        var cut = Ctx.Render<PharmacyBenefits>();
        cut.Find("input.input-id").Change("P-BAD");
        var loadButton = cut.FindAll("button").First(b => b.TextContent.Contains("Load Patient"));
        await loadButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Plan not found"));
    }
}
