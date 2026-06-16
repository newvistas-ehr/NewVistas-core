// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.Federation;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Optional feature grain for patient record merging.
/// Enabled per site via ISiteParametersGrain.Features containing "PATIENT_MERGE".
/// Maps to VistA DG MERGE utility (File #15.1).
///
/// Merge strategy:
/// 1. Validate both patients exist and source is not already merged
/// 2. Copy all embedded collections from source → target (deduplicate by ID)
/// 3. Move all ID lists from source → target
/// 4. Update the patient index (remove source, refresh target)
/// 5. Mark source patient as merged into target
/// 6. Record audit trail
/// </summary>
public class PatientMergeGrain : Grain, IPatientMergeGrain
{
    private readonly IPersistentState<PatientMergeState> _state;
    private readonly IClusterIdentity _clusterIdentity;
    private readonly IMpiFederationAnnouncer _mpiAnnouncer;

    public PatientMergeGrain(
        [PersistentState("patientMergeState", "patientMergeStore")]
        IPersistentState<PatientMergeState> state,
        IClusterIdentity clusterIdentity,
        IMpiFederationAnnouncer mpiAnnouncer)
    {
        _state = state;
        _clusterIdentity = clusterIdentity;
        _mpiAnnouncer = mpiAnnouncer;
    }

    public Task<PatientMergeState> GetMergeStateAsync() => Task.FromResult(_state.State);

