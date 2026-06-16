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
public class CoverSheetPageTests : BlazorTestBase
{
    [Test]
    public void CoverSheet_RendersPageTitle()
    {
        var cut = Ctx.Render<CoverSheet>();

        Assert.That(cut.Markup, Does.Contain("Cover Sheet"));
    }

    [Test]
    public void CoverSheet_RendersLookupBar()
    {
        var cut = Ctx.Render<CoverSheet>();

        var input = cut.Find("input.lookup-input");
        Assert.That(input, Is.Not.Null);
        Assert.That(input.GetAttribute("placeholder"), Is.EqualTo("Patient ID"));
    }

    [Test]
    public void CoverSheet_LoadButton_DisabledWhenEmpty()
    {
        var cut = Ctx.Render<CoverSheet>();

        var button = cut.Find("button.btn-primary");
        Assert.That(button.HasAttribute("disabled"), Is.True);
    }

    [Test]
    public async Task CoverSheet_LoadsPatientDataFromGrain()
    {
        var state = new CoverSheetState
        {
            PatientId = "PATIENT-001",
            Demographics = new PatientDemographicsSummary
            {
                Name = "SMITH, JOHN", Sex = "M", Age = 65,
                IsServiceConnected = true, ServiceConnectedPercent = 70,
                IsAdmitted = false
            },
            Cwad = new CwadFlags { HasAllergies = true, HasWarnings = true },
            ActiveProblems =
            [
                new() { Diagnosis = "Type 2 Diabetes", DiagnosisCode = "E11.9", Status = "ACTIVE",
                         DateOfOnset = new DateTime(2020, 1, 15) }
            ],
            Allergies =
            [
                new() { Allergen = "Penicillin", Severity = "Severe", Reactions = ["Rash"] }
            ],
            ActiveMedications =
            [
                new() { DrugName = "Metformin 500mg", Sig = "PO BID", Status = "ACTIVE" }
            ],
            RecentVitals =
            [
                new() { VitalType = "Blood Pressure", Value = "130/85", Units = "mmHg",
                         DateTimeTaken = new DateTime(2026, 3, 20, 10, 0, 0) }
            ]
        };
        MockWorkflowGrain.GetCoverSheetAsync().Returns(state);

        var cut = Ctx.Render<CoverSheet>();

        cut.Find("input.lookup-input").Input("PATIENT-001");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await MockWorkflowGrain.Received(1).GetCoverSheetAsync();

        // Patient banner
        Assert.That(cut.Markup, Does.Contain("SMITH, JOHN"));
        Assert.That(cut.Markup, Does.Contain("Age: 65"));
        Assert.That(cut.Markup, Does.Contain("SC 70%"));

        // CWAD flags
        Assert.That(cut.Markup, Does.Contain("WA")); // Warnings + Allergies

        // Problems panel
        Assert.That(cut.Markup, Does.Contain("Type 2 Diabetes"));
        Assert.That(cut.Markup, Does.Contain("E11.9"));

        // Allergies panel
        Assert.That(cut.Markup, Does.Contain("Penicillin"));
        Assert.That(cut.Markup, Does.Contain("Severe"));

        // Medications panel
        Assert.That(cut.Markup, Does.Contain("Metformin 500mg"));

        // Vitals panel
        Assert.That(cut.Markup, Does.Contain("Blood Pressure"));
        Assert.That(cut.Markup, Does.Contain("130/85"));
    }

