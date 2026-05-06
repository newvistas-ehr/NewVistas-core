// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for VistA V IMMUNIZATION — File #9000010.11.
/// Immunizations are now embedded on the patient grain as ImmunizationEntry.
/// Tests exercise the workflow grain methods for recording, VIS, series tracking,
/// manufacturer, vaccine group, comments, and full lifecycle workflows.
/// </summary>
[TestFixture]
public class ImmunizationWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IPatientWorkflowGrain NewWorkflow()
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>($"PATIENT-{Guid.NewGuid()}");

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private IPatientGrain GetPatient(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);

    private async Task<string> RecordStandardAsync(IPatientWorkflowGrain w, string name = "COVID-19 VACCINE")
    {
        return await w.RecordImmunizationAsync(
            name, "CVX-213",
            DateTime.UtcNow, "1",
            "LOT-ABC123", "PFIZER",
            "NURSE-001", "Nurse Johnson",
            "LEFT DELTOID", "INTRAMUSCULAR",
            "0.3 mL",
            "LOC-001", "Primary Care Clinic",
            "Patient tolerated well");
    }

    // ─── 1. Record Immunization ───────────────────────────────────────────────

    [Test]
    public async Task Immunization_CanRecordImmunization()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        string id = await RecordStandardAsync(w);

        ImmunizationEntry? entry = await GetPatient(patientId).GetImmunizationAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.ImmunizationName, Is.EqualTo("COVID-19 VACCINE"));
        Assert.That(entry.CvxCode, Is.EqualTo("CVX-213"));
        Assert.That(entry.LotNumber, Is.EqualTo("LOT-ABC123"));
        Assert.That(entry.Manufacturer, Is.EqualTo("PFIZER"));
        Assert.That(entry.AdministeredByName, Is.EqualTo("Nurse Johnson"));
        Assert.That(entry.AdministrationSite, Is.EqualTo("LEFT DELTOID"));
        Assert.That(entry.Route, Is.EqualTo("INTRAMUSCULAR"));
    }

    // ─── 2. Get Immunization ──────────────────────────────────────────────────

    [Test]
    public async Task Immunization_CanGetImmunization()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        string id = await w.RecordImmunizationAsync(
            "INFLUENZA VACCINE", "CVX-158",
            DateTime.UtcNow, null,
            "LOT-FLU-2024", "Sanofi",
            "RN-1", "Nurse Kim",
            "LEFT DELTOID", "IM",
            "0.5 mL",
            null, null, null);

        ImmunizationEntry? entry = await GetPatient(patientId).GetImmunizationAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.ImmunizationName, Is.EqualTo("INFLUENZA VACCINE"));
        Assert.That(entry.ImmunizationId, Is.Not.Empty);
    }

    // ─── 3. Mark As Historical ──────────────────────────────────────────────────

    [Test]
    public async Task Immunization_CanMarkAsHistorical()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await RecordStandardAsync(w);

        await w.MarkImmunizationHistoricalAsync(id, "HISTORICAL");

        ImmunizationEntry? entry = await GetPatient(patientId).GetImmunizationAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.InformationSource, Is.EqualTo("HISTORICAL"));
    }

    // ─── 4. Record VIS ────────────────────────────────────────────────────────

    [Test]
    public async Task Immunization_CanRecordVIS()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await RecordStandardAsync(w);
        DateTime visOffered = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);
        DateTime visPublished = new DateTime(2025, 12, 15, 0, 0, 0, DateTimeKind.Utc);

        await w.RecordImmunizationVISAsync(id, visOffered, visPublished);

        ImmunizationEntry? entry = await GetPatient(patientId).GetImmunizationAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.VisDateOffered, Is.EqualTo(visOffered));
        Assert.That(entry.VisDatePublished, Is.EqualTo(visPublished));
    }

    // ─── 5. Set Series Info ───────────────────────────────────────────────────

    [Test]
    public async Task Immunization_CanSetSeriesInfo()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await RecordStandardAsync(w);

        await w.SetImmunizationSeriesInfoAsync(id, 2, 3, false);

        ImmunizationEntry? entry = await GetPatient(patientId).GetImmunizationAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.DoseNumber, Is.EqualTo(2));
        Assert.That(entry.DosesInSeries, Is.EqualTo(3));
        Assert.That(entry.SeriesComplete, Is.False);
    }

    // ─── 6. Series Toggle ───────────────────────────────────────────────────

    [Test]
    public async Task Immunization_SeriesToggle()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await RecordStandardAsync(w);

        await w.SetImmunizationSeriesInfoAsync(id, 3, 3, true);
        ImmunizationEntry? entry1 = await GetPatient(patientId).GetImmunizationAsync(id);
        Assert.That(entry1!.SeriesComplete, Is.True);

        await w.SetImmunizationSeriesInfoAsync(id, 2, 3, false);
        ImmunizationEntry? entry2 = await GetPatient(patientId).GetImmunizationAsync(id);
        Assert.That(entry2!.DoseNumber, Is.EqualTo(2));
        Assert.That(entry2.SeriesComplete, Is.False);
    }

    // ─── 7. Set Administration Details ────────────────────────────────────────

    [Test]
    public async Task Immunization_CanSetAdministrationDetails()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await RecordStandardAsync(w);

        await w.SetImmunizationAdministrationDetailsAsync(id, "RIGHT THIGH", "SUBCUTANEOUS");

        ImmunizationEntry? entry = await GetPatient(patientId).GetImmunizationAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.AdministrationSite, Is.EqualTo("RIGHT THIGH"));
        Assert.That(entry.Route, Is.EqualTo("SUBCUTANEOUS"));
    }

    // ─── 8. Set Vaccine Group ────────────────────────────────────────────────

    [Test]
    public async Task Immunization_CanSetVaccineGroup()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await RecordStandardAsync(w);

        await w.SetImmunizationVaccineGroupAsync(id, "HEPATITIS B", "HBV-45");

        ImmunizationEntry? entry = await GetPatient(patientId).GetImmunizationAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.VaccineGroupName, Is.EqualTo("HEPATITIS B"));
        Assert.That(entry.VaccineGroupCode, Is.EqualTo("HBV-45"));
    }

    // ─── 9. Set Manufacturer ─────────────────────────────────────────────────

    [Test]
    public async Task Immunization_CanSetManufacturer()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await RecordStandardAsync(w);

        await w.SetImmunizationManufacturerAsync(id, "MODERNA", "MOD");

        ImmunizationEntry? entry = await GetPatient(patientId).GetImmunizationAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.ManufacturerName, Is.EqualTo("MODERNA"));
        Assert.That(entry.ManufacturerCode, Is.EqualTo("MOD"));
    }

    // ─── 10. Update Registry Status ──────────────────────────────────────────

    [Test]
    public async Task Immunization_CanUpdateRegistryStatus()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await RecordStandardAsync(w);

        await w.UpdateImmunizationRegistryStatusAsync(id, "REPORTED");

        ImmunizationEntry? entry = await GetPatient(patientId).GetImmunizationAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.RegistryStatus, Is.EqualTo("REPORTED"));
    }

    // ─── 11. Add Comment ──────────────────────────────────────────────────────

    [Test]
    public async Task Immunization_CanAddComment()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await RecordStandardAsync(w);

        await w.AddImmunizationCommentAsync(id, "Nurse Johnson", "Patient tolerated vaccine well, no immediate reactions");

        ImmunizationEntry? entry = await GetPatient(patientId).GetImmunizationAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.ImmunizationComments, Has.Count.EqualTo(1));
        Assert.That(entry.ImmunizationComments[0].AuthorName, Is.EqualTo("Nurse Johnson"));
        Assert.That(entry.ImmunizationComments[0].CommentText, Is.EqualTo("Patient tolerated vaccine well, no immediate reactions"));
    }

    // ─── 12. Add Multiple Comments ───────────────────────────────────────────

    [Test]
    public async Task Immunization_CanAddMultipleComments()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await RecordStandardAsync(w);

        await w.AddImmunizationCommentAsync(id, "Nurse Johnson", "Vaccine administered per protocol");
        await w.AddImmunizationCommentAsync(id, "Dr. Smith", "Patient counseled on side effects");
        await w.AddImmunizationCommentAsync(id, "Nurse Johnson", "15-minute observation complete, no reactions");

        ImmunizationEntry? entry = await GetPatient(patientId).GetImmunizationAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.ImmunizationComments, Has.Count.EqualTo(3));
        Assert.That(entry.ImmunizationComments[0].AuthorName, Is.EqualTo("Nurse Johnson"));
        Assert.That(entry.ImmunizationComments[1].AuthorName, Is.EqualTo("Dr. Smith"));
        Assert.That(entry.ImmunizationComments[2].AuthorName, Is.EqualTo("Nurse Johnson"));
    }

    // ─── 13. List Immunizations ───────────────────────────────────────────────

    [Test]
    public async Task Immunization_ListReturnsAll()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        await RecordStandardAsync(w, "COVID-19 VACCINE");
        await w.RecordImmunizationAsync(
            "INFLUENZA VACCINE", "CVX-158",
            DateTime.UtcNow, null,
            "LOT-FLU-2024", "Sanofi",
            "RN-1", "Nurse Kim",
            "LEFT DELTOID", "IM",
            "0.5 mL",
            null, null, null);

        List<ImmunizationSummary> list = await w.GetImmunizationsAsync();
        Assert.That(list, Has.Count.EqualTo(2));
    }

    // ─── 14. Patient Linkage ──────────────────────────────────────────────────

    [Test]
    public async Task Immunization_LinksToPatient()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        await RecordStandardAsync(w);

        List<ImmunizationEntry> entries = await GetPatient(patientId).GetImmunizationsAsync();
        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(entries[0].ImmunizationId, Does.StartWith("IMM-"));
    }

    // ─── 15. Multiple Patients Independent ────────────────────────────────────

    [Test]
    public async Task Immunization_MultiplePatients_Independent()
    {
        IPatientWorkflowGrain w1 = NewWorkflow();
        IPatientWorkflowGrain w2 = NewWorkflow();

        await RecordStandardAsync(w1);

        List<ImmunizationSummary> list2 = await w2.GetImmunizationsAsync();
        Assert.That(list2, Is.Empty);
    }

    // ─── 16. Full Workflow ───────────────────────────────────────────────────

    [Test]
    public async Task Immunization_FullWorkflow()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        // Record immunization
        string id = await w.RecordImmunizationAsync(
            "HEPATITIS B VACCINE", "CVX-08",
            DateTime.UtcNow, "1",
            "LOT-HBV-001", "MERCK",
            "NURSE-001", "Nurse Davis",
            "LEFT DELTOID", "INTRAMUSCULAR",
            "1.0 mL",
            "LOC-001", "Primary Care Clinic",
            null);

        ImmunizationEntry? s1 = await GetPatient(patientId).GetImmunizationAsync(id);
        Assert.That(s1!.ImmunizationName, Is.EqualTo("HEPATITIS B VACCINE"));

        // Set administration details
        await w.SetImmunizationAdministrationDetailsAsync(id, "RIGHT DELTOID", "INTRAMUSCULAR");

        // Record VIS
        DateTime visOffered = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);
        DateTime visPublished = new DateTime(2025, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        await w.RecordImmunizationVISAsync(id, visOffered, visPublished);

        // Set series info
        await w.SetImmunizationSeriesInfoAsync(id, 1, 3, false);

        // Set manufacturer
        await w.SetImmunizationManufacturerAsync(id, "MERCK", "MSD");

        // Set vaccine group
        await w.SetImmunizationVaccineGroupAsync(id, "HEPATITIS B", "HBV-45");

        // Add comments
        await w.AddImmunizationCommentAsync(id, "Nurse Davis", "First dose of HepB series");

        // Update registry status
        await w.UpdateImmunizationRegistryStatusAsync(id, "REPORTED");

        // Final assertions
        ImmunizationEntry? final_entry = await GetPatient(patientId).GetImmunizationAsync(id);
        Assert.That(final_entry, Is.Not.Null);
        Assert.That(final_entry!.AdministrationSite, Is.EqualTo("RIGHT DELTOID"));
        Assert.That(final_entry.Route, Is.EqualTo("INTRAMUSCULAR"));
        Assert.That(final_entry.VisDateOffered, Is.EqualTo(visOffered));
        Assert.That(final_entry.VisDatePublished, Is.EqualTo(visPublished));
        Assert.That(final_entry.DoseNumber, Is.EqualTo(1));
        Assert.That(final_entry.DosesInSeries, Is.EqualTo(3));
        Assert.That(final_entry.SeriesComplete, Is.False);
        Assert.That(final_entry.ManufacturerName, Is.EqualTo("MERCK"));
        Assert.That(final_entry.ManufacturerCode, Is.EqualTo("MSD"));
        Assert.That(final_entry.VaccineGroupName, Is.EqualTo("HEPATITIS B"));
        Assert.That(final_entry.VaccineGroupCode, Is.EqualTo("HBV-45"));
        Assert.That(final_entry.ImmunizationComments, Has.Count.EqualTo(1));
        Assert.That(final_entry.RegistryStatus, Is.EqualTo("REPORTED"));
    }
}
