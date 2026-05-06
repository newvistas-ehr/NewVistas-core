// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for the iCare Dashboard grain — IHS RPMS iCare / BQI provider dashboard.
/// Tests panel management, dashboard generation, clinical status computation,
/// and cached summary retrieval.
/// </summary>
[TestFixture]
public class iCareDashboardGrainTests
{
    private TestCluster _cluster = default!;

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

    // ── 1. Panel Management ──────────────────────────────────────────────────

    [Test]
    public async Task iCareDashboard_AddPatientToPanel()
    {
        // Arrange
        string dashKey = $"ICARE:{Guid.NewGuid()}";
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IiCareDashboardGrain dashboard = GetDashboard(dashKey);

        // Act
        await dashboard.AddPatientToPanelAsync(patientId, "DOE,JOHN");

        // Assert
        List<PanelPatient> panel = await dashboard.GetPanelAsync();
        Assert.That(panel, Has.Count.EqualTo(1));
        Assert.That(panel[0].PatientId, Is.EqualTo(patientId));
        Assert.That(panel[0].PatientName, Is.EqualTo("DOE,JOHN"));
    }

    [Test]
    public async Task iCareDashboard_RemovePatientFromPanel()
    {
        // Arrange
        string dashKey = $"ICARE:{Guid.NewGuid()}";
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IiCareDashboardGrain dashboard = GetDashboard(dashKey);
        await dashboard.AddPatientToPanelAsync(patientId, "DOE,JANE");

        // Act
        await dashboard.RemovePatientFromPanelAsync(patientId);

        // Assert
        List<PanelPatient> panel = await dashboard.GetPanelAsync();
        Assert.That(panel, Has.Count.EqualTo(0));
    }

    [Test]
    public async Task iCareDashboard_NoDuplicatesInPanel()
    {
        // Arrange
        string dashKey = $"ICARE:{Guid.NewGuid()}";
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IiCareDashboardGrain dashboard = GetDashboard(dashKey);

        // Act — add same patient twice
        await dashboard.AddPatientToPanelAsync(patientId, "DOE,JOHN");
        await dashboard.AddPatientToPanelAsync(patientId, "DOE,JOHN");

        // Assert
        List<PanelPatient> panel = await dashboard.GetPanelAsync();
        Assert.That(panel, Has.Count.EqualTo(1));
    }

    // ── 2. Dashboard Generation ──────────────────────────────────────────────

