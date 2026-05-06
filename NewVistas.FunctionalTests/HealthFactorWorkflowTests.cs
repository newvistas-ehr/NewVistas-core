// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for VistA V HEALTH FACTORS — File #9000010.23.
/// Tests Health Factor grain methods directly for recording, severity,
/// categories, values, resolution lifecycle, history tracking, and full workflows.
/// </summary>
[TestFixture]
public class HealthFactorWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IHealthFactorGrain NewHealthFactorGrain()
        => _cluster.GrainFactory.GetGrain<IHealthFactorGrain>($"HF-{Guid.NewGuid():N}");

    private async Task RecordStandardAsync(IHealthFactorGrain grain, string patientId = "PAT-001", string name = "CURRENT SMOKER")
    {
        await grain.RecordHealthFactorAsync(
            patientId, name, "DEF-TOBACCO-001",
            "TOBACCO", DateTime.UtcNow, "MODERATE",
            "VISIT-001", "LOC-001", "Primary Care Clinic",
            "PROV-001", "Dr. Smith", "Patient reports smoking 1 pack/day");
    }

    // ─── 1. Record Health Factor ──────────────────────────────────────────────

    [Test]
    public async Task HealthFactor_CanRecordHealthFactor()
    {
        // Arrange
        IHealthFactorGrain grain = NewHealthFactorGrain();

        // Act
        await RecordStandardAsync(grain);
        HealthFactorState state = await grain.GetHealthFactorAsync();

        // Assert
        Assert.That(state.PatientId, Is.EqualTo("PAT-001"));
        Assert.That(state.HealthFactorName, Is.EqualTo("CURRENT SMOKER"));
        Assert.That(state.Category, Is.EqualTo("TOBACCO"));
        Assert.That(state.LevelSeverity, Is.EqualTo("MODERATE"));
        Assert.That(state.VisitId, Is.EqualTo("VISIT-001"));
        Assert.That(state.EnteredByName, Is.EqualTo("Dr. Smith"));
    }

    // ─── 2. Get Health Factor ─────────────────────────────────────────────────

    [Test]
    public async Task HealthFactor_CanGetHealthFactor()
    {
        // Arrange
        IHealthFactorGrain grain = NewHealthFactorGrain();
        await RecordStandardAsync(grain, name: "ALCOHOL USE");

        // Act
        HealthFactorState state = await grain.GetHealthFactorAsync();

        // Assert
        Assert.That(state.HealthFactorName, Is.EqualTo("ALCOHOL USE"));
        Assert.That(state.HealthFactorId, Is.Not.Empty);
    }

    // ─── 3. Update Severity ───────────────────────────────────────────────────

    [Test]
    public async Task HealthFactor_CanUpdateSeverity()
    {
        // Arrange
        IHealthFactorGrain grain = NewHealthFactorGrain();
        await RecordStandardAsync(grain);

        // Act
        await grain.UpdateSeverityAsync("MODERATE");
        HealthFactorState state = await grain.GetHealthFactorAsync();

        // Assert
        Assert.That(state.LevelSeverity, Is.EqualTo("MODERATE"));
    }

    // ─── 4. Set Category ──────────────────────────────────────────────────────

    [Test]
    public async Task HealthFactor_CanSetCategory()
    {
        // Arrange
        IHealthFactorGrain grain = NewHealthFactorGrain();
        await RecordStandardAsync(grain);

        // Act
        await grain.SetCategoryAsync("ALCOHOL", "HEAVY DRINKER");
        HealthFactorState state = await grain.GetHealthFactorAsync();

        // Assert
        Assert.That(state.Category, Is.EqualTo("ALCOHOL"));
        Assert.That(state.Subcategory, Is.EqualTo("HEAVY DRINKER"));
    }

    // ─── 5. Set Category Without Subcategory ─────────────────────────────────

    [Test]
    public async Task HealthFactor_CanSetCategoryWithoutSubcategory()
    {
        // Arrange
        IHealthFactorGrain grain = NewHealthFactorGrain();
        await RecordStandardAsync(grain);

        // Act
        await grain.SetCategoryAsync("EXERCISE", null);
        HealthFactorState state = await grain.GetHealthFactorAsync();

        // Assert
        Assert.That(state.Category, Is.EqualTo("EXERCISE"));
        Assert.That(state.Subcategory, Is.Null);
    }

    // ─── 6. Set Value ─────────────────────────────────────────────────────────

    [Test]
    public async Task HealthFactor_CanSetValue()
    {
        // Arrange
        IHealthFactorGrain grain = NewHealthFactorGrain();
        await RecordStandardAsync(grain);

        // Act
        await grain.SetValueAsync("2 packs/day", "2");
        HealthFactorState state = await grain.GetHealthFactorAsync();

        // Assert
        Assert.That(state.Value, Is.EqualTo("2 packs/day"));
        Assert.That(state.Magnitude, Is.EqualTo("2"));
    }

    // ─── 7. Link To Visit ────────────────────────────────────────────────────

    [Test]
    public async Task HealthFactor_CanLinkToVisit()
    {
        // Arrange
        IHealthFactorGrain grain = NewHealthFactorGrain();
        await RecordStandardAsync(grain);

        // Act
        await grain.LinkToVisitAsync("VISIT-999");
        HealthFactorState state = await grain.GetHealthFactorAsync();

        // Assert
        Assert.That(state.VisitId, Is.EqualTo("VISIT-999"));
    }

    // ─── 8. Add Comment ──────────────────────────────────────────────────────

    [Test]
    public async Task HealthFactor_CanAddComment()
    {
        // Arrange
        IHealthFactorGrain grain = NewHealthFactorGrain();
        await RecordStandardAsync(grain);

        // Act
        await grain.AddCommentAsync("Patient counseled on smoking cessation");
        HealthFactorState state = await grain.GetHealthFactorAsync();

        // Assert
        Assert.That(state.Comments, Is.EqualTo("Patient counseled on smoking cessation"));
    }

    // ─── 9. Resolve ──────────────────────────────────────────────────────────

    [Test]
    public async Task HealthFactor_CanResolve()
    {
        // Arrange
        IHealthFactorGrain grain = NewHealthFactorGrain();
        await RecordStandardAsync(grain);

        // Act
        await grain.ResolveAsync("Dr. Smith");
        HealthFactorState state = await grain.GetHealthFactorAsync();

        // Assert
        Assert.That(state.EvaluationStatus, Is.EqualTo("RESOLVED"));
        Assert.That(state.ResolutionDate, Is.Not.Null);
        Assert.That(state.ResolvedByName, Is.EqualTo("Dr. Smith"));
    }

    // ─── 10. Reactivate ──────────────────────────────────────────────────────

    [Test]
    public async Task HealthFactor_CanReactivate()
    {
        // Arrange
        IHealthFactorGrain grain = NewHealthFactorGrain();
        await RecordStandardAsync(grain);
        await grain.ResolveAsync("Dr. Smith");
        HealthFactorState resolved = await grain.GetHealthFactorAsync();
        Assert.That(resolved.EvaluationStatus, Is.EqualTo("RESOLVED"));

        // Act
        await grain.ReactivateAsync();
        HealthFactorState state = await grain.GetHealthFactorAsync();

        // Assert
        Assert.That(state.EvaluationStatus, Is.EqualTo("CURRENT"));
        Assert.That(state.ResolutionDate, Is.Null);
        Assert.That(state.ResolvedByName, Is.Null);
    }

    // ─── 11. Add History Entry ───────────────────────────────────────────────

    [Test]
    public async Task HealthFactor_CanAddHistoryEntry()
    {
        // Arrange
        IHealthFactorGrain grain = NewHealthFactorGrain();
        await RecordStandardAsync(grain);

        // Act
        await grain.AddHistoryEntryAsync("1 pack/day", "MODERATE", "Unchanged from last visit", "Dr. Smith");
        List<HealthFactorHistoryEntry> history = await grain.GetHistoryAsync();

        // Assert
        Assert.That(history, Has.Count.EqualTo(1));
        Assert.That(history[0].Value, Is.EqualTo("1 pack/day"));
        Assert.That(history[0].SeverityLevel, Is.EqualTo("MODERATE"));
        Assert.That(history[0].Comment, Is.EqualTo("Unchanged from last visit"));
        Assert.That(history[0].RecordedByName, Is.EqualTo("Dr. Smith"));
        Assert.That(history[0].EntryDate, Is.Not.EqualTo(default(DateTime)));
    }

    // ─── 12. Add Multiple History Entries ────────────────────────────────────

    [Test]
    public async Task HealthFactor_CanAddMultipleHistoryEntries()
    {
        // Arrange
        IHealthFactorGrain grain = NewHealthFactorGrain();
        await RecordStandardAsync(grain);

        // Act
        await grain.AddHistoryEntryAsync("2 packs/day", "SEVERE", "Increased use", "Dr. Smith");
        await grain.AddHistoryEntryAsync("1.5 packs/day", "MODERATE", "Slight decrease", "Dr. Jones");
        await grain.AddHistoryEntryAsync("1 pack/day", "MODERATE", "Continuing decrease", "Dr. Smith");
        List<HealthFactorHistoryEntry> history = await grain.GetHistoryAsync();

        // Assert
        Assert.That(history, Has.Count.EqualTo(3));
        Assert.That(history[0].Value, Is.EqualTo("2 packs/day"));
        Assert.That(history[1].Value, Is.EqualTo("1.5 packs/day"));
        Assert.That(history[2].Value, Is.EqualTo("1 pack/day"));
    }

    // ─── 13. Get History ─────────────────────────────────────────────────────

    [Test]
    public async Task HealthFactor_GetHistory()
    {
        // Arrange
        IHealthFactorGrain grain = NewHealthFactorGrain();
        await RecordStandardAsync(grain);
        await grain.AddHistoryEntryAsync("Baseline", "MINIMAL", "Initial assessment", "Dr. Lee");
        await grain.AddHistoryEntryAsync("Worsening", "SEVERE", "Patient relapsed", "Dr. Lee");

        // Act
        List<HealthFactorHistoryEntry> history = await grain.GetHistoryAsync();

        // Assert
        Assert.That(history, Has.Count.EqualTo(2));
        Assert.That(history[0].Value, Is.EqualTo("Baseline"));
        Assert.That(history[1].Value, Is.EqualTo("Worsening"));
        Assert.That(history[1].SeverityLevel, Is.EqualTo("SEVERE"));
    }

    // ─── 14. Severity Levels ─────────────────────────────────────────────────

    [Test]
    public async Task HealthFactor_SeverityLevels()
    {
        // Arrange
        IHealthFactorGrain grain = NewHealthFactorGrain();
        await RecordStandardAsync(grain);

        // Act & Assert — MINIMAL
        await grain.UpdateSeverityAsync("MINIMAL");
        HealthFactorState s1 = await grain.GetHealthFactorAsync();
        Assert.That(s1.LevelSeverity, Is.EqualTo("MINIMAL"));

        // Act & Assert — MODERATE
        await grain.UpdateSeverityAsync("MODERATE");
        HealthFactorState s2 = await grain.GetHealthFactorAsync();
        Assert.That(s2.LevelSeverity, Is.EqualTo("MODERATE"));

        // Act & Assert — SEVERE
        await grain.UpdateSeverityAsync("SEVERE");
        HealthFactorState s3 = await grain.GetHealthFactorAsync();
        Assert.That(s3.LevelSeverity, Is.EqualTo("SEVERE"));
    }

    // ─── 15. Value And Magnitude ─────────────────────────────────────────────

    [Test]
    public async Task HealthFactor_ValueAndMagnitude()
    {
        // Arrange
        IHealthFactorGrain grain = NewHealthFactorGrain();
        await RecordStandardAsync(grain);

        // Act
        await grain.SetValueAsync("3 drinks/week", "3");
        HealthFactorState state = await grain.GetHealthFactorAsync();

        // Assert
        Assert.That(state.Value, Is.EqualTo("3 drinks/week"));
        Assert.That(state.Magnitude, Is.EqualTo("3"));
    }

    // ─── 16. Full Workflow ───────────────────────────────────────────────────

    [Test]
    public async Task HealthFactor_FullWorkflow()
    {
        // Arrange
        IHealthFactorGrain grain = NewHealthFactorGrain();

        // Record health factor
        await RecordStandardAsync(grain, name: "TOBACCO USE");
        HealthFactorState s1 = await grain.GetHealthFactorAsync();
        Assert.That(s1.HealthFactorName, Is.EqualTo("TOBACCO USE"));

        // Set category
        await grain.SetCategoryAsync("TOBACCO", "CURRENT SMOKER");

        // Set value
        await grain.SetValueAsync("2 packs/day", "2");

        // Update severity
        await grain.UpdateSeverityAsync("SEVERE");

        // Link to visit
        await grain.LinkToVisitAsync("VISIT-100");

        // Add history entries
        await grain.AddHistoryEntryAsync("2 packs/day", "SEVERE", "Initial assessment", "Dr. Smith");
        await grain.AddHistoryEntryAsync("1.5 packs/day", "MODERATE", "Using nicotine patch", "Dr. Smith");

        // Add comment
        await grain.AddCommentAsync("Patient enrolled in cessation program");

        // Resolve
        await grain.ResolveAsync("Dr. Smith");
        HealthFactorState resolved = await grain.GetHealthFactorAsync();
        Assert.That(resolved.EvaluationStatus, Is.EqualTo("RESOLVED"));
        Assert.That(resolved.ResolutionDate, Is.Not.Null);

        // Reactivate
        await grain.ReactivateAsync();

        // Final assertions
        HealthFactorState final_state = await grain.GetHealthFactorAsync();
        Assert.That(final_state.EvaluationStatus, Is.EqualTo("CURRENT"));
        Assert.That(final_state.ResolutionDate, Is.Null);
        Assert.That(final_state.Category, Is.EqualTo("TOBACCO"));
        Assert.That(final_state.Subcategory, Is.EqualTo("CURRENT SMOKER"));
        Assert.That(final_state.Value, Is.EqualTo("2 packs/day"));
        Assert.That(final_state.Magnitude, Is.EqualTo("2"));
        Assert.That(final_state.LevelSeverity, Is.EqualTo("SEVERE"));
        Assert.That(final_state.VisitId, Is.EqualTo("VISIT-100"));
        Assert.That(final_state.Comments, Is.EqualTo("Patient enrolled in cessation program"));
        Assert.That(final_state.History, Has.Count.EqualTo(2));
    }
}
