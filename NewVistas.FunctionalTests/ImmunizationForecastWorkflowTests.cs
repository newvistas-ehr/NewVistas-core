// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for Immunization Forecast via the PatientWorkflowGrain.
/// Verifies the Site Flavor Architecture (Option 4 — Composition) feature gate,
/// forecast generation through the workflow layer, DOB validation, and cached results.
/// Maps to IHS RPMS Immunization Forecasting module (BI FORECAST RPCs).
/// </summary>
[TestFixture]
public class ImmunizationForecastWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain GetWorkflow(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private IPatientGrain GetPatient(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);

    private ISiteParametersGrain GetSiteParams() =>
        _cluster.GrainFactory.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");

    [Test]
    public async Task WorkflowForecast_FailsWhenFeatureDisabled()
    {
        // Arrange — do NOT enable the IMMUNIZATION_FORECAST feature
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientGrain patient = GetPatient(patientId);
        await patient.UpdateDemographicsAsync("SMITH,JOHN", "M", new DateTime(2000, 5, 10), "123456789");
        IPatientWorkflowGrain workflow = GetWorkflow(patientId);

        // Act
        ImmunizationForecastResult result = await workflow.GenerateImmunizationForecastAsync();

        // Assert — should fail with an error about the feature not being enabled
        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task WorkflowForecast_SucceedsWhenFeatureEnabled()
    {
        // Arrange — enable the feature and create patient with DOB and immunizations
        ISiteParametersGrain siteParams = GetSiteParams();
        await siteParams.EnableFeatureAsync("IMMUNIZATION_FORECAST");

        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientGrain patient = GetPatient(patientId);
        await patient.UpdateDemographicsAsync("DOE,JANE", "F", new DateTime(2020, 1, 15), "987654321");
        await patient.AddImmunizationAsync(new ImmunizationEntry
        {
            ImmunizationId = "IMM-1",
            ImmunizationName = "Hep B Dose 1",
            CvxCode = "08",
            EventDateTime = new DateTime(2020, 1, 15),
            VaccineGroupName = "Hepatitis B",
        });
        await patient.AddImmunizationAsync(new ImmunizationEntry
        {
            ImmunizationId = "IMM-2",
            ImmunizationName = "Hep B Dose 2",
            CvxCode = "08",
            EventDateTime = new DateTime(2020, 3, 15),
            VaccineGroupName = "Hepatitis B",
        });

        IPatientWorkflowGrain workflow = GetWorkflow(patientId);

        // Act
        ImmunizationForecastResult result = await workflow.GenerateImmunizationForecastAsync();

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Recommendations, Is.Not.Empty);
        Assert.That(result.ForecastDate, Is.GreaterThan(DateTime.MinValue));
    }

    [Test]
    public async Task WorkflowForecast_FailsWithoutDateOfBirth()
    {
        // Arrange — enable feature, create patient WITHOUT date of birth
        ISiteParametersGrain siteParams = GetSiteParams();
        await siteParams.EnableFeatureAsync("IMMUNIZATION_FORECAST");

        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientGrain patient = GetPatient(patientId);
        // UpdateDemographics with default(DateTime) — no valid DOB
        await patient.UpdateDemographicsAsync("NODOB,PATIENT", "M", default, "111223333");

        IPatientWorkflowGrain workflow = GetWorkflow(patientId);

        // Act
        ImmunizationForecastResult result = await workflow.GenerateImmunizationForecastAsync();

        // Assert — should fail with DOB required message
        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorMessage, Is.Not.Null);
        Assert.That(result.ErrorMessage, Does.Contain("date of birth").IgnoreCase);
    }

    [Test]
    public async Task WorkflowForecast_GetForecast_ReturnsCachedResult()
    {
        // Arrange — enable feature, create patient, generate forecast
        ISiteParametersGrain siteParams = GetSiteParams();
        await siteParams.EnableFeatureAsync("IMMUNIZATION_FORECAST");

        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientGrain patient = GetPatient(patientId);
        await patient.UpdateDemographicsAsync("CACHED,RESULT", "F", new DateTime(2019, 6, 1), "555667777");
        await patient.AddImmunizationAsync(new ImmunizationEntry
        {
            ImmunizationId = "IMM-1",
            ImmunizationName = "DTaP Dose 1",
            CvxCode = "20",
            EventDateTime = new DateTime(2019, 8, 1),
            VaccineGroupName = "DTaP",
        });

        IPatientWorkflowGrain workflow = GetWorkflow(patientId);
        ImmunizationForecastResult generated = await workflow.GenerateImmunizationForecastAsync();
        Assert.That(generated.Success, Is.True);

        // Act — retrieve the cached forecast without regenerating
        ImmunizationForecastResult cached = await workflow.GetImmunizationForecastAsync();

        // Assert — same recommendations should be returned
        Assert.That(cached.Success, Is.True);
        Assert.That(cached.Recommendations, Has.Count.EqualTo(generated.Recommendations.Count));
        Assert.That(cached.TotalDue, Is.EqualTo(generated.TotalDue));
        Assert.That(cached.TotalOverdue, Is.EqualTo(generated.TotalOverdue));
        Assert.That(cached.TotalComplete, Is.EqualTo(generated.TotalComplete));
    }
}
