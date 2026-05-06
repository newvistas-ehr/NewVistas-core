// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Bunit;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class ResearchIRBPageTests : BlazorTestBase
{
    private IResearchStudyIndexGrain _mockStudyIndex = null!;

    public override void Setup()
    {
        base.Setup();
        _mockStudyIndex = Substitute.For<IResearchStudyIndexGrain>();
        MockGrainFactory.GetGrain<IResearchStudyIndexGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(_mockStudyIndex);
        _mockStudyIndex.GetOpenStudiesAsync().Returns(new List<IrbStudyIndexEntry>());
        _mockStudyIndex.GetAllStudiesAsync().Returns(new List<IrbStudyIndexEntry>());
    }

    [Test]
    public void ResearchIRB_RendersPageTitle()
    {
        var cut = Ctx.Render<ResearchIRB>();
        Assert.That(cut.Markup, Does.Contain("Research"));
        Assert.That(cut.Markup, Does.Contain("IRB"));
    }

    [Test]
    public void ResearchIRB_RendersTabBar()
    {
        var cut = Ctx.Render<ResearchIRB>();
        Assert.That(cut.Markup, Does.Contain("Study Registry"));
        Assert.That(cut.Markup, Does.Contain("Subjects"));
        Assert.That(cut.Markup, Does.Contain("Dashboard"));
    }

    [Test]
    public async Task ResearchIRB_ShowsErrorOnGrainFailure()
    {
        _mockStudyIndex.GetOpenStudiesAsync().Returns<List<IrbStudyIndexEntry>>(
            _ => throw new Exception("Study index unavailable"));

        var cut = Ctx.Render<ResearchIRB>();

        Assert.That(cut.Markup, Does.Contain("Error loading studies"));
    }
}
