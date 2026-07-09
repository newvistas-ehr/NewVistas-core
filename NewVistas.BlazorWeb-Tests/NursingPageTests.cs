// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.BlazorWeb.Components.Pages;
using NSubstitute;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class NursingPageTests : BlazorTestBase
{
    private void StubUnitIndex()
    {
        // The unit directory now lives on the per-institution BED-CAPACITY rollup grain
        // (INursingUnitIndexGrain was retired by the bed-management rework).
        var mockCapacity = Substitute.For<IBedCapacityGrain>();
        mockCapacity.GetUnitsAsync(Arg.Any<bool>()).Returns(new List<UnitCapacitySummary>());
        MockGrainFactory
            .GetGrain<IBedCapacityGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(mockCapacity);
    }

    [Test]
    public void Nursing_PromptsToSelectPatientWhenNoneChosen()
    {
        var cut = Ctx.Render<Nursing>();
        // No patient in context — the shared PatientBar prompts the user to pick one.
        Assert.That(cut.Markup, Does.Contain("Select a patient"));
    }

    [Test]
    public void Nursing_ShowsSelectedPatientInBar()
    {
        StubUnitIndex();
        MockWorkflowGrain.GetNursingAssessmentsAsync().Returns(new List<NursingAssessmentIndexEntry>());
        MockWorkflowGrain.GetNursingCarePlanAsync().Returns(new NursingCarePlanState { PatientId = "PATIENT-001" });
        MockWorkflowGrain.GetNursingAcuityAsync().Returns(new NursingAcuityState { PatientId = "PATIENT-001" });

        SelectPatient("PATIENT-001", "SMITH, JOHN");
        var cut = Ctx.Render<Nursing>();

        Assert.That(cut.Markup, Does.Contain("Change patient"));
    }

    [Test]
    public void Nursing_LoadsDataFromGrain()
    {
        var assessments = new List<NursingAssessmentIndexEntry>
        {
            new() { AssessmentId = "NA-001", AssessmentDateTime = DateTime.Today,
                     AssessmentType = "Shift", NurseName = "Nurse Jones",
                     Status = NursingAssessmentStatus.Draft, PainScore = 3, MorseScore = 25, BradenScore = 18 }
        };
        MockWorkflowGrain.GetNursingAssessmentsAsync().Returns(assessments);
        MockWorkflowGrain.GetNursingCarePlanAsync().Returns(new NursingCarePlanState { PatientId = "PATIENT-001" });
        MockWorkflowGrain.GetNursingAcuityAsync().Returns(new NursingAcuityState { PatientId = "PATIENT-001" });
        StubUnitIndex();

        SelectPatient("PATIENT-001");
        var cut = Ctx.Render<Nursing>();

        Assert.That(cut.Markup, Does.Contain("Nurse Jones"));
        Assert.That(cut.Markup, Does.Contain("Shift"));
    }

    [Test]
    public void Nursing_ShowsEmptyState()
    {
        MockWorkflowGrain.GetNursingAssessmentsAsync().Returns(new List<NursingAssessmentIndexEntry>());
        MockWorkflowGrain.GetNursingCarePlanAsync().Returns(new NursingCarePlanState { PatientId = "PATIENT-002" });
        MockWorkflowGrain.GetNursingAcuityAsync().Returns(new NursingAcuityState { PatientId = "PATIENT-002" });
        StubUnitIndex();

        SelectPatient("PATIENT-002");
        var cut = Ctx.Render<Nursing>();

        Assert.That(cut.Markup, Does.Contain("No assessments recorded"));
    }

    [Test]
    public void Nursing_ShowsErrorOnGrainFailure()
    {
        MockWorkflowGrain.GetNursingAssessmentsAsync().Returns<List<NursingAssessmentIndexEntry>>(
            _ => throw new Exception("Cluster down"));
        StubUnitIndex();

        SelectPatient("PATIENT-003");
        var cut = Ctx.Render<Nursing>();

        Assert.That(cut.Markup, Does.Contain("Error"));
        Assert.That(cut.Markup, Does.Contain("Cluster down"));
    }
}
