// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Pharmacy Workflow State Machine — enforces VistA PSOORED.m sequencing.
///
/// Coordinates cross-grain safety checks (DUR, interaction screening) before
/// delegating to PharmacyGrain for status mutations. PharmacyGrain itself
/// enforces status/state guards (e.g., must be ACTIVE, must be verified).
///
/// Sequence: Create → DUR → Interaction Screen → Verify → Label → Fill → Refill
///
/// Both Blazor (OrleansGrainService) and WPF (OrleansGrainService) call these
/// methods directly as Orleans clients — no controller intermediary needed.
/// Controllers mirror the same calls for external API access.
/// </summary>
public partial class PatientWorkflowGrain
{
    // ─── Pharmacy grain helper ───────────────────────────────────────────────

    private IPharmacyGrain PharmacyRx(string prescriptionId)
        => GrainFactory.GetGrain<IPharmacyGrain>(prescriptionId);

    // ─── DUR Clearance Check ─────────────────────────────────────────────────

    public async Task<bool> IsDurClearedForPrescriptionAsync(string prescriptionId)
    {
        DurAssessmentIndexEntry? durEntry = await DurIndex().GetByPrescriptionAsync(prescriptionId);

        // No DUR performed — not cleared (DUR is required before fill/verify)
        if (durEntry is null)
            return false;

        return durEntry.Status is DurAssessmentStatus.Passed
            or DurAssessmentStatus.OverriddenByPharmacist
            or DurAssessmentStatus.Acknowledged;
    }

    // ─── Prior Auth / Coverage helpers ─────────────────────────────────────

    private IPatientBenefitPlanGrain PatientBenefit()
        => GrainFactory.GetGrain<IPatientBenefitPlanGrain>($"PBM-PATIENT:{PatientId}");

    private IFormularyIndexGrain Formulary(string planId)
        => GrainFactory.GetGrain<IFormularyIndexGrain>($"PBM-FORMULARY:{planId}");

    private IPriorAuthIndexGrain PriorAuthIndex()
        => GrainFactory.GetGrain<IPriorAuthIndexGrain>($"PA-INDEX:{PatientId}");

    public async Task<PriorAuthCoverageResult> CheckPriorAuthStatusAsync(string prescriptionId)
    {
        PriorAuthCoverageResult result = new() { PrescriptionId = prescriptionId, IsCleared = true };

        PharmacyState rxState = await PharmacyRx(prescriptionId).GetPrescriptionAsync();
        string? drugId = rxState.DrugId;

        // If no drug ID, can't check formulary — allow (no coverage data)
        if (string.IsNullOrEmpty(drugId))
            return result;

        // Get patient's benefit plan
        PatientBenefitPlanState plan = await PatientBenefit().GetPlanAsync();
        if (string.IsNullOrEmpty(plan.PlanId) || !plan.IsActive)
        {
            result.HasActivePlan = false;
            return result; // No active plan — allow (uninsured / VA direct)
        }

        result.HasActivePlan = true;
        result.PlanName = plan.PlanName;

        // Check formulary coverage
        FormularyEntry? entry = await Formulary(plan.PlanId).GetDrugAsync(drugId);
        if (entry is null)
            return result; // Drug not in formulary — allow

        result.CoverageStatus = entry.CoverageStatus;
        result.Tier = entry.Tier;

        if (entry.CoverageStatus == "NOT_COVERED")
        {
            result.IsCleared = false;
            result.Reasons.Add($"Drug is NOT COVERED under plan {plan.PlanName}.");
        }

        if (entry.RequiresPriorAuth)
        {
            result.RequiresPriorAuth = true;
            // Check if an approved PA exists
            List<PriorAuthIndexEntry> approvedPAs = await PriorAuthIndex().GetApprovedAsync();
            PriorAuthIndexEntry? matchingPa = approvedPAs.FirstOrDefault(pa =>
                pa.DrugId == drugId || string.Equals(pa.DrugName, rxState.DrugName, StringComparison.OrdinalIgnoreCase));

            if (matchingPa is not null)
            {
                result.HasApprovedPa = true;
                result.PaId = matchingPa.PaId;
            }
            else
            {
                result.HasApprovedPa = false;
                result.IsCleared = false;
                result.Reasons.Add("Drug requires Prior Authorization. No approved PA found.");
            }
        }

        return result;
    }

    // ─── Pharmacy Workflow Methods ───────────────────────────────────────────

    public async Task FillPrescriptionWorkflowAsync(string prescriptionId, DateTime fillDate)
    {
        // Cross-grain gate 1: DUR must be cleared
        if (!await IsDurClearedForPrescriptionAsync(prescriptionId))
            throw new InvalidOperationException(
                "Cannot fill prescription: DUR assessment is required and must be Passed, Overridden, or Acknowledged.");

        // Cross-grain gate 2: Interaction screening must be cleared
        if (!await IsPrescriptionClearedForFillAsync(prescriptionId))
            throw new InvalidOperationException(
                "Cannot fill prescription: drug interaction screening has unresolved blocking interactions.");

        // Cross-grain gate 3: Prior auth / coverage must be cleared
        PriorAuthCoverageResult paResult = await CheckPriorAuthStatusAsync(prescriptionId);
        if (!paResult.IsCleared)
            throw new InvalidOperationException(
                $"Cannot fill prescription: {string.Join(" ", paResult.Reasons)}");

        // Delegate to PharmacyGrain (enforces ACTIVE, verified, DEA, constraints, not already filled)
        await PharmacyRx(prescriptionId).FillPrescriptionAsync(fillDate);
    }

