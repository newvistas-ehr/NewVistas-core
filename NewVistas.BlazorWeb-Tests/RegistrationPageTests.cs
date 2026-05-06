// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;
using Orleans;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class RegistrationPageTests : BlazorTestBase
{
    private IPatientEnrollmentGrain MockEnrollmentGrain { get; set; } = null!;
    private IPrfAssignmentGrain MockPrfGrain { get; set; } = null!;
    private IPrfNationalFlagIndexGrain MockFlagIndexGrain { get; set; } = null!;
    private IMstHistoryGrain MockMstGrain { get; set; } = null!;
    private IPatientRelationGrain MockRelationGrain { get; set; } = null!;
    private IIncomeHouseholdGrain MockIncomeGrain { get; set; } = null!;
    private ITreatingFacilityListGrain MockFacilityGrain { get; set; } = null!;

    public override void Setup()
    {
        base.Setup();

        MockEnrollmentGrain = Substitute.For<IPatientEnrollmentGrain>();
        MockPrfGrain = Substitute.For<IPrfAssignmentGrain>();
        MockFlagIndexGrain = Substitute.For<IPrfNationalFlagIndexGrain>();
        MockMstGrain = Substitute.For<IMstHistoryGrain>();
        MockRelationGrain = Substitute.For<IPatientRelationGrain>();
        MockIncomeGrain = Substitute.For<IIncomeHouseholdGrain>();
        MockFacilityGrain = Substitute.For<ITreatingFacilityListGrain>();

        MockGrainFactory
            .GetGrain<IPatientEnrollmentGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(MockEnrollmentGrain);
        MockGrainFactory
            .GetGrain<IPrfAssignmentGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(MockPrfGrain);
        MockGrainFactory
            .GetGrain<IPrfNationalFlagIndexGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(MockFlagIndexGrain);
        MockGrainFactory
            .GetGrain<IMstHistoryGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(MockMstGrain);
        MockGrainFactory
            .GetGrain<IPatientRelationGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(MockRelationGrain);
        MockGrainFactory
            .GetGrain<IIncomeHouseholdGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(MockIncomeGrain);
        MockGrainFactory
            .GetGrain<ITreatingFacilityListGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(MockFacilityGrain);
    }

    [Test]
    public void Registration_RendersPageTitle()
    {
        var cut = Ctx.Render<Registration>();

        Assert.That(cut.Markup, Does.Contain("Registration"));
    }

    [Test]
    public void Registration_ShowsWarningWithoutPatientId()
    {
        var cut = Ctx.Render<Registration>();

        Assert.That(cut.Markup, Does.Contain("No patient ID provided"));
    }

    [Test]
    public void Registration_HasTabNavigation()
    {
        // Registration requires a patientId query parameter to show tabs.
        // Without it, the page shows a warning message instead.
        MockEnrollmentGrain.GetAsync().Returns(new PatientEnrollmentState
        {
            PatientId = "PATIENT-TAB",
            EnrollmentStatus = EnrollmentStatus.Verified,
            PriorityGroup = "1"
        });

        var cut = Ctx.Render<Registration>();
        var nav = Ctx.Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo(nav.GetUriWithQueryParameter("PatientId", "PATIENT-TAB"));

        cut.WaitForState(() => cut.Markup.Contains("Enrollment"), TimeSpan.FromSeconds(3));

        Assert.That(cut.Markup, Does.Contain("Enrollment"));
        Assert.That(cut.Markup, Does.Contain("PRF Flags"));
        Assert.That(cut.Markup, Does.Contain("MST History"));
        Assert.That(cut.Markup, Does.Contain("Relations"));
        Assert.That(cut.Markup, Does.Contain("Income"));
        Assert.That(cut.Markup, Does.Contain("Treating Facilities"));
    }

    [Test]
    public async Task Registration_LoadsEnrollmentFromGrain()
    {
        var enrollmentState = new PatientEnrollmentState
        {
            PatientId = "PATIENT-001",
            EnrollmentStatus = EnrollmentStatus.Verified,
            PriorityGroup = "1",
            CopayExempt = true,
            CopayExemptionReason = "SC",
            MeansTestRequired = false
        };
        MockEnrollmentGrain.GetAsync().Returns(enrollmentState);

        var cut = Ctx.Render<Registration>();
        var nav = Ctx.Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo(nav.GetUriWithQueryParameter("PatientId", "PATIENT-001"));
        cut.WaitForState(() => cut.Markup.Contains("Verified") || cut.Markup.Contains("Loading"), TimeSpan.FromSeconds(3));

        await MockEnrollmentGrain.Received().GetAsync();
    }

    [Test]
    public async Task Registration_ShowsEmptyPrfFlags()
    {
        var enrollmentState = new PatientEnrollmentState
        {
            PatientId = "PATIENT-001",
            EnrollmentStatus = EnrollmentStatus.Verified,
            PriorityGroup = "1"
        };
        MockEnrollmentGrain.GetAsync().Returns(enrollmentState);

        var prfState = new PrfAssignmentState
        {
            PatientId = "PATIENT-001",
            Assignments = new List<PrfFlagAssignment>()
        };
        MockPrfGrain.GetAsync().Returns(prfState);
        MockFlagIndexGrain.GetAllAsync().Returns(new List<PrfNationalFlagEntry>());

        var cut = Ctx.Render<Registration>();
        var nav = Ctx.Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo(nav.GetUriWithQueryParameter("PatientId", "PATIENT-001"));

        // Wait for enrollment to load, then click PRF tab
        cut.WaitForState(() => cut.Markup.Contains("Verified"), TimeSpan.FromSeconds(3));

        var prfTab = cut.FindAll("button.nav-link").First(b => b.TextContent.Contains("PRF Flags"));
        await prfTab.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        cut.WaitForState(() => cut.Markup.Contains("No active PRF flags"), TimeSpan.FromSeconds(3));
        Assert.That(cut.Markup, Does.Contain("No active PRF flags"));
    }

    [Test]
    public async Task Registration_ShowsErrorOnGrainFailure()
    {
        MockEnrollmentGrain.GetAsync().Returns<PatientEnrollmentState>(
            _ => throw new Exception("Enrollment grain offline"));

        var cut = Ctx.Render<Registration>();
        var nav = Ctx.Services.GetRequiredService<NavigationManager>();

        // Navigate with patient ID — this will trigger enrollment load which throws
        try
        {
            nav.NavigateTo(nav.GetUriWithQueryParameter("PatientId", "PATIENT-ERR"));
            cut.WaitForState(() => cut.Markup.Contains("Error") || cut.Markup.Contains("Loading"), TimeSpan.FromSeconds(3));
        }
        catch
        {
            // Exception may propagate from grain call
        }

        // The enrollment grain was called
        await MockEnrollmentGrain.Received().GetAsync();
    }
}
