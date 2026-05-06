// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Bunit;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class TransplantPageTests : BlazorTestBase
{
    private ITransplantWaitlistIndexGrain _mockWlIndex = null!;
    private ITransplantDonorIndexGrain _mockDonorIndex = null!;

    public override void Setup()
    {
        base.Setup();
        _mockWlIndex = Substitute.For<ITransplantWaitlistIndexGrain>();
        _mockDonorIndex = Substitute.For<ITransplantDonorIndexGrain>();
        MockGrainFactory.GetGrain<ITransplantWaitlistIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(_mockWlIndex);
        MockGrainFactory.GetGrain<ITransplantDonorIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(_mockDonorIndex);
        _mockWlIndex.GetActiveWaitlistAsync().Returns(new List<TransplantWaitlistEntry>());
        _mockDonorIndex.GetAvailableDonorsAsync().Returns(new List<TransplantDonorSummaryEntry>());
    }

    [Test]
    public void Transplant_RendersTitle()
    {
        var cut = Ctx.Render<Transplant>();
        Assert.That(cut.Markup, Does.Contain("Transplant Services"));
    }

    [Test]
    public void Transplant_RendersTabs()
    {
        var cut = Ctx.Render<Transplant>();
        Assert.That(cut.Markup, Does.Contain("Waitlist"));
        Assert.That(cut.Markup, Does.Contain("Donors"));
        Assert.That(cut.Markup, Does.Contain("Register"));
    }

    [Test]
    public async Task Transplant_LoadsWaitlist()
    {
        var patients = new List<TransplantWaitlistEntry>
        {
            new() { PatientId = "P1", PatientName = "Kidney Patient",
                     OrganType = TransplantOrganType.Kidney, Priority = TransplantPriority.Status1A,
                     Status = TransplantStatus.Listed, BloodType = BloodType.OPositive,
                     ListedDate = DateTime.UtcNow.AddDays(-60), PrimaryDiagnosis = "ESRD" }
        };
        _mockWlIndex.GetActiveWaitlistAsync().Returns(patients);

        var cut = Ctx.Render<Transplant>();
        cut.WaitForState(() => cut.Markup.Contains("Kidney Patient"));

        Assert.That(cut.Markup, Does.Contain("Kidney"));
        Assert.That(cut.Markup, Does.Contain("ESRD"));
    }

    [Test]
    public async Task Transplant_ShowsEmptyWaitlist()
    {
        _mockWlIndex.GetActiveWaitlistAsync().Returns(new List<TransplantWaitlistEntry>());

        var cut = Ctx.Render<Transplant>();
        cut.WaitForState(() => cut.Markup.Contains("No patients found"));

        Assert.That(cut.Markup, Does.Contain("No patients found on the waiting list"));
    }
}
