// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.Clinical;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Medical/procedure prior authorization (parallel to the drug PA) + the payer×procedure requirements
/// intelligence. Writes are gated by the IBCNR PRECERT key and audited; the requirements checklist read is
/// open (advisory decision support). Denial/approval outcomes feed the learned KB shard.
/// </summary>
public partial class PatientWorkflowGrain
{
    // ── Grain accessors ────────────────────────────────────────────────
    private IProcedureAuthorizationGrain ProcAuth(string procAuthId) =>
        GrainFactory.GetGrain<IProcedureAuthorizationGrain>(procAuthId);
    private IProcedureAuthorizationIndexGrain ProcAuthIndex() =>
        GrainFactory.GetGrain<IProcedureAuthorizationIndexGrain>($"PROC-AUTH-IDX:{PatientId}");
    private IPayerProcedureRequirementIndexGrain PayerProcReq(string payerId, string cptCode) =>
        GrainFactory.GetGrain<IPayerProcedureRequirementIndexGrain>(
            $"PAYER-PROC:{payerId.Trim().ToUpperInvariant()}:{cptCode.Trim().ToUpperInvariant()}");

    // ── Writes (gated IBCNR PRECERT, audited) ──────────────────────────

    public async Task<string> SubmitProcedureAuthAsync(
        string cptCode, string cptDescription, string payerId, string payerName,
        string orderingProviderId, string orderingProviderName, List<string> diagnosisCodes,
        string clinicalJustification, DateTime? serviceStartDate, DateTime? serviceEndDate,
        ProcedureAuthSubmissionChannel channel, string? orderId, string? consultId, string? externalReferralId)
    {
        string procAuthId = $"PROC-AUTH:{Guid.NewGuid()}";
        await ProcAuth(procAuthId).SubmitAsync(
            PatientId, cptCode, cptDescription, payerId, payerName,
            orderingProviderId, orderingProviderName, diagnosisCodes ?? new(),
            clinicalJustification, serviceStartDate, serviceEndDate,
            channel, orderId, consultId, externalReferralId);
        await RefreshProcAuthIndexAsync(procAuthId);
        return procAuthId;
    }

    public async Task PendProcedureAuthAsync(string procAuthId, string infoRequested)
    {
        await ProcAuth(procAuthId).PendAsync(infoRequested);
        await RefreshProcAuthIndexAsync(procAuthId);
    }

    public async Task ApproveProcedureAuthAsync(
        string procAuthId, string reviewerId, string reviewerName, string authorizationNumber,
        DateTime? expirationDate, List<PriorAuthRequirementCategory> categoriesSatisfied)
    {
        await ProcAuth(procAuthId).ApproveAsync(reviewerId, reviewerName, authorizationNumber, expirationDate);
        ProcedureAuthorizationState s = await ProcAuth(procAuthId).GetAsync();
        await RefreshProcAuthIndexAsync(procAuthId, s);
        // Feed the learned KB: which requirement categories this payer accepted for this procedure.
        await PayerProcReq(s.PayerId, s.CptCode).RecordApprovalAsync(categoriesSatisfied ?? new(), procAuthId);
    }

    public async Task DenyProcedureAuthAsync(
        string procAuthId, string reviewerId, string reviewerName, List<ProcedureDenialReason> denialReasons)
    {
        await ProcAuth(procAuthId).DenyAsync(reviewerId, reviewerName, denialReasons ?? new());
        ProcedureAuthorizationState s = await ProcAuth(procAuthId).GetAsync();
        await RefreshProcAuthIndexAsync(procAuthId, s);
        // Feed the learned KB: the categories (and any unmapped text) this payer denied for.
        List<PriorAuthRequirementCategory> categories = (denialReasons ?? new())
            .Select(d => d.Category).Distinct().ToList();
        string reasonText = string.Join("; ", (denialReasons ?? new())
            .Select(d => d.ReasonText).Where(t => !string.IsNullOrWhiteSpace(t)));
        await PayerProcReq(s.PayerId, s.CptCode).RecordDenialAsync(categories, reasonText, procAuthId);
    }

    public async Task ExpireProcedureAuthAsync(string procAuthId)
    {
        await ProcAuth(procAuthId).ExpireAsync();
        await RefreshProcAuthIndexAsync(procAuthId);
    }

    public async Task CancelProcedureAuthAsync(string procAuthId)
    {
        await ProcAuth(procAuthId).CancelAsync();
        await RefreshProcAuthIndexAsync(procAuthId);
    }

    // ── Reads (open) ───────────────────────────────────────────────────

    public Task<List<ProcedureAuthIndexEntry>> GetProcedureAuthsAsync() => ProcAuthIndex().GetAllAsync();

    public Task<ProcedureAuthorizationState> GetProcedureAuthAsync(string procAuthId) => ProcAuth(procAuthId).GetAsync();

    /// <summary>
    /// The forward-looking "fill these boxes" checklist for a (procedure, payer): the curated baseline
    /// unioned with the learned denial history, ranked by how often this payer has denied for each item.
    /// </summary>
    public async Task<PriorAuthRequirementChecklist> GetPriorAuthRequirementsAsync(string cptCode, string payerId)
    {
        string cpt = (cptCode ?? string.Empty).Trim().ToUpperInvariant();
        string payer = (payerId ?? string.Empty).Trim().ToUpperInvariant();
        string payerType = PriorAuthRequirementCatalog.ClassifyPayerType(payer);

        string payerName = string.Empty;
        try { payerName = (await GrainFactory.GetGrain<IPayerConfigGrain>($"PAYER-CFG:{payer}").GetAsync()).PayerName; }
        catch { /* best-effort name resolution */ }

        string cptDescription = string.Empty;
        try
        {
            CptCodeIndexEntry? entry = await GrainFactory.GetGrain<ICptCodeIndexGrain>("CPT-INDEX").GetCodeAsync(cpt);
            cptDescription = entry?.ShortName ?? entry?.LongDescription ?? string.Empty;
        }
        catch { /* best-effort description resolution */ }

        IReadOnlyList<RequirementItem> baseline = PriorAuthRequirementCatalog.GetBaseline(cpt, payer, payerType);
        PayerProcedureRequirementProfile profile = await PayerProcReq(payer, cpt).GetProfileAsync();

        return PriorAuthRequirementCatalog.Merge(
            baseline, profile, cpt, cptDescription, payer, payerName, payerType, DateTime.UtcNow);
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private async Task RefreshProcAuthIndexAsync(string procAuthId, ProcedureAuthorizationState? known = null)
    {
        ProcedureAuthorizationState s = known ?? await ProcAuth(procAuthId).GetAsync();
        await ProcAuthIndex().AddOrUpdateAsync(new ProcedureAuthIndexEntry
        {
            ProcAuthId = s.ProcAuthId,
            CptCode = s.CptCode,
            CptDescription = s.CptDescription,
            PayerName = s.PayerName,
            Status = s.Status,
            SubmittedDate = s.SubmittedDate,
            ExpirationDate = s.ExpirationDate,
            OrderingProviderName = s.OrderingProviderName
        });
    }
}
