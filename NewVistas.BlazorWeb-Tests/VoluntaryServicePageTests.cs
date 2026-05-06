// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Bunit;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class VoluntaryServicePageTests : BlazorTestBase
{
    private IVolunteerIndexGrain _mockVolIndex = null!;

    public override void Setup()
    {
        base.Setup();
        _mockVolIndex = Substitute.For<IVolunteerIndexGrain>();
        MockGrainFactory.GetGrain<IVolunteerIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(_mockVolIndex);
        _mockVolIndex.GetAllAsync().Returns(new List<VolunteerIndexEntry>());
    }

    [Test]
    public void VoluntaryService_RendersTitle()
    {
        var cut = Ctx.Render<VoluntaryService>();
        Assert.That(cut.Markup, Does.Contain("Voluntary Service"));
    }

    [Test]
    public void VoluntaryService_RendersTabs()
    {
        var cut = Ctx.Render<VoluntaryService>();
        Assert.That(cut.Markup, Does.Contain("Volunteers"));
        Assert.That(cut.Markup, Does.Contain("Hours Tracking"));
        Assert.That(cut.Markup, Does.Contain("Recognition"));
        Assert.That(cut.Markup, Does.Contain("Enroll Volunteer"));
    }

    [Test]
    public async Task VoluntaryService_LoadsVolunteers()
    {
        var volunteers = new List<VolunteerIndexEntry>
        {
            new() { VolunteerId = "V1", FirstName = "Jane", LastName = "Volunteer",
                     Status = VolunteerStatus.Active, TotalHours = 150.5m,
                     EnrollmentDate = DateTime.UtcNow.AddYears(-1) }
        };
        _mockVolIndex.GetAllAsync().Returns(volunteers);

        var cut = Ctx.Render<VoluntaryService>();
        cut.WaitForState(() => cut.Markup.Contains("Volunteer, Jane"));

        Assert.That(cut.Markup, Does.Contain("150.5"));
    }

    [Test]
    public async Task VoluntaryService_ShowsEmptyState()
    {
        _mockVolIndex.GetAllAsync().Returns(new List<VolunteerIndexEntry>());

        var cut = Ctx.Render<VoluntaryService>();
        cut.WaitForState(() => cut.Markup.Contains("No volunteers found"));

        Assert.That(cut.Markup, Does.Contain("No volunteers found"));
    }
}