    public async Task<PatientMergeResult> ExecuteMergeAsync(
        string targetPatientId,
        string sourcePatientId,
        string reason,
        string mergedByUserId,
        string mergedByUserName)
    {
        // Initialize merge job state
        _state.State.MergeId = this.GetPrimaryKeyString();
        _state.State.TargetPatientId = targetPatientId;
        _state.State.SourcePatientId = sourcePatientId;
        _state.State.Reason = reason;
        _state.State.MergedByUserId = mergedByUserId;
        _state.State.MergedByUserName = mergedByUserName;
        _state.State.MergeDate = DateTime.UtcNow;
        _state.State.Status = "IN_PROGRESS";
        await _state.WriteStateAsync();

        try
        {
            IPatientGrain sourceGrain = GrainFactory.GetGrain<IPatientGrain>(sourcePatientId);
            IPatientGrain targetGrain = GrainFactory.GetGrain<IPatientGrain>(targetPatientId);

            PatientState sourceState = await sourceGrain.GetPatientAsync();
            PatientState targetState = await targetGrain.GetPatientAsync();

            // ── Validation ──────────────────────────────────────────────
            if (sourcePatientId == targetPatientId)
                return await FailMerge("Cannot merge a patient into themselves.");
            if (sourceState.MergedIntoPatientId != null)
                return await FailMerge($"Source patient was already merged into {sourceState.MergedIntoPatientId}.");
            if (!sourceState.IsActive)
                return await FailMerge("Source patient is not active.");
            if (!targetState.IsActive)
                return await FailMerge("Target patient is not active.");

            var moved = new Dictionary<string, int>();

            // ── Phase 1: Embedded Collections ───────────────────────────
            moved["Allergies"] = await MergeEmbeddedAsync(
                sourceGrain.GetAllergiesAsync,
                targetGrain.GetAllergiesAsync,
                targetGrain.AddAllergyAsync,
                e => e.AllergyId);

            moved["Problems"] = await MergeEmbeddedAsync(
                sourceGrain.GetProblemsAsync,
                targetGrain.GetProblemsAsync,
                targetGrain.AddProblemAsync,
                e => e.ProblemId);

            moved["Immunizations"] = await MergeEmbeddedAsync(
                sourceGrain.GetImmunizationsAsync,
                targetGrain.GetImmunizationsAsync,
                targetGrain.AddImmunizationAsync,
                e => e.ImmunizationId);

            moved["ScConditions"] = await MergeEmbeddedAsync(
                sourceGrain.GetScConditionsAsync,
                targetGrain.GetScConditionsAsync,
                targetGrain.AddScConditionAsync,
                e => e.ConditionId);

            moved["DietOrders"] = await MergeEmbeddedAsync(
                sourceGrain.GetDietOrdersAsync,
                targetGrain.GetDietOrdersAsync,
                targetGrain.AddDietOrderAsync,
                e => e.DieteticsId);

            moved["ProstheticsItems"] = await MergeEmbeddedAsync(
                sourceGrain.GetProstheticsItemsAsync,
                targetGrain.GetProstheticsItemsAsync,
                targetGrain.AddProstheticsItemAsync,
                e => e.ProstheticsId);

            moved["MeansTests"] = await MergeEmbeddedAsync(
                sourceGrain.GetMeansTestsAsync,
                targetGrain.GetMeansTestsAsync,
                targetGrain.AddMeansTestAsync,
                e => e.MeansTestId);

            // ── Phase 2: ID Lists ───────────────────────────────────────
            moved["LabTests"] = await MergeIdListAsync(
                sourceGrain.GetLabTestIdsAsync, targetGrain.GetLabTestIdsAsync, targetGrain.AddLabTestIdAsync);

            moved["Orders"] = await MergeIdListAsync(
                sourceGrain.GetOrderIdsAsync, targetGrain.GetOrderIdsAsync, targetGrain.AddOrderIdAsync);

            moved["Pharmacy"] = await MergeIdListAsync(
                sourceGrain.GetPharmacyIdsAsync, targetGrain.GetPharmacyIdsAsync, targetGrain.AddPharmacyIdAsync);

            moved["Bcma"] = await MergeIdListAsync(
                sourceGrain.GetBcmaIdsAsync, targetGrain.GetBcmaIdsAsync, targetGrain.AddBcmaIdAsync);

            moved["Radiology"] = await MergeIdListAsync(
                sourceGrain.GetRadiologyIdsAsync, targetGrain.GetRadiologyIdsAsync, targetGrain.AddRadiologyIdAsync);

            moved["TiuDocuments"] = await MergeIdListAsync(
                sourceGrain.GetTiuDocumentIdsAsync, targetGrain.GetTiuDocumentIdsAsync, targetGrain.AddTiuDocumentIdAsync);

            moved["Consults"] = await MergeIdListAsync(
                sourceGrain.GetConsultIdsAsync, targetGrain.GetConsultIdsAsync, targetGrain.AddConsultIdAsync);

            moved["Surgeries"] = await MergeIdListAsync(
                sourceGrain.GetSurgeryIdsAsync, targetGrain.GetSurgeryIdsAsync, targetGrain.AddSurgeryIdAsync);

            moved["ClinicalReminders"] = await MergeIdListAsync(
                sourceGrain.GetClinicalReminderIdsAsync, targetGrain.GetClinicalReminderIdsAsync, targetGrain.AddClinicalReminderIdAsync);

            moved["HealthFactors"] = await MergeIdListAsync(
                sourceGrain.GetHealthFactorIdsAsync, targetGrain.GetHealthFactorIdsAsync, targetGrain.AddHealthFactorIdAsync);

            moved["MentalHealth"] = await MergeIdListAsync(
                sourceGrain.GetMentalHealthIdsAsync, targetGrain.GetMentalHealthIdsAsync, targetGrain.AddMentalHealthIdAsync);

            moved["Imaging"] = await MergeIdListAsync(
                sourceGrain.GetImagingIdsAsync, targetGrain.GetImagingIdsAsync, targetGrain.AddImagingIdAsync);

            moved["Adt"] = await MergeIdListAsync(
                sourceGrain.GetAdtIdsAsync, targetGrain.GetAdtIdsAsync, targetGrain.AddAdtIdAsync);

            moved["Appointments"] = await MergeIdListAsync(
                sourceGrain.GetAppointmentIdsAsync, targetGrain.GetAppointmentIdsAsync, targetGrain.AddAppointmentIdAsync);

            // ── Phase 2b: Full-History Indexes ──────────────────────────
            // The Phase 2 ID-list copies read PatientState lists, which are a
            // capped recent window once a domain is migrated. Merge the
            // source's COMPLETE per-domain ID history into the target's
            // IPatientHistoryIndexGrain so older item references survive the
            // merge. Only domains in PatientHistoryDomains are history-merged.
            await MergeHistoryIndexAsync(sourcePatientId, targetPatientId, sourceGrain,
                PatientHistoryDomains.Lab, sourceGrain.GetLabTestIdsAsync);
            await MergeHistoryIndexAsync(sourcePatientId, targetPatientId, sourceGrain,
                PatientHistoryDomains.Order, sourceGrain.GetOrderIdsAsync);
            await MergeHistoryIndexAsync(sourcePatientId, targetPatientId, sourceGrain,
                PatientHistoryDomains.Pharmacy, sourceGrain.GetPharmacyIdsAsync);
            await MergeHistoryIndexAsync(sourcePatientId, targetPatientId, sourceGrain,
                PatientHistoryDomains.Bcma, sourceGrain.GetBcmaIdsAsync);
            await MergeHistoryIndexAsync(sourcePatientId, targetPatientId, sourceGrain,
                PatientHistoryDomains.Radiology, sourceGrain.GetRadiologyIdsAsync);
            await MergeHistoryIndexAsync(sourcePatientId, targetPatientId, sourceGrain,
                PatientHistoryDomains.Tiu, sourceGrain.GetTiuDocumentIdsAsync);
            await MergeHistoryIndexAsync(sourcePatientId, targetPatientId, sourceGrain,
                PatientHistoryDomains.Consult, sourceGrain.GetConsultIdsAsync);
            await MergeHistoryIndexAsync(sourcePatientId, targetPatientId, sourceGrain,
                PatientHistoryDomains.Surgery, sourceGrain.GetSurgeryIdsAsync);
            await MergeHistoryIndexAsync(sourcePatientId, targetPatientId, sourceGrain,
                PatientHistoryDomains.Reminder, sourceGrain.GetClinicalReminderIdsAsync);
            await MergeHistoryIndexAsync(sourcePatientId, targetPatientId, sourceGrain,
                PatientHistoryDomains.HealthFactor, sourceGrain.GetHealthFactorIdsAsync);
            await MergeHistoryIndexAsync(sourcePatientId, targetPatientId, sourceGrain,
                PatientHistoryDomains.MentalHealth, sourceGrain.GetMentalHealthIdsAsync);
            await MergeHistoryIndexAsync(sourcePatientId, targetPatientId, sourceGrain,
                PatientHistoryDomains.Imaging, sourceGrain.GetImagingIdsAsync);
            await MergeHistoryIndexAsync(sourcePatientId, targetPatientId, sourceGrain,
                PatientHistoryDomains.Adt, sourceGrain.GetAdtIdsAsync);
            await MergeHistoryIndexAsync(sourcePatientId, targetPatientId, sourceGrain,
                PatientHistoryDomains.Appointment, sourceGrain.GetAppointmentIdsAsync);

            // PHARMACY additionally has a per-patient PSO index (the
            // authoritative complete prescription set, feeding drug-interaction
            // screening) — copy the source's entries into the target's index.
            IPatientPrescriptionIndexGrain sourcePso =
                GrainFactory.GetGrain<IPatientPrescriptionIndexGrain>($"PSO-INDEX:{sourcePatientId}");
            IPatientPrescriptionIndexGrain targetPso =
                GrainFactory.GetGrain<IPatientPrescriptionIndexGrain>($"PSO-INDEX:{targetPatientId}");
            foreach (PrescriptionIndexEntry psoEntry in await sourcePso.GetAllAsync())
                await targetPso.AddOrUpdateEntryAsync(psoEntry);

            // ── Phase 3: Update Patient Index ───────────────────────────
            IPatientIndexGrain indexGrain = GrainFactory.GetGrain<IPatientIndexGrain>("PATIENT-INDEX");

            // Mark source as inactive in the index
            await indexGrain.AddOrUpdateAsync(new PatientIndexEntry
            {
                PatientId = sourcePatientId,
                Name = sourceState.Name,
                DateOfBirth = sourceState.DateOfBirth,
                Sex = sourceState.Sex,
                SsnLast4 = sourceState.SocialSecurityNumber?.Length >= 4
                    ? sourceState.SocialSecurityNumber[^4..] : string.Empty,
                Dfn = sourceState.Dfn,
                Icn = sourceState.Icn,
                IsActive = false
            });

            // Refresh target in index
            PatientState updatedTarget = await targetGrain.GetPatientAsync();
            await indexGrain.AddOrUpdateAsync(new PatientIndexEntry
            {
                PatientId = targetPatientId,
                Name = updatedTarget.Name,
                DateOfBirth = updatedTarget.DateOfBirth,
                Sex = updatedTarget.Sex,
                SsnLast4 = updatedTarget.SocialSecurityNumber?.Length >= 4
                    ? updatedTarget.SocialSecurityNumber[^4..] : string.Empty,
                Dfn = updatedTarget.Dfn,
                Icn = updatedTarget.Icn,
                IsActive = true
            });

            // ── Phase 4: Mark Source as Merged ──────────────────────────
            await sourceGrain.MarkAsMergedAsync(targetPatientId, mergedByUserId);

            // ── Phase 4b: Propagate the merge to the MPI ────────────────
            // Without this, MPI search would still return the source as a
            // separate patient and cross-cluster lookups by the source ICN
            // would not redirect to the survivor.
            //
            // - Source MPI correlation grain: stamp MergedIntoIcn so federation
            //   inbound and any future search-by-ICN can follow the alias.
            // - Source MPI search entry: stamp MergedIntoIcn so the clinician UI
            //   can flag the result as merged. Source's facility correlations stay
            //   on the source grain (audit trail of where the duplicate was seen);
            //   any clinician following the alias will read the target's facilities.
            // - Target MPI search entry: refresh facility count after the move.
            //
            // Both updates are skipped silently if the source patient never had
            // an ICN (legacy data path; pre-ICN-issuance patients).
            if (!string.IsNullOrEmpty(sourceState.Icn) && !string.IsNullOrEmpty(targetState.Icn))
            {
                IMpiCorrelationGrain sourceMpi = GrainFactory.GetGrain<IMpiCorrelationGrain>($"MPI:{sourceState.Icn}");
                await sourceMpi.MarkAsMergedAsync(targetState.Icn);

                IMpiSearchGrain mpiSearch = GrainFactory.GetGrain<IMpiSearchGrain>("MPI-INDEX");
                MpiCorrelationState sourceCorr = await sourceMpi.GetCorrelationAsync();
                await mpiSearch.AddOrUpdatePatientAsync(new MpiSearchEntry
                {
                    Icn = sourceState.Icn,
                    PatientName = sourceCorr.PatientName,
                    Ssn = sourceCorr.Ssn,
                    DateOfBirth = sourceCorr.DateOfBirth,
                    Sex = sourceCorr.Sex,
                    FacilityCount = sourceCorr.LocalCorrelations.Count,
                    IsDeceased = sourceCorr.IsDeceased,
                    MergedIntoIcn = targetState.Icn
                });

                IMpiCorrelationGrain targetMpi = GrainFactory.GetGrain<IMpiCorrelationGrain>($"MPI:{targetState.Icn}");
                MpiCorrelationState targetCorr = await targetMpi.GetCorrelationAsync();
                await mpiSearch.AddOrUpdatePatientAsync(new MpiSearchEntry
                {
                    Icn = targetState.Icn,
                    PatientName = targetCorr.PatientName,
                    Ssn = targetCorr.Ssn,
                    DateOfBirth = targetCorr.DateOfBirth,
                    Sex = targetCorr.Sex,
                    FacilityCount = targetCorr.LocalCorrelations.Count,
                    IsDeceased = targetCorr.IsDeceased,
                    MergedIntoIcn = null
                });
            }

            // ── Phase 4c: Federation Announce ───────────────────────────
            // In a federated multi-facility deployment, peer clusters need
            // to know that the source ICN is now an alias for the target
            // so their MPI search/correlation grains can update. The
            // announcer is a per-deployment policy (no-op by default for
            // single-cluster sites). Errors here are intentionally swallowed
            // — local merge has already committed and is consistent.
            if (!string.IsNullOrEmpty(sourceState.Icn) && !string.IsNullOrEmpty(targetState.Icn))
            {
                try
                {
                    await _mpiAnnouncer.AnnouncePatientMergedAsync(
                        sourceIcn: sourceState.Icn,
                        targetIcn: targetState.Icn,
                        originatingFacilityId: _clusterIdentity.LocalClusterId);
                }
                catch
                {
                    // Announce failure is operational, not clinical.
                }
            }

            // ── Phase 5: Persist Audit ──────────────────────────────────
            _state.State.Status = "COMPLETED";
            _state.State.ItemsMoved = moved;
            await _state.WriteStateAsync();

            return new PatientMergeResult
            {
                Success = true,
                MergeId = _state.State.MergeId,
                ItemsMoved = moved
            };
        }
        catch (Exception ex)
        {
            return await FailMerge(ex.Message);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private async Task<PatientMergeResult> FailMerge(string error)
    {
        _state.State.Status = "FAILED";
        _state.State.ErrorMessage = error;
        await _state.WriteStateAsync();
        return new PatientMergeResult
        {
            Success = false,
            MergeId = _state.State.MergeId,
            ErrorMessage = error
        };
    }

    /// <summary>
    /// Merge an embedded collection from source into target, skipping duplicates by ID.
    /// </summary>
    private static async Task<int> MergeEmbeddedAsync<T>(
        Func<Task<List<T>>> getSource,
        Func<Task<List<T>>> getTarget,
        Func<T, Task> addToTarget,
        Func<T, string> getId)
    {
        List<T> sourceItems = await getSource();
        if (sourceItems.Count == 0) return 0;

        List<T> targetItems = await getTarget();
        HashSet<string> existingIds = new(targetItems.Select(getId));

        int count = 0;
        foreach (T item in sourceItems)
        {
            if (existingIds.Add(getId(item)))
            {
                await addToTarget(item);
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// Merge a domain's FULL ID history from source into target's history
    /// index. Reads the source history index once the domain is migrated
    /// (the legacy PatientState list is then only a capped recent window),
    /// otherwise the legacy list. AddRangeAsync deduplicates by ItemId.
    /// </summary>
    private async Task MergeHistoryIndexAsync(
        string sourcePatientId,
        string targetPatientId,
        IPatientGrain sourceGrain,
        string domain,
        Func<Task<List<string>>> getLegacySourceIds)
    {
        IPatientHistoryIndexGrain sourceHistory =
            GrainFactory.GetGrain<IPatientHistoryIndexGrain>($"{sourcePatientId}:{domain}");

        List<string> sourceIds = await sourceGrain.IsDomainMigratedAsync(domain)
            ? await sourceHistory.GetAllIdsAsync()
            : await getLegacySourceIds();

        if (sourceIds.Count == 0) return;

        IPatientHistoryIndexGrain targetHistory =
            GrainFactory.GetGrain<IPatientHistoryIndexGrain>($"{targetPatientId}:{domain}");
        await targetHistory.AddRangeAsync(
            sourceIds.Select(id => new HistoryRef { ItemId = id, Date = null }).ToList());
    }

    /// <summary>
    /// Merge an ID list from source into target, skipping duplicates.
    /// </summary>
    private static async Task<int> MergeIdListAsync(
        Func<Task<List<string>>> getSource,
        Func<Task<List<string>>> getTarget,
        Func<string, Task> addToTarget)
    {
        List<string> sourceIds = await getSource();
        if (sourceIds.Count == 0) return 0;

        List<string> targetIds = await getTarget();
        HashSet<string> existingIds = new(targetIds);

        int count = 0;
        foreach (string id in sourceIds)
        {
            if (existingIds.Add(id))
            {
                await addToTarget(id);
                count++;
            }
        }
        return count;
    }
}