    [Test]
    public async Task iCareDashboard_GenerateDashboard_EmptyPanel()
    {
        // Arrange
        string dashKey = $"ICARE:{Guid.NewGuid()}";
        IiCareDashboardGrain dashboard = GetDashboard(dashKey);

        // Act
        iCareDashboardResult result = await dashboard.GenerateDashboardAsync();

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.TotalPatients, Is.EqualTo(0));
        Assert.That(result.PatientSummaries, Has.Count.EqualTo(0));
    }

    [Test]
    public async Task iCareDashboard_GenerateDashboard_WithPatient()
    {
        // Arrange
        string dashKey = $"ICARE:{Guid.NewGuid()}";
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientGrain patient = GetPatient(patientId);
        await patient.UpdateDemographicsAsync("DOE,JOHN", "M", new DateTime(1960, 1, 1), "123456789");

        IiCareDashboardGrain dashboard = GetDashboard(dashKey);
        await dashboard.AddPatientToPanelAsync(patientId, "DOE,JOHN");

        // Act
        iCareDashboardResult result = await dashboard.GenerateDashboardAsync();

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.TotalPatients, Is.EqualTo(1));
        Assert.That(result.PatientSummaries, Has.Count.EqualTo(1));
        Assert.That(result.PatientSummaries[0].PatientId, Is.EqualTo(patientId));
        Assert.That(result.PatientSummaries[0].PatientName, Is.EqualTo("DOE,JOHN"));
        Assert.That(result.PatientSummaries[0].Sex, Is.EqualTo("M"));
    }

    [Test]
    public async Task iCareDashboard_GenerateDashboard_WithReminders()
    {
        // Arrange
        string dashKey = $"ICARE:{Guid.NewGuid()}";
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientGrain patient = GetPatient(patientId);
        await patient.UpdateDemographicsAsync("DOE,REMINDER", "F", new DateTime(1975, 6, 15), "987654321");

        string reminderId = $"REMINDER-{Guid.NewGuid()}";
        IClinicalReminderGrain reminder = GetReminder(reminderId);
        await reminder.CreateReminderAsync(
            patientId, "Annual Physical", null, "EXAM", "HIGH", "ANNUALLY",
            DateTime.UtcNow.AddDays(-30));
        await patient.AddClinicalReminderIdAsync(reminderId);

        IiCareDashboardGrain dashboard = GetDashboard(dashKey);
        await dashboard.AddPatientToPanelAsync(patientId, "DOE,REMINDER");

        // Act
        iCareDashboardResult result = await dashboard.GenerateDashboardAsync();

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.TotalDueReminders, Is.GreaterThan(0));
        Assert.That(result.PatientSummaries[0].DueReminderCount, Is.GreaterThan(0));
    }

    // ── 3. Status Computation ────────────────────────────────────────────────

    [Test]
    public async Task iCareDashboard_StatusGreen_WhenNoGaps()
    {
        // Arrange — patient with no reminders, no quality gaps
        string dashKey = $"ICARE:{Guid.NewGuid()}";
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientGrain patient = GetPatient(patientId);
        await patient.UpdateDemographicsAsync("GREEN,PAT", "M", new DateTime(1980, 3, 10), "111223333");

        IiCareDashboardGrain dashboard = GetDashboard(dashKey);
        await dashboard.AddPatientToPanelAsync(patientId, "GREEN,PAT");

        // Act
        iCareDashboardResult result = await dashboard.GenerateDashboardAsync();

        // Assert
        Assert.That(result.PatientSummaries[0].OverallStatus, Is.EqualTo("GREEN"));
        Assert.That(result.PatientsWithGaps, Is.EqualTo(0));
    }

    [Test]
    public async Task iCareDashboard_StatusYellow_WhenSomeGaps()
    {
        // Arrange — patient with 1 DUE reminder (non-HIGH priority) → YELLOW
        string dashKey = $"ICARE:{Guid.NewGuid()}";
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientGrain patient = GetPatient(patientId);
        await patient.UpdateDemographicsAsync("YELLOW,PAT", "F", new DateTime(1970, 8, 20), "444556666");

        string reminderId = $"REMINDER-{Guid.NewGuid()}";
        IClinicalReminderGrain reminder = GetReminder(reminderId);
        await reminder.CreateReminderAsync(
            patientId, "Flu Shot", null, "IMMUNIZATION", "NORMAL", "ANNUALLY",
            DateTime.UtcNow.AddDays(-10));
        await patient.AddClinicalReminderIdAsync(reminderId);

        IiCareDashboardGrain dashboard = GetDashboard(dashKey);
        await dashboard.AddPatientToPanelAsync(patientId, "YELLOW,PAT");

        // Act
        iCareDashboardResult result = await dashboard.GenerateDashboardAsync();

        // Assert
        Assert.That(result.PatientSummaries[0].OverallStatus, Is.EqualTo("YELLOW"));
        Assert.That(result.PatientsWithGaps, Is.EqualTo(1));
    }

    // ── 4. Cached Retrieval ──────────────────────────────────────────────────

    [Test]
    public async Task iCareDashboard_GetPatientSummary()
    {
        // Arrange
        string dashKey = $"ICARE:{Guid.NewGuid()}";
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientGrain patient = GetPatient(patientId);
        await patient.UpdateDemographicsAsync("CACHE,PAT", "M", new DateTime(1955, 12, 1), "777889999");

        IiCareDashboardGrain dashboard = GetDashboard(dashKey);
        await dashboard.AddPatientToPanelAsync(patientId, "CACHE,PAT");
        await dashboard.GenerateDashboardAsync();

        // Act — retrieve cached summary
        iCarePatientSummary summary = await dashboard.GetPatientSummaryAsync(patientId);

        // Assert
        Assert.That(summary, Is.Not.Null);
        Assert.That(summary.PatientId, Is.EqualTo(patientId));
        Assert.That(summary.PatientName, Is.EqualTo("CACHE,PAT"));
    }

    [Test]
    public async Task iCareDashboard_GetDashboardState()
    {
        // Arrange
        string dashKey = $"ICARE:{Guid.NewGuid()}";
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientGrain patient = GetPatient(patientId);
        await patient.UpdateDemographicsAsync("STATE,PAT", "F", new DateTime(1990, 4, 25), "222334444");

        IiCareDashboardGrain dashboard = GetDashboard(dashKey);
        await dashboard.AddPatientToPanelAsync(patientId, "STATE,PAT");
        await dashboard.GenerateDashboardAsync();

        // Act
        iCareDashboardState state = await dashboard.GetDashboardStateAsync();

        // Assert
        Assert.That(state, Is.Not.Null);
        Assert.That(state.PatientSummaries, Has.Count.EqualTo(1));
        Assert.That(state.LastGeneratedDate, Is.Not.Null);
        Assert.That(state.Panel, Has.Count.EqualTo(1));
    }
}
