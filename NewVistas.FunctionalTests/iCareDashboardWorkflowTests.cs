// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for the iCare Dashboard feature.
/// Tests feature gating via ISiteParametersGrain, panel lifecycle,
/// clinical data aggregation, and multi-provider panel independence.
/// Provider-centric (not via PatientWorkflowGrain).
/// </summary>
[TestFixture]
public class iCareDashboardWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IiCareDashboardGrain GetDashboard(string key) =>
        _cluster.GrainFactory.GetGrain<IiCareDashboardGrain>(key);

    private IPatientGrain GetPatient(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);

    private IClinicalReminderGrain GetReminder(string reminderId) =>
        _cluster.GrainFactory.GetGrain<IClinicalReminderGrain>(reminderId);

    private ISiteParametersGrain GetSiteParams() =>
        _cluster.GrainFactory.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");

    // ── 1. Feature Gate ──────────────────────────────────────────────────────

    [Test]
    public async Task iCareDashboard_FeatureGate_ChecksEnabled()
    {
        // Arrange
        ISiteParametersGrain siteParams = GetSiteParams();

        // Act / Assert — disabled by default
        bool disabledByDefault = await siteParams.IsFeatureEnabledAsync("ICARE_DASHBOARD");
        Assert.That(disabledByDefault, Is.False);

        // Act — enable the feature
        await siteParams.EnableFeatureAsync("ICARE_DASHBOARD");

        // Assert — now enabled
        bool enabledAfterSet = await siteParams.IsFeatureEnabledAsync("ICARE_DASHBOARD");
        Assert.That(enabledAfterSet, Is.True);

        // Verify it appears in the feature set
        HashSet<string> features = await siteParams.GetFeaturesAsync();
        Assert.That(features, Contains.Item("ICARE_DASHBOARD"));
    }

    // ── 2. Panel Lifecycle ───────────────────────────────────────────────────

    [Test]
    public async Task iCareDashboard_PanelManagement_FullLifecycle()
    {
        // Arrange
        string dashKey = $"ICARE:{Guid.NewGuid()}";
        string patientId1 = $"PATIENT-{Guid.NewGuid()}";
        string patientId2 = $"PATIENT-{Guid.NewGuid()}";
        IiCareDashboardGrain dashboard = GetDashboard(dashKey);

        // Act — add 2 patients
        await dashboard.AddPatientToPanelAsync(patientId1, "DOE,JOHN");
        await dashboard.AddPatientToPanelAsync(patientId2, "DOE,JANE");

        // Assert — panel has 2
        List<PanelPatient> panel = await dashboard.GetPanelAsync();
        Assert.That(panel, Has.Count.EqualTo(2));

        // Act — remove 1 patient
        await dashboard.RemovePatientFromPanelAsync(patientId1);

        // Assert — panel has 1
        panel = await dashboard.GetPanelAsync();
        Assert.That(panel, Has.Count.EqualTo(1));
        Assert.That(panel[0].PatientId, Is.EqualTo(patientId2));
    }

    // ── 3. Dashboard with Clinical Data ──────────────────────────────────────

    [Test]
    public async Task iCareDashboard_GenerateDashboard_WithClinicalData()
    {
        // Arrange — enable feature
        ISiteParametersGrain siteParams = GetSiteParams();
        await siteParams.EnableFeatureAsync("ICARE_DASHBOARD");

        // Create patient with demographics
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientGrain patient = GetPatient(patientId);
        await patient.UpdateDemographicsAsync("CLINICAL,PAT", "M", new DateTime(1965, 5, 20), "555667777");

        // Create a DUE reminder for the patient
        string reminderId = $"REMINDER-{Guid.NewGuid()}";
        IClinicalReminderGrain reminder = GetReminder(reminderId);
        await reminder.CreateReminderAsync(
            patientId, "Diabetic Eye Exam", null, "EXAM", "NORMAL", "ANNUALLY",
            DateTime.UtcNow.AddDays(-60));
        await patient.AddClinicalReminderIdAsync(reminderId);

        // Add patient to panel and generate
        string dashKey = $"ICARE:{Guid.NewGuid()}";
        IiCareDashboardGrain dashboard = GetDashboard(dashKey);
        await dashboard.AddPatientToPanelAsync(patientId, "CLINICAL,PAT");

        // Act
        iCareDashboardResult result = await dashboard.GenerateDashboardAsync();

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.TotalPatients, Is.EqualTo(1));
        Assert.That(result.TotalDueReminders, Is.GreaterThan(0));
        Assert.That(result.PatientSummaries[0].PatientName, Is.EqualTo("CLINICAL,PAT"));
        Assert.That(result.PatientSummaries[0].DueReminderCount, Is.GreaterThan(0));
        Assert.That(result.PatientSummaries[0].DueReminders[0].ReminderName, Is.EqualTo("Diabetic Eye Exam"));

        // Verify state was persisted
        iCareDashboardState state = await dashboard.GetDashboardStateAsync();
        Assert.That(state.LastGeneratedDate, Is.Not.Null);
        Assert.That(state.PatientSummaries, Has.Count.EqualTo(1));
    }

    // ── 4. Multi-Provider Independence ───────────────────────────────────────

    [Test]
    public async Task iCareDashboard_MultipleProviders_IndependentPanels()
    {
        // Arrange — two providers with distinct panels
        string provider1Key = $"ICARE:{Guid.NewGuid()}";
        string provider2Key = $"ICARE:{Guid.NewGuid()}";
        string patientA = $"PATIENT-{Guid.NewGuid()}";
        string patientB = $"PATIENT-{Guid.NewGuid()}";

        // Create patients with demographics
        IPatientGrain patA = GetPatient(patientA);
        await patA.UpdateDemographicsAsync("ALPHA,PAT", "M", new DateTime(1950, 1, 1), "111111111");
        IPatientGrain patB = GetPatient(patientB);
        await patB.UpdateDemographicsAsync("BRAVO,PAT", "F", new DateTime(1960, 2, 2), "222222222");

        IiCareDashboardGrain dash1 = GetDashboard(provider1Key);
        IiCareDashboardGrain dash2 = GetDashboard(provider2Key);

        // Act — each provider adds different patients
        await dash1.AddPatientToPanelAsync(patientA, "ALPHA,PAT");
        await dash2.AddPatientToPanelAsync(patientB, "BRAVO,PAT");

        // Assert — panels are independent
        List<PanelPatient> panel1 = await dash1.GetPanelAsync();
        List<PanelPatient> panel2 = await dash2.GetPanelAsync();

        Assert.That(panel1, Has.Count.EqualTo(1));
        Assert.That(panel1[0].PatientId, Is.EqualTo(patientA));

        Assert.That(panel2, Has.Count.EqualTo(1));
        Assert.That(panel2[0].PatientId, Is.EqualTo(patientB));

        // Generate dashboards independently
        iCareDashboardResult result1 = await dash1.GenerateDashboardAsync();
        iCareDashboardResult result2 = await dash2.GenerateDashboardAsync();

        Assert.That(result1.PatientSummaries[0].PatientName, Is.EqualTo("ALPHA,PAT"));
        Assert.That(result2.PatientSummaries[0].PatientName, Is.EqualTo("BRAVO,PAT"));
    }
}