    public async Task RefillPrescriptionWorkflowAsync(string prescriptionId, DateTime fillDate)
    {
        // Cross-grain gate 1: DUR
        if (!await IsDurClearedForPrescriptionAsync(prescriptionId))
            throw new InvalidOperationException(
                "Cannot refill prescription: DUR assessment is required and must be Passed, Overridden, or Acknowledged.");

        // Cross-grain gate 2: Interaction screening
        if (!await IsPrescriptionClearedForFillAsync(prescriptionId))
            throw new InvalidOperationException(
                "Cannot refill prescription: drug interaction screening has unresolved blocking interactions.");

        // Cross-grain gate 3: Prior auth / coverage
        PriorAuthCoverageResult paResult = await CheckPriorAuthStatusAsync(prescriptionId);
        if (!paResult.IsCleared)
            throw new InvalidOperationException(
                $"Cannot refill prescription: {string.Join(" ", paResult.Reasons)}");

        // Delegate to PharmacyGrain (enforces ACTIVE, prior fill, DEA, constraints, refills > 0, not expired)
        await PharmacyRx(prescriptionId).RefillAsync(fillDate);
    }

    public async Task VerifyPrescriptionWorkflowAsync(string prescriptionId, string pharmacistId)
    {
        // Cross-grain gate: DUR must be cleared before verification
        if (!await IsDurClearedForPrescriptionAsync(prescriptionId))
            throw new InvalidOperationException(
                "Cannot verify prescription: DUR assessment is required and must be Passed, Overridden, or Acknowledged.");

        // Delegate to PharmacyGrain (enforces ACTIVE, not already verified)
        await PharmacyRx(prescriptionId).VerifyAsync(pharmacistId);
    }

    public async Task PrintLabelWorkflowAsync(string prescriptionId, string? rxNumber)
    {
        // PharmacyGrain enforces IsVerified — no additional cross-grain checks needed
        await PharmacyRx(prescriptionId).PrintLabelAsync(rxNumber);
    }

    public async Task HoldPrescriptionWorkflowAsync(string prescriptionId, string reason)
    {
        await PharmacyRx(prescriptionId).PlaceOnHoldAsync(reason);
    }

    public async Task ResumePrescriptionWorkflowAsync(string prescriptionId)
    {
        await PharmacyRx(prescriptionId).ResumeAsync();
    }

    public async Task DiscontinuePrescriptionWorkflowAsync(string prescriptionId, string reason)
    {
        await PharmacyRx(prescriptionId).DiscontinueAsync(reason);
    }

    public async Task ExpirePrescriptionWorkflowAsync(string prescriptionId)
    {
        await PharmacyRx(prescriptionId).ExpireAsync();
    }

    // ─── Refill Eligibility (PSO refill date calculation) ────────────────────

    public async Task<RefillEligibilityResult> GetRefillEligibilityAsync(
        string prescriptionId, DateTime proposedFillDate)
    {
        // Get grain-level eligibility (status, refills remaining, timing, DEA)
        RefillEligibilityResult result = await PharmacyRx(prescriptionId)
            .CheckRefillEligibilityAsync(proposedFillDate);

        // Add cross-grain DUR/interaction gates
        result.DurCleared = await IsDurClearedForPrescriptionAsync(prescriptionId);
        result.InteractionCleared = await IsPrescriptionClearedForFillAsync(prescriptionId);

        if (!result.DurCleared)
        {
            result.IsEligible = false;
            result.Reasons.Add("DUR assessment is required and must be Passed, Overridden, or Acknowledged.");
        }

        if (!result.InteractionCleared)
        {
            result.IsEligible = false;
            result.Reasons.Add("Drug interaction screening has unresolved blocking interactions.");
        }

        // Add cross-grain PA/coverage check
        PriorAuthCoverageResult paResult = await CheckPriorAuthStatusAsync(prescriptionId);
        result.PaCoverageCleared = paResult.IsCleared;
        if (!paResult.IsCleared)
        {
            result.IsEligible = false;
            result.PaCoverageReasons = paResult.Reasons;
            result.Reasons.AddRange(paResult.Reasons);
        }

        return result;
    }

    // ─── NDC/Lot Tracking (PSO dispense recording) ──────────────────────────

    public async Task RecordDispenseWorkflowAsync(
        string prescriptionId, string? ndcDispensed, string? lotNumber, string? pharmacistId)
    {
        await PharmacyRx(prescriptionId).RecordDispenseAsync(ndcDispensed, lotNumber, pharmacistId);
    }

    // ─── Patient Counseling (PSOCP.m) ───────────────────────────────────────

    public async Task RecordCounselingWorkflowAsync(
        string prescriptionId, string pharmacistId, string? notes)
    {
        await PharmacyRx(prescriptionId).RecordCounselingAsync(pharmacistId, notes);
    }

    // ─── Label Generation (PSJLBL.m) ────────────────────────────────────────

    public async Task<PrescriptionLabelContent> GenerateLabelContentWorkflowAsync(string prescriptionId)
    {
        PrescriptionLabelContent label = await PharmacyRx(prescriptionId).GenerateLabelContentAsync();

        // Enrich with patient name from the patient grain
        PatientState patient = await GetPatientAsync();
        label.PatientName = patient.Name;

        return label;
    }

    // ─── Pending DUR Status (PSOORED.m incomplete/pending status) ───────────

    public async Task SetPendingDurReviewStatusAsync(string prescriptionId)
    {
        PharmacyState state = await PharmacyRx(prescriptionId).GetPrescriptionAsync();
        if (state.Status == "ACTIVE")
        {
            // We don't change the PharmacyGrain status — the fill gate (IsDurClearedForPrescriptionAsync)
            // already blocks fill/verify/refill. Instead we update MedStatusGroup to PENDING (1)
            // so the UI and index can display the pending DUR state.
            await PharmacyRx(prescriptionId).UpdateMedStatusGroupAsync();
        }
    }
}
