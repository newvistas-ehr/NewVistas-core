// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class BeneficiaryTravelPageTests : BlazorTestBase
{
    private IBeneficiaryTravelIndexGrain _mockIndex = null!;

    public override void Setup()
    {
        base.Setup();
        _mockIndex = Substitute.For<IBeneficiaryTravelIndexGrain>();
        _mockIndex.GetClaimsAsync().Returns(new List<BeneficiaryTravelClaimEntry>());
        MockGrainFactory.GetGrain<IBeneficiaryTravelIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(_mockIndex);
    }

    [Test]
    public void BeneficiaryTravel_RendersPageTitle()
    {
        var cut = Ctx.Render<BeneficiaryTravel>();
        Assert.That(cut.Markup, Does.Contain("Beneficiary Travel"));
    }

    [Test]
    public void BeneficiaryTravel_RendersTabs()
    {
        var cut = Ctx.Render<BeneficiaryTravel>();
        Assert.That(cut.Markup, Does.Contain("Claims"));
        Assert.That(cut.Markup, Does.Contain("File Claim"));
    }

    [Test]
    public async Task BeneficiaryTravel_ShowsErrorOnGrainFailure()
    {
        _mockIndex.GetClaimsAsync().Returns<List<BeneficiaryTravelClaimEntry>>(
            _ => throw new Exception("Travel index error"));

        var cut = Ctx.Render<BeneficiaryTravel>();
        // OnInitializedAsync calls LoadClaims
        cut.WaitForState(() => cut.Markup.Contains("Travel index error"), TimeSpan.FromSeconds(2));

        Assert.That(cut.Markup, Does.Contain("Travel index error"));
    }
}
