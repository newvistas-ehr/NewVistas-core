// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for VistA Allergy/Adverse Drug Reaction — GMRA package.
/// File #120.8 (PATIENT ALLERGIES) — now embedded on the patient grain.
/// </summary>
[TestFixture]
public class AllergiesWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain NewWorkflow()
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>($"PATIENT-{Guid.NewGuid()}");

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private IPatientGrain GetPatient(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);

    // ─── ID / Creation ────────────────────────────────────────────────────

    [Test]
    public async Task RecordAllergy_ReturnsIdWithAllergyPrefix()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        string id = await w.RecordAllergyAsync(
            "Penicillin", "Drug", null, "O",
            new List<string> { "Rash" }, "Moderate",
            null, null, null);

        Assert.That(id, Does.StartWith("ALLERGY-"));
    }

    [Test]
    public async Task RecordAllergy_AllergenStoredCorrectly()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        await w.RecordAllergyAsync(
            "Sulfa", "Drug", null, "H",
            new List<string> { "Itching" }, "Mild",
            null, null, null);

        List<AllergyEntry> entries = await GetPatient(patientId).GetAllergiesAsync();
        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(entries[0].Allergen, Is.EqualTo("Sulfa"));
    }

    [Test]
    public async Task RecordAllergy_AllergenTypeStored()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        await w.RecordAllergyAsync(
            "Peanuts", "Food", null, "O",
            new List<string> { "Anaphylaxis" }, "Severe",
            null, null, null);

        List<AllergyEntry> entries = await GetPatient(patientId).GetAllergiesAsync();
        Assert.That(entries[0].AllergenType, Is.EqualTo("Food"));
    }

    [Test]
    public async Task RecordAllergy_ReactionListPreserved()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        List<string> reactions = new List<string> { "Hives", "Angioedema", "Nausea" };

        await w.RecordAllergyAsync(
            "Aspirin", "Drug", null, "O",
            reactions, "Moderate", null, null, null);

        List<AllergyEntry> entries = await GetPatient(patientId).GetAllergiesAsync();
        Assert.That(entries[0].Reactions, Has.Count.EqualTo(3));
        Assert.That(entries[0].Reactions, Contains.Item("Hives"));
        Assert.That(entries[0].Reactions, Contains.Item("Angioedema"));
        Assert.That(entries[0].Reactions, Contains.Item("Nausea"));
    }

    [Test]
    public async Task RecordAllergy_SeverityStored()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        await w.RecordAllergyAsync(
            "Codeine", "Drug", null, "O",
            new List<string> { "Nausea" }, "Moderate",
            null, null, null);

        List<AllergyEntry> entries = await GetPatient(patientId).GetAllergiesAsync();
        Assert.That(entries[0].Severity, Is.EqualTo("Moderate"));
    }

    [Test]
    public async Task RecordAllergy_ObservedHistoricalStored()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        await w.RecordAllergyAsync(
            "Latex", "Other", null, "H",
            new List<string> { "Contact Dermatitis" }, "Mild",
            null, null, null);

        List<AllergyEntry> entries = await GetPatient(patientId).GetAllergiesAsync();
        Assert.That(entries[0].ObservedHistorical, Is.EqualTo("H"));
    }

    // ─── Retrieve ─────────────────────────────────────────────────────────

    [Test]
    public async Task GetAllergies_NoAllergies_ReturnsEmpty()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        List<AllergySummary> allergies = await w.GetAllergiesAsync();

        Assert.That(allergies, Is.Empty);
    }

    [Test]
    public async Task GetAllergies_ReturnsAllRecordedAllergens()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        await w.RecordAllergyAsync("Penicillin", "Drug", null, "O",
            new List<string> { "Rash" }, "Moderate", null, null, null);
        await w.RecordAllergyAsync("Shellfish", "Food", null, "O",
            new List<string> { "Hives" }, "Severe", null, null, null);

        List<AllergySummary> allergies = await w.GetAllergiesAsync();
        Assert.That(allergies, Has.Count.EqualTo(2));
        Assert.That(allergies.Any(a => a.Allergen == "Penicillin"), Is.True);
        Assert.That(allergies.Any(a => a.Allergen == "Shellfish"), Is.True);
    }

    // ─── Patient Linkage ─────────────────────────────────────────────────

    [Test]
    public async Task RecordAllergy_EmbeddedOnPatient()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        await w.RecordAllergyAsync("Ibuprofen", "Drug", null, "O",
            new List<string> { "GI Upset" }, "Mild", null, null, null);

        List<AllergyEntry> entries = await GetPatient(patientId).GetAllergiesAsync();
        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(entries[0].AllergyId, Does.StartWith("ALLERGY-"));
    }

    // ─── Grain State Access ───────────────────────────────────────────────

    [Test]
    public async Task RecordAllergy_OriginatorNameStored()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        await w.RecordAllergyAsync(
            "Erythromycin", "Drug", null, "O",
            new List<string> { "Nausea" }, "Mild",
            "PROV-007", "Dr. Martinez", null);

        List<AllergyEntry> entries = await GetPatient(patientId).GetAllergiesAsync();
        Assert.That(entries[0].OriginatorName, Is.EqualTo("Dr. Martinez"));
        Assert.That(entries[0].OriginatorId, Is.EqualTo("PROV-007"));
    }

    [Test]
    public async Task RecordAllergy_CommentsStored()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        await w.RecordAllergyAsync(
            "Contrast Dye", "Other", null, "H",
            new List<string> { "Flushing" }, "Mild",
            null, null, "Patient reported reaction during CT scan in 2019");

        List<AllergyEntry> entries = await GetPatient(patientId).GetAllergiesAsync();
        Assert.That(entries[0].Comments, Does.Contain("CT scan"));
    }

    [Test]
    public async Task RecordMultipleAllergies_AllInList()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        await w.RecordAllergyAsync("ACE Inhibitors", "Drug", null, "O",
            new List<string> { "Cough" }, "Mild", null, null, null);
        await w.RecordAllergyAsync("Tetracycline", "Drug", null, "H",
            new List<string> { "Photosensitivity" }, "Moderate", null, null, null);
        await w.RecordAllergyAsync("Tree Nuts", "Food", null, "O",
            new List<string> { "Throat Swelling" }, "Severe", null, null, null);

        List<AllergySummary> allergies = await w.GetAllergiesAsync();
        Assert.That(allergies, Has.Count.EqualTo(3));
    }

    // ─── Isolation ────────────────────────────────────────────────────────

    [Test]
    public async Task MultiplePatients_AllergiesAreIndependent()
    {
        IPatientWorkflowGrain w1 = NewWorkflow();
        IPatientWorkflowGrain w2 = NewWorkflow();

        await w1.RecordAllergyAsync("Morphine", "Drug", null, "O",
            new List<string> { "Confusion" }, "Moderate", null, null, null);

        List<AllergySummary> allergies2 = await w2.GetAllergiesAsync();
        Assert.That(allergies2, Is.Empty);
    }

    // ─── Full Workflow ────────────────────────────────────────────────────

    [Test]
    public async Task FullWorkflow_RecordMultipleAllergies_SummaryMatchesInput()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        string id1 = await w.RecordAllergyAsync(
            "Penicillin", "Drug", null, "O",
            new List<string> { "Rash", "Urticaria" }, "Moderate", null, null, null);
        string id2 = await w.RecordAllergyAsync(
            "Sulfa", "Drug", null, "H",
            new List<string> { "Stevens-Johnson Syndrome" }, "Severe", null, null, null);

        List<AllergySummary> allergies = await w.GetAllergiesAsync();
        Assert.That(allergies, Has.Count.EqualTo(2));

        AllergySummary? pen = allergies.FirstOrDefault(a => a.AllergyId == id1);
        Assert.That(pen, Is.Not.Null);
        Assert.That(pen!.Allergen, Is.EqualTo("Penicillin"));
        Assert.That(pen.Severity, Is.EqualTo("Moderate"));
        Assert.That(pen.Reactions, Has.Count.EqualTo(2));

        AllergySummary? sulfa = allergies.FirstOrDefault(a => a.AllergyId == id2);
        Assert.That(sulfa, Is.Not.Null);
        Assert.That(sulfa!.Severity, Is.EqualTo("Severe"));
    }

    [Test]
    public async Task GetAllergies_IncludesAllergenTypeAndObservedHistorical()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        await w.RecordAllergyAsync("Peanuts", "Food", null, "O",
            new List<string> { "Anaphylaxis" }, "Severe", null, null, null);

        List<AllergySummary> allergies = await w.GetAllergiesAsync();
        Assert.That(allergies[0].AllergenType, Is.EqualTo("Food"));
        Assert.That(allergies[0].ObservedHistorical, Is.EqualTo("O"));
    }
}
