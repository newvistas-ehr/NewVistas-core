// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Patient Merge — Site Flavor Architecture (Option 4: Composition).
/// This workflow method checks the PATIENT_MERGE feature flag before delegating
/// to the optional IPatientMergeGrain. If the feature is not enabled, the
/// merge grain is never activated and consumes no resources.
/// </summary>
public partial class PatientWorkflowGrain
{
    private const string PatientMergeFeature = "PATIENT_MERGE";

    public async Task<PatientMergeResult> MergePatientAsync(
        string sourcePatientId,
        string reason,
        string mergedByUserId,
        string mergedByUserName)
    {
        // ── Feature gate (Site Flavor Architecture) ─────────────────
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(PatientMergeFeature);
        if (!enabled)
        {
            return new PatientMergeResult
            {
                Success = false,
                ErrorMessage = "Patient merge is not enabled for this site. Enable the PATIENT_MERGE feature in Site Parameters."
            };
        }

        // ── Delegate to the optional merge grain ────────────────────
        string mergeId = $"MERGE:{Guid.NewGuid()}";
        IPatientMergeGrain mergeGrain = GrainFactory.GetGrain<IPatientMergeGrain>(mergeId);

        // [AuditAction("PATIENT_MERGE", "MERGE", IsClinicalWrite = true)] on the
        // interface method causes AuditCallFilter to record this call automatically;
        // no manual LogAuditEventAsync needed.
        return await mergeGrain.ExecuteMergeAsync(
            PatientId,          // this grain's patient is the surviving (target) patient
            sourcePatientId,    // the duplicate being merged in
            reason,
            mergedByUserId,
            mergedByUserName);
    }
}
