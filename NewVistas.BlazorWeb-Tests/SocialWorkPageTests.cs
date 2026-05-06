// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class SocialWorkPageTests : BlazorTestBase
{
    [Test]
    public void SocialWork_RendersPageTitle()
    {
        var cut = Ctx.Render<SocialWork>();

        Assert.That(cut.Markup, Does.Contain("Social Work"));
    }

    [Test]
    public void SocialWork_RendersLookupBar()
    {
        var cut = Ctx.Render<SocialWork>();

        var input = cut.Find("input.lookup-input");
        Assert.That(input, Is.Not.Null);
        Assert.That(input.GetAttribute("placeholder"), Is.EqualTo("Patient ID"));
    }

    [Test]
    public void SocialWork_LoadButton_DisabledWhenEmpty()
    {
        var cut = Ctx.Render<SocialWork>();

        var button = cut.Find("button.btn-primary");
        Assert.That(button.HasAttribute("disabled"), Is.True);
    }

    [Test]
    public async Task SocialWork_LoadsDataFromGrain()
    {
        var assessments = new List<SocialWorkAssessmentIndexEntry>
        {
            new()
            {
                AssessmentId = "SW-A-001",
                PatientId = "PAT-001",
                AssessmentType = SocialWorkAssessmentType.Psychosocial,
                AssessmentDate = DateTime.Today,
                SocialWorkerName = "Jane Doe",
                RiskLevel = SocialWorkRiskLevel.Moderate,
                Status = SocialWorkAssessmentStatus.Draft,
                HousingStatus = "HOUSED"
            }
        };
        var referrals = new List<SocialWorkReferralIndexEntry>();

        MockWorkflowGrain.GetSocialWorkAssessmentsAsync().Returns(assessments);
        MockWorkflowGrain.GetSocialWorkReferralsAsync().Returns(referrals);

        var cut = Ctx.Render<SocialWork>();

        cut.Find("input.lookup-input").Input("PAT-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await MockWorkflowGrain.Received(1).GetSocialWorkAssessmentsAsync();
        Assert.That(cut.Markup, Does.Contain("Jane Doe"));
        Assert.That(cut.Markup, Does.Contain("Psychosocial"));
    }

    [Test]
    public async Task SocialWork_ShowsEmptyState()
    {
        MockWorkflowGrain.GetSocialWorkAssessmentsAsync().Returns(new List<SocialWorkAssessmentIndexEntry>());
        MockWorkflowGrain.GetSocialWorkReferralsAsync().Returns(new List<SocialWorkReferralIndexEntry>());

        var cut = Ctx.Render<SocialWork>();

        cut.Find("input.lookup-input").Input("PAT-002");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("No assessments on record"));
    }

    [Test]
    public async Task SocialWork_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetSocialWorkAssessmentsAsync().Returns<List<SocialWorkAssessmentIndexEntry>>(
            _ => throw new Exception("Connection failed"));

        var cut = Ctx.Render<SocialWork>();

        cut.Find("input.lookup-input").Input("PAT-003");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Connection failed"));
    }
}
