// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Drug Interaction Blocking workflow methods.
///
/// VistA reference: DRGINT.m integration in PSO fill workflow (PSOORED.m).
///
/// Connects the existing IDrugInteractionCheckerGrain to the pharmacy
/// fill/refill pipeline. Screens a prescription's ingredients against all
/// active medications. Significant and Contraindicated interactions block
/// fill/refill until a pharmacist overrides each with a documented reason.
///
/// This closes the Critical gap: "IDrugInteractionCheckerGrain exists but
/// is NOT connected to fill/refill/verify endpoints."
/// </summary>
public partial class PatientWorkflowGrain
{
    // ─── Interaction Screening grain helpers ─────────────────────────────────

    private IInteractionScreeningGrain InteractionScreening(string screeningId)
        => GrainFactory.GetGrain<IInteractionScreeningGrain>(screeningId);

    private IInteractionScreeningIndexGrain InteractionScreeningIndex()
        => GrainFactory.GetGrain<IInteractionScreeningIndexGrain>($"IXSCREEN-IDX:{PatientId}");

    private IDrugInteractionCheckerGrain InteractionChecker()
        => GrainFactory.GetGrain<IDrugInteractionCheckerGrain>("CHECKER");

    // ─── Interaction Blocking Workflow Methods ───────────────────────────────

    public async Task<string> ScreenPrescriptionForInteractionsAsync(
        string prescriptionId,
        string drugName,
        List<DrugIngredient> newDrugIngredients,
        List<DrugIngredient> existingMedicationIngredients,
        string? screenedBy)
    {
        // Combine all ingredients for pairwise checking
        List<DrugIngredient> allIngredients = new();
        allIngredients.AddRange(newDrugIngredients);
        allIngredients.AddRange(existingMedicationIngredients);

        // Run the existing interaction checker
        List<DrugInteractionResult> results = await InteractionChecker()
            .CheckInteractionsAsync(allIngredients);

        // Filter to only interactions that involve at least one new drug ingredient
        HashSet<string> newIens = newDrugIngredients
            .Select(i => i.IngredientIen)
            .Where(ien => !string.IsNullOrEmpty(ien))
            .ToHashSet();

        List<DrugInteractionResult> relevantInteractions = results
            .Where(r => newIens.Contains(r.Drug1.IngredientIen)
                     || newIens.Contains(r.Drug2.IngredientIen))
            .ToList();

        // Convert to InteractionFinding objects
        List<InteractionFinding> findings = relevantInteractions.Select(r =>
        {
            // Determine which drug is the new one and which is existing
            bool drug1IsNew = newIens.Contains(r.Drug1.IngredientIen);
            DrugIngredient newDrug = drug1IsNew ? r.Drug1 : r.Drug2;
            DrugIngredient existingDrug = drug1IsNew ? r.Drug2 : r.Drug1;

            bool isBlocking = r.Interaction.Severity >= InteractionSeverity.Significant;

            return new InteractionFinding
            {
                Drug1Name = drugName,
                Drug1IngredientIen = newDrug.IngredientIen,
                Drug1IngredientName = newDrug.Name,
                Drug2Name = existingDrug.Name,
                Drug2IngredientIen = existingDrug.IngredientIen,
                Drug2IngredientName = existingDrug.Name,
                Severity = r.Interaction.Severity,
                Description = r.Interaction.Description,
                ClinicalEffects = r.Interaction.ClinicalEffects,
                Mechanism = r.Interaction.Mechanism,
                Management = r.Interaction.Management,
                IsBlocking = isBlocking,
                IsOverridden = false,
            };
        }).ToList();

        // Create the screening grain
        string screeningId = $"IXSCREEN:{prescriptionId}";
        IInteractionScreeningGrain grain = InteractionScreening(screeningId);
        await grain.ScreenAsync(prescriptionId, PatientId, drugName, screenedBy, findings);

        InteractionScreeningState state = await grain.GetAsync();

        // Update index
        await InteractionScreeningIndex().AddEntryAsync(new InteractionScreeningIndexEntry
        {
            ScreeningId = screeningId,
            PrescriptionId = prescriptionId,
            DrugName = drugName,
            Status = state.Status,
            BlockingCount = state.BlockingCount,
            TotalInteractionCount = state.TotalInteractionCount,
            ScreenedDate = state.ScreenedDate,
        });

        return screeningId;
    }

    public async Task<InteractionScreeningState> GetInteractionScreeningAsync(string screeningId)
        => await InteractionScreening(screeningId).GetAsync();

    public async Task<InteractionScreeningState> GetInteractionScreeningForPrescriptionAsync(string prescriptionId)
        => await InteractionScreening($"IXSCREEN:{prescriptionId}").GetAsync();

    public async Task<List<InteractionScreeningIndexEntry>> GetInteractionScreeningsAsync()
        => await InteractionScreeningIndex().GetAllAsync();

    public async Task<List<InteractionScreeningIndexEntry>> GetBlockedPrescriptionsAsync()
        => await InteractionScreeningIndex().GetBlockedAsync();

    public async Task OverrideInteractionBlockAsync(
        string screeningId,
        int findingIndex,
        string pharmacistId,
        string reason)
    {
        await InteractionScreening(screeningId).OverrideInteractionAsync(findingIndex, pharmacistId, reason);
        InteractionScreeningState state = await InteractionScreening(screeningId).GetAsync();

        int remainingBlocking = state.Findings.Count(f => f.IsBlocking && !f.IsOverridden);
        await InteractionScreeningIndex().UpdateEntryAsync(
            screeningId, state.Status, remainingBlocking, state.TotalInteractionCount);
    }

    public async Task<bool> IsPrescriptionClearedForFillAsync(string prescriptionId)
    {
        InteractionScreeningState state = await InteractionScreening($"IXSCREEN:{prescriptionId}").GetAsync();

        // Not yet screened — allow fill (screening is optional but recommended)
        if (state.Status == InteractionScreeningStatus.NotScreened
            && string.IsNullOrEmpty(state.PrescriptionId))
            return true;

        // Cleared or all blocking overridden — allow fill
        return state.Status == InteractionScreeningStatus.Cleared
            || state.Status == InteractionScreeningStatus.OverriddenByPharmacist;
    }
}