    [Test]
    public async Task CoverSheet_ShowsEmptyPanelsWhenNoData()
    {
        var state = new CoverSheetState
        {
            PatientId = "PATIENT-002",
            Demographics = new PatientDemographicsSummary { Name = "DOE, JANE", Sex = "F", Age = 30 }
        };
        MockWorkflowGrain.GetCoverSheetAsync().Returns(state);

        var cut = Ctx.Render<CoverSheet>();

        cut.Find("input.lookup-input").Input("PATIENT-002");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("DOE, JANE"));
        Assert.That(cut.Markup, Does.Contain("No active problems"));
        Assert.That(cut.Markup, Does.Contain("No Known Allergies"));
        Assert.That(cut.Markup, Does.Contain("No active medications"));
        Assert.That(cut.Markup, Does.Contain("No recent vitals"));
    }

    [Test]
    public async Task CoverSheet_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetCoverSheetAsync().Returns<CoverSheetState>(
            _ => throw new Exception("Connection refused"));

        var cut = Ctx.Render<CoverSheet>();

        cut.Find("input.lookup-input").Input("PATIENT-003");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Error loading cover sheet"));
        Assert.That(cut.Markup, Does.Contain("Connection refused"));
    }

    [Test]
    public async Task CoverSheet_ShowsAdmittedBadge()
    {
        var state = new CoverSheetState
        {
            PatientId = "PATIENT-004",
            Demographics = new PatientDemographicsSummary
            {
                Name = "VETERAN, BOB", Sex = "M", Age = 72,
                IsAdmitted = true, RoomBed = "3B-12"
            }
        };
        MockWorkflowGrain.GetCoverSheetAsync().Returns(state);

        var cut = Ctx.Render<CoverSheet>();

        cut.Find("input.lookup-input").Input("PATIENT-004");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("Admitted"));
        Assert.That(cut.Markup, Does.Contain("3B-12"));
    }

    [Test]
    public async Task CoverSheet_ShowsCwadAllFlags()
    {
        var state = new CoverSheetState
        {
            PatientId = "PATIENT-005",
            Demographics = new PatientDemographicsSummary { Name = "FLAGS, ALL", Sex = "F", Age = 50 },
            Cwad = new CwadFlags
            {
                HasCrisisNotes = true, HasWarnings = true,
                HasAllergies = true, HasAdvanceDirectives = true
            }
        };
        MockWorkflowGrain.GetCoverSheetAsync().Returns(state);

        var cut = Ctx.Render<CoverSheet>();

        cut.Find("input.lookup-input").Input("PATIENT-005");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("CWAD"));
    }

    [Test]
    public async Task CoverSheet_DisplaysAllPanels()
    {
        var state = new CoverSheetState
        {
            PatientId = "PATIENT-006",
            Demographics = new PatientDemographicsSummary { Name = "FULL, CHART", Sex = "M", Age = 55 },
            ActiveProblems = [new() { Diagnosis = "CHF", Status = "ACTIVE" }],
            Allergies = [new() { Allergen = "Sulfa", Reactions = [] }],
            ActiveMedications = [new() { DrugName = "Lisinopril", Status = "ACTIVE" }],
            ClinicalReminders = [new() { ReminderName = "Flu Shot", Status = "DUE" }],
            RecentLabs = [new() { TestName = "HbA1c", ResultValue = "7.2", Units = "%" }],
            RecentVitals = [new() { VitalType = "Temp", Value = "98.6", Units = "F",
                                     DateTimeTaken = DateTime.UtcNow }],
            RecentVisits = [new() { ClinicName = "Primary Care", Status = "CHECKED IN",
                                     AppointmentDateTime = DateTime.UtcNow }],
            ActiveOrders = [new() { OrderText = "CBC w/Diff", Status = "ACTIVE" }]
        };
        MockWorkflowGrain.GetCoverSheetAsync().Returns(state);

        var cut = Ctx.Render<CoverSheet>();

        cut.Find("input.lookup-input").Input("PATIENT-006");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(cut.Markup, Does.Contain("CHF"));
        Assert.That(cut.Markup, Does.Contain("Sulfa"));
        Assert.That(cut.Markup, Does.Contain("Lisinopril"));
        Assert.That(cut.Markup, Does.Contain("Flu Shot"));
        Assert.That(cut.Markup, Does.Contain("HbA1c"));
        Assert.That(cut.Markup, Does.Contain("98.6"));
        Assert.That(cut.Markup, Does.Contain("Primary Care"));
        Assert.That(cut.Markup, Does.Contain("CBC w/Diff"));
    }
}
