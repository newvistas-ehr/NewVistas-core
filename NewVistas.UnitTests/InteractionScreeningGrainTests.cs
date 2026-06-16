// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.TestingHost;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for InteractionScreening and InteractionScreeningIndex grains.
/// Tests grain-level behavior directly without the workflow grain.
/// </summary>
[TestFixture]
public class InteractionScreeningGrainTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ─── Screening Grain Tests ──────────────────────────────────────────────

    [Test]
    public async Task ScreeningGrain_NoInteractions_StatusCleared()
    {
        string id = $"IXSCREEN:RX-{Guid.NewGuid()}";
        IInteractionScreeningGrain grain = _cluster.GrainFactory.GetGrain<IInteractionScreeningGrain>(id);

        await grain.ScreenAsync("RX-001", "PATIENT-001", "METFORMIN 500MG", "PHARM-001",
            new List<InteractionFinding>());

        InteractionScreeningState state = await grain.GetAsync();

        Assert.That(state.ScreeningId, Is.EqualTo(id));
        Assert.That(state.PrescriptionId, Is.EqualTo("RX-001"));
        Assert.That(state.PatientId, Is.EqualTo("PATIENT-001"));
        Assert.That(state.DrugName, Is.EqualTo("METFORMIN 500MG"));
        Assert.That(state.Status, Is.EqualTo(InteractionScreeningStatus.Cleared));
        Assert.That(state.TotalInteractionCount, Is.EqualTo(0));
        Assert.That(state.BlockingCount, Is.EqualTo(0));
    }

    [Test]
    public async Task ScreeningGrain_NonBlockingInteractions_StatusCleared()
    {
        string id = $"IXSCREEN:RX-{Guid.NewGuid()}";
        IInteractionScreeningGrain grain = _cluster.GrainFactory.GetGrain<IInteractionScreeningGrain>(id);

        List<InteractionFinding> findings = new()
        {
            new InteractionFinding
            {
                Drug1Name = "WARFARIN", Drug1IngredientIen = "1", Drug1IngredientName = "WARFARIN SODIUM",
                Drug2Name = "ASPIRIN", Drug2IngredientIen = "2", Drug2IngredientName = "ASPIRIN",
                Severity = InteractionSeverity.Minor, Description = "Minor interaction.",
                IsBlocking = false
            },
            new InteractionFinding
            {
                Drug1Name = "WARFARIN", Drug1IngredientIen = "1", Drug1IngredientName = "WARFARIN SODIUM",
                Drug2Name = "IBUPROFEN", Drug2IngredientIen = "3", Drug2IngredientName = "IBUPROFEN",
                Severity = InteractionSeverity.Moderate, Description = "Moderate interaction.",
                IsBlocking = false
            }
        };

        await grain.ScreenAsync("RX-002", "PATIENT-002", "WARFARIN 5MG", "PHARM-001", findings);

        InteractionScreeningState state = await grain.GetAsync();

        Assert.That(state.Status, Is.EqualTo(InteractionScreeningStatus.Cleared));
        Assert.That(state.TotalInteractionCount, Is.EqualTo(2));
        Assert.That(state.BlockingCount, Is.EqualTo(0));
    }

    [Test]
    public async Task ScreeningGrain_BlockingInteractions_StatusBlocked()
    {
        string id = $"IXSCREEN:RX-{Guid.NewGuid()}";
        IInteractionScreeningGrain grain = _cluster.GrainFactory.GetGrain<IInteractionScreeningGrain>(id);

        List<InteractionFinding> findings = new()
        {
            new InteractionFinding
            {
                Drug1Name = "WARFARIN", Drug1IngredientIen = "1", Drug1IngredientName = "WARFARIN SODIUM",
                Drug2Name = "METRONIDAZOLE", Drug2IngredientIen = "4", Drug2IngredientName = "METRONIDAZOLE",
                Severity = InteractionSeverity.Significant, Description = "Significantly increases INR.",
                IsBlocking = true
            }
        };

        await grain.ScreenAsync("RX-003", "PATIENT-003", "WARFARIN 5MG", "PHARM-001", findings);

        InteractionScreeningState state = await grain.GetAsync();

        Assert.That(state.Status, Is.EqualTo(InteractionScreeningStatus.BlockedPendingOverride));
        Assert.That(state.BlockingCount, Is.EqualTo(1));
        Assert.That(state.TotalInteractionCount, Is.EqualTo(1));
    }

    [Test]
    public async Task ScreeningGrain_ContraindicatedInteraction_StatusBlocked()
    {
        string id = $"IXSCREEN:RX-{Guid.NewGuid()}";
        IInteractionScreeningGrain grain = _cluster.GrainFactory.GetGrain<IInteractionScreeningGrain>(id);

        List<InteractionFinding> findings = new()
        {
            new InteractionFinding
            {
                Drug1Name = "LINEZOLID", Drug1IngredientIen = "10", Drug1IngredientName = "LINEZOLID",
                Drug2Name = "SELEGILINE", Drug2IngredientIen = "11", Drug2IngredientName = "SELEGILINE",
                Severity = InteractionSeverity.Contraindicated,
                Description = "Contraindicated: serotonin syndrome risk.",
                ClinicalEffects = "Potentially fatal serotonin syndrome",
                IsBlocking = true
            }
        };

        await grain.ScreenAsync("RX-004", "PATIENT-004", "LINEZOLID 600MG", "PHARM-001", findings);

        InteractionScreeningState state = await grain.GetAsync();

        Assert.That(state.Status, Is.EqualTo(InteractionScreeningStatus.BlockedPendingOverride));
        Assert.That(state.Findings[0].Severity, Is.EqualTo(InteractionSeverity.Contraindicated));
        Assert.That(state.Findings[0].ClinicalEffects, Does.Contain("serotonin"));
    }

    [Test]
    public async Task ScreeningGrain_Override_SingleBlocking_StatusOverridden()
    {
        string id = $"IXSCREEN:RX-{Guid.NewGuid()}";
        IInteractionScreeningGrain grain = _cluster.GrainFactory.GetGrain<IInteractionScreeningGrain>(id);

        List<InteractionFinding> findings = new()
        {
            new InteractionFinding
            {
                Drug1Name = "DRUG A", Drug1IngredientIen = "20", Drug1IngredientName = "INGREDIENT A",
                Drug2Name = "DRUG B", Drug2IngredientIen = "21", Drug2IngredientName = "INGREDIENT B",
                Severity = InteractionSeverity.Significant, Description = "Significant.",
                IsBlocking = true
            }
        };

        await grain.ScreenAsync("RX-005", "PATIENT-005", "DRUG A", "PHARM-001", findings);
        Assert.That((await grain.GetAsync()).Status, Is.EqualTo(InteractionScreeningStatus.BlockedPendingOverride));

        await grain.OverrideInteractionAsync(0, "PHARM-SENIOR", "Benefit outweighs risk; will monitor INR closely.");

        InteractionScreeningState state = await grain.GetAsync();

        Assert.That(state.Status, Is.EqualTo(InteractionScreeningStatus.OverriddenByPharmacist));
        Assert.That(state.Findings[0].IsOverridden, Is.True);
        Assert.That(state.Findings[0].OverriddenBy, Is.EqualTo("PHARM-SENIOR"));
        Assert.That(state.Findings[0].OverrideReason, Does.Contain("monitor INR"));
        Assert.That(state.Findings[0].OverrideDate, Is.Not.Null);
        Assert.That(state.OverriddenCount, Is.EqualTo(1));
    }

    [Test]
    public async Task ScreeningGrain_Override_MultipleBlocking_PartialThenFull()
    {
        string id = $"IXSCREEN:RX-{Guid.NewGuid()}";
        IInteractionScreeningGrain grain = _cluster.GrainFactory.GetGrain<IInteractionScreeningGrain>(id);

        List<InteractionFinding> findings = new()
        {
            new InteractionFinding
            {
                Drug1Name = "DRUG X", Drug1IngredientIen = "30", Drug1IngredientName = "INGR X",
                Drug2Name = "DRUG Y", Drug2IngredientIen = "31", Drug2IngredientName = "INGR Y",
                Severity = InteractionSeverity.Significant, Description = "Interaction 1.",
                IsBlocking = true
            },
            new InteractionFinding
            {
                Drug1Name = "DRUG X", Drug1IngredientIen = "30", Drug1IngredientName = "INGR X",
                Drug2Name = "DRUG Z", Drug2IngredientIen = "32", Drug2IngredientName = "INGR Z",
                Severity = InteractionSeverity.Contraindicated, Description = "Interaction 2.",
                IsBlocking = true
            }
        };

        await grain.ScreenAsync("RX-006", "PATIENT-006", "DRUG X", "PHARM-001", findings);

        // Override first only — should remain blocked
        await grain.OverrideInteractionAsync(0, "PHARM-001", "Justified.");
        InteractionScreeningState state = await grain.GetAsync();
        Assert.That(state.Status, Is.EqualTo(InteractionScreeningStatus.BlockedPendingOverride));
        Assert.That(state.OverriddenCount, Is.EqualTo(1));

        // Override second — should now be fully overridden
        await grain.OverrideInteractionAsync(1, "PHARM-001", "Also justified.");
        state = await grain.GetAsync();
        Assert.That(state.Status, Is.EqualTo(InteractionScreeningStatus.OverriddenByPharmacist));
        Assert.That(state.OverriddenCount, Is.EqualTo(2));
    }

    [Test]
    public async Task ScreeningGrain_Override_NonBlocking_DoesNothing()
    {
        string id = $"IXSCREEN:RX-{Guid.NewGuid()}";
        IInteractionScreeningGrain grain = _cluster.GrainFactory.GetGrain<IInteractionScreeningGrain>(id);

        List<InteractionFinding> findings = new()
        {
            new InteractionFinding
            {
                Drug1Name = "A", Drug1IngredientIen = "40", Drug1IngredientName = "A",
                Drug2Name = "B", Drug2IngredientIen = "41", Drug2IngredientName = "B",
                Severity = InteractionSeverity.Minor, Description = "Minor.",
                IsBlocking = false
            }
        };

        await grain.ScreenAsync("RX-007", "PATIENT-007", "A", "PHARM-001", findings);
        await grain.OverrideInteractionAsync(0, "PHARM-001", "Trying.");

        InteractionScreeningState state = await grain.GetAsync();
        Assert.That(state.Findings[0].IsOverridden, Is.False);
    }

    [Test]
    public async Task ScreeningGrain_Override_InvalidIndex_DoesNothing()
    {
        string id = $"IXSCREEN:RX-{Guid.NewGuid()}";
        IInteractionScreeningGrain grain = _cluster.GrainFactory.GetGrain<IInteractionScreeningGrain>(id);

        await grain.ScreenAsync("RX-008", "PATIENT-008", "DRUG", "PHARM-001", new List<InteractionFinding>());
        await grain.OverrideInteractionAsync(99, "PHARM-001", "Bad index.");

        InteractionScreeningState state = await grain.GetAsync();
        Assert.That(state.Status, Is.EqualTo(InteractionScreeningStatus.Cleared));
    }

    [Test]
    public async Task ScreeningGrain_AddNotes_AppendsToExisting()
    {
        string id = $"IXSCREEN:RX-{Guid.NewGuid()}";
        IInteractionScreeningGrain grain = _cluster.GrainFactory.GetGrain<IInteractionScreeningGrain>(id);

        await grain.ScreenAsync("RX-009", "PATIENT-009", "DRUG", "PHARM-001", new List<InteractionFinding>());
        await grain.AddNotesAsync("First note.");
        await grain.AddNotesAsync("Second note.");

        InteractionScreeningState state = await grain.GetAsync();
        Assert.That(state.Notes, Does.Contain("First note."));
        Assert.That(state.Notes, Does.Contain("Second note."));
    }

    // ─── Index Grain Tests ──────────────────────────────────────────────────

    [Test]
    public async Task IndexGrain_AddAndGetAll_ReturnsMostRecentFirst()
    {
        string indexKey = $"IXSCREEN-IDX:PATIENT-{Guid.NewGuid()}";
        IInteractionScreeningIndexGrain index = _cluster.GrainFactory.GetGrain<IInteractionScreeningIndexGrain>(indexKey);

        string id1 = $"IXSCREEN:RX-{Guid.NewGuid()}";
        string id2 = $"IXSCREEN:RX-{Guid.NewGuid()}";

        await index.AddEntryAsync(new InteractionScreeningIndexEntry { ScreeningId = id1, PrescriptionId = "RX-A", DrugName = "Drug A", Status = InteractionScreeningStatus.Cleared });
        await index.AddEntryAsync(new InteractionScreeningIndexEntry { ScreeningId = id2, PrescriptionId = "RX-B", DrugName = "Drug B", Status = InteractionScreeningStatus.BlockedPendingOverride, BlockingCount = 1 });

        List<InteractionScreeningIndexEntry> all = await index.GetAllAsync();
        Assert.That(all, Has.Count.EqualTo(2));
        Assert.That(all[0].ScreeningId, Is.EqualTo(id2));
    }

    [Test]
    public async Task IndexGrain_GetBlocked_FiltersCorrectly()
    {
        string indexKey = $"IXSCREEN-IDX:PATIENT-{Guid.NewGuid()}";
        IInteractionScreeningIndexGrain index = _cluster.GrainFactory.GetGrain<IInteractionScreeningIndexGrain>(indexKey);

        await index.AddEntryAsync(new InteractionScreeningIndexEntry { ScreeningId = "S1", PrescriptionId = "RX-1", DrugName = "Clear Drug", Status = InteractionScreeningStatus.Cleared });
        await index.AddEntryAsync(new InteractionScreeningIndexEntry { ScreeningId = "S2", PrescriptionId = "RX-2", DrugName = "Blocked Drug", Status = InteractionScreeningStatus.BlockedPendingOverride, BlockingCount = 2 });

        List<InteractionScreeningIndexEntry> blocked = await index.GetBlockedAsync();
        Assert.That(blocked, Has.Count.EqualTo(1));
        Assert.That(blocked[0].DrugName, Is.EqualTo("Blocked Drug"));
    }

    [Test]
    public async Task IndexGrain_GetByPrescription_ReturnsMatch()
    {
        string indexKey = $"IXSCREEN-IDX:PATIENT-{Guid.NewGuid()}";
        IInteractionScreeningIndexGrain index = _cluster.GrainFactory.GetGrain<IInteractionScreeningIndexGrain>(indexKey);

        await index.AddEntryAsync(new InteractionScreeningIndexEntry { ScreeningId = "S3", PrescriptionId = "RX-UNIQUE", DrugName = "ASPIRIN" });

        InteractionScreeningIndexEntry? found = await index.GetByPrescriptionAsync("RX-UNIQUE");
        Assert.That(found, Is.Not.Null);
        Assert.That(found!.DrugName, Is.EqualTo("ASPIRIN"));

        InteractionScreeningIndexEntry? notFound = await index.GetByPrescriptionAsync("RX-NOPE");
        Assert.That(notFound, Is.Null);
    }

    [Test]
    public async Task IndexGrain_ReScreen_ReplacesExistingEntry()
    {
        string indexKey = $"IXSCREEN-IDX:PATIENT-{Guid.NewGuid()}";
        IInteractionScreeningIndexGrain index = _cluster.GrainFactory.GetGrain<IInteractionScreeningIndexGrain>(indexKey);

        await index.AddEntryAsync(new InteractionScreeningIndexEntry { ScreeningId = "S4-OLD", PrescriptionId = "RX-RESCAN", DrugName = "OLD", Status = InteractionScreeningStatus.BlockedPendingOverride });
        await index.AddEntryAsync(new InteractionScreeningIndexEntry { ScreeningId = "S4-NEW", PrescriptionId = "RX-RESCAN", DrugName = "NEW", Status = InteractionScreeningStatus.Cleared });

        List<InteractionScreeningIndexEntry> all = await index.GetAllAsync();
        Assert.That(all.Count(e => e.PrescriptionId == "RX-RESCAN"), Is.EqualTo(1));
        Assert.That(all.First(e => e.PrescriptionId == "RX-RESCAN").ScreeningId, Is.EqualTo("S4-NEW"));
    }

    [Test]
    public async Task IndexGrain_UpdateEntry_UpdatesCorrectly()
    {
        string indexKey = $"IXSCREEN-IDX:PATIENT-{Guid.NewGuid()}";
        IInteractionScreeningIndexGrain index = _cluster.GrainFactory.GetGrain<IInteractionScreeningIndexGrain>(indexKey);

        await index.AddEntryAsync(new InteractionScreeningIndexEntry { ScreeningId = "S5", PrescriptionId = "RX-UPD", Status = InteractionScreeningStatus.BlockedPendingOverride, BlockingCount = 2, TotalInteractionCount = 3 });

        await index.UpdateEntryAsync("S5", InteractionScreeningStatus.OverriddenByPharmacist, 0, 3);

        List<InteractionScreeningIndexEntry> all = await index.GetAllAsync();
        InteractionScreeningIndexEntry entry = all.First(e => e.ScreeningId == "S5");
        Assert.That(entry.Status, Is.EqualTo(InteractionScreeningStatus.OverriddenByPharmacist));
        Assert.That(entry.BlockingCount, Is.EqualTo(0));
    }
}
