// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Emerging-condition surveillance workflow hooks (Phase E). Patient-scoped operations route
/// through the workflow so they are per-patient audited; the cover-sheet banner helper assembles the
/// non-suppressible precaution banners. Feature-gated by <c>EMERGING_CONDITIONS</c> (shared const
/// and guard defined in the Symptoms partial).
/// </summary>
public partial class PatientWorkflowGrain
{
    private IProtoConditionGrain Proto(string protoConditionId) =>
        GrainFactory.GetGrain<IProtoConditionGrain>($"PROTO:{protoConditionId}");

    // ─── Cover-sheet precaution banners ─────────────────────────────────

    /// <summary>
    /// Builds one banner per Active proto-condition this patient is a CONFIRMED member of. A
    /// Candidate is not a clinical assertion, so it never banners. Cheap: reads the index and the
    /// confirmed-cohort shards only (no full proto loads).
    /// </summary>
    private async Task<List<PrecautionBanner>> BuildEmergingConditionBannersAsync()
    {
        var banners = new List<PrecautionBanner>();
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(EmergingConditionsFeature);
        if (!enabled)
            return banners;

        List<ProtoConditionSummary> active = await GrainFactory
            .GetGrain<IProtoConditionIndexGrain>("PROTOCONDITION-INDEX").GetActiveAsync();

        foreach (ProtoConditionSummary s in active)
        {
            bool isMember = await GrainFactory
                .GetGrain<IProtoCohortIndexGrain>($"PROTO-COHORT:{s.ProtoConditionId}")
                .ContainsAsync(PatientId);
            if (!isMember)
                continue;

            string precaution = s.IsolationRecommendation is BedIsolationType b && b != BedIsolationType.None
                ? $"{b} isolation recommended"
                : "under surveillance";
            banners.Add(new PrecautionBanner
            {
                ProtoConditionId = s.ProtoConditionId,
                ConditionName = s.Name,
                Isolation = s.IsolationRecommendation,
                Message = $"Emerging condition — {s.Name}: {precaution} (confirmed cluster member)."
            });
        }
        return banners;
    }

    // ─── Membership (per-patient audited wrappers over the proto grain) ──

    public async Task SuggestForProtoConditionAsync(string protoConditionId, string byUser)
    {
        await RequireEmergingConditionsFeatureAsync();
        await Proto(protoConditionId).SuggestMemberAsync(PatientId, byUser);
    }

    public async Task ConfirmProtoMembershipAsync(string protoConditionId, string byUser)
    {
        await RequireEmergingConditionsFeatureAsync();
        await Proto(protoConditionId).ConfirmMemberAsync(PatientId, byUser);
    }

    public async Task ExcludeProtoMembershipAsync(string protoConditionId, string reason, string byUser)
    {
        await RequireEmergingConditionsFeatureAsync();
        await Proto(protoConditionId).ExcludeMemberAsync(PatientId, byUser, reason);
    }

    // ─── Post-promotion migration (problem-list recode) ─────────────────

    public async Task<string> MigratePromotedProtoProblemAsync(string protoConditionId, string byUser)
    {
        await RequireEmergingConditionsFeatureAsync();
        IProtoConditionGrain proto = Proto(protoConditionId);
        ProtoConditionState state = await proto.GetAsync();
        if (state.Status != ProtoConditionStatus.Promoted)
            throw new InvalidOperationException("The proto-condition has not been promoted; nothing to migrate.");

        string dxCode = state.PromotedIcd10Codes.FirstOrDefault() ?? state.PromotedSnomed ?? string.Empty;
        string dxName = string.IsNullOrWhiteSpace(state.PromotedName) ? state.Name : state.PromotedName!;

        // Idempotent: if the patient already carries the promoted code, record the migration and stop.
        List<ProblemSummary> existing = await GetActiveProblemsAsync();
        ProblemSummary? already = existing.FirstOrDefault(p =>
            !string.IsNullOrWhiteSpace(dxCode) && string.Equals(p.DiagnosisCode, dxCode, StringComparison.OrdinalIgnoreCase));
        if (already is not null)
        {
            await proto.RecordMigrationAsync(PatientId, ProtoMigrationStatus.Migrated, already.ProblemId, "already coded", byUser);
            return already.ProblemId;
        }

        string comment = $"Recoded from emerging cluster '{state.Name}' (proto {protoConditionId}) on promotion to {dxName}.";
        string problemId = await AddProblemAsync(
            diagnosis: dxName, diagnosisCode: dxCode, condition: null, priority: null,
            dateOfOnset: state.PromotedEffectiveFrom, providerId: null, providerName: null,
            clinicId: null, clinicName: null, isServiceConnected: false, comments: comment);

        await proto.RecordMigrationAsync(PatientId, ProtoMigrationStatus.Migrated, problemId, null, byUser);
        await NotifyPrimaryProviderOfMigrationAsync(dxName, dxCode);
        return problemId;
    }

    public async Task SkipMemberMigrationAsync(string protoConditionId, string reason, string byUser)
    {
        await RequireEmergingConditionsFeatureAsync();
        await Proto(protoConditionId).RecordMigrationAsync(PatientId, ProtoMigrationStatus.Skipped, null, reason, byUser);
    }

    private async Task NotifyPrimaryProviderOfMigrationAsync(string dxName, string dxCode)
    {
        try
        {
            List<CareTeamMember> team = await GetCareTeamAsync();
            CareTeamMember? provider = team.FirstOrDefault(m => m.Role.Contains("PRIMARY", StringComparison.OrdinalIgnoreCase))
                                       ?? team.FirstOrDefault();
            if (provider is null || string.IsNullOrWhiteSpace(provider.ProviderId))
                return;

            string alertId = $"ALERT-PROTO-MIGRATE-{PatientId}-{Guid.NewGuid():N}";
            await GrainFactory.GetGrain<INotificationGrain>(alertId).CreateNotificationAsync(
                patientId: PatientId,
                notificationType: NotificationType.EmergingConditionPromoted,
                notificationTypeText: "Emerging condition recoded",
                recipientId: provider.ProviderId,
                recipientName: provider.ProviderName,
                sendingPackage: "SURVEILLANCE",
                messageText: $"{dxName} ({dxCode}) added to the problem list from a promoted emerging cluster. Please review/amend as needed.",
                followUpAction: "/problems",
                isCritical: false,
                xqaData: dxCode);
        }
        catch
        {
            // Notification is best-effort — never fail the migration on a notify error.
        }
    }
}
