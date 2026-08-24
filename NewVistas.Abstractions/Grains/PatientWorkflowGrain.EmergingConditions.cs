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

        List<ProtoConditionSummary> all = await GrainFactory
            .GetGrain<IProtoConditionIndexGrain>("PROTOCONDITION-INDEX").GetAllAsync();

        foreach (ProtoConditionSummary s in all)
        {
            if (s.Status is not (ProtoConditionStatus.Active or ProtoConditionStatus.Promoted))
                continue;

            bool isMember = await GrainFactory
                .GetGrain<IProtoCohortIndexGrain>($"PROTO-COHORT:{s.ProtoConditionId}")
                .ContainsAsync(PatientId);
            if (!isMember)
                continue;

            // A promoted cluster keeps its banner ONLY while this patient's recode is still
            // pending. Previously the index was filtered to Active, so at the instant of
            // promotion the precaution banner vanished for every member — including those with
            // no coded problem yet, leaving an infection-control patient with neither a banner
            // nor a diagnosis until somebody clicked Recode. Once the problem exists the coded
            // diagnosis carries the signal and the banner correctly stands down.
            bool awaitingRecode = false;
            if (s.Status == ProtoConditionStatus.Promoted)
            {
                ProtoConditionState p = await Proto(s.ProtoConditionId).GetAsync();
                awaitingRecode = p.MigrationLog.Any(m =>
                    m.PatientId == PatientId && m.Status == ProtoMigrationStatus.Pending);
                if (!awaitingRecode)
                    continue;
            }

            string precaution = s.IsolationRecommendation is BedIsolationType b && b != BedIsolationType.None
                ? $"{b} isolation recommended"
                : "under surveillance";
            string qualifier = awaitingRecode
                ? "promoted — problem-list recode pending"
                : "confirmed cluster member";
            banners.Add(new PrecautionBanner
            {
                ProtoConditionId = s.ProtoConditionId,
                ConditionName = s.Name,
                Isolation = s.IsolationRecommendation,
                Message = $"Emerging condition — {s.Name}: {precaution} ({qualifier})."
            });
        }
        return banners;
    }

    // ─── Proposing a new cluster from this patient ───────────────────────

    /// <summary>
    /// "This matches nothing I know." Assembles this patient's feature snapshot and drafts a
    /// cluster from it (ADR-004 ↔ ADR-006).
    ///
    /// The draft is created at <see cref="ProtoConditionStatus.Draft"/> and can be created at
    /// nothing else, so it is invisible to every chart, banner and sweep until an
    /// epidemiologist reviews and activates it. Detection and assembly are automated; publication
    /// is not — naming a cluster creates its population, so that step stays human.
    /// </summary>
    public async Task<string> ProposeProtoConditionFromPatientAsync(string workingName, string byUser)
    {
        await RequireEmergingConditionsFeatureAsync();

        PatientFeatureSnapshot snapshot = await GrainFactory
            .GetGrain<IProtoConditionScreeningGrain>($"PROTO-SCREEN:{PatientId}")
            .AssembleSnapshotAsync();

        string protoId = Guid.NewGuid().ToString();
        await Proto(protoId).ProposeFromPatientAsync(snapshot, workingName, byUser);

        // The proposing patient is the first suggested member — they are the reason the cluster
        // exists, and an epidemiologist reviewing the draft needs the index case in front of them.
        await Proto(protoId).SuggestMemberAsync(PatientId, byUser);
        return protoId;
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

        // ── Which provisional problem, if any, does the code replace? ──────
        // Deterministic and deliberately abstaining. A recode should close the loop on the
        // working diagnosis the patient was actually carrying while in the cluster — but
        // guessing wrong would silently retire an unrelated active diagnosis, so the rule
        // only fires when the answer is unambiguous.
        ProblemSummary? supersedes = await SelectProblemToSupersedeAsync(existing, protoConditionId, state);

        string problemId = await AssertRecodedProblemAsync(
            protoConditionId, state, dxName, dxCode, supersedes, byUser);

        // Close the superseded problem's diagnostic episode as Recoded — no clinician was
        // wrong, the code set moved. Recoded sits in neither numerator nor denominator, so a
        // mass recode cannot shift any reported revision rate.
        if (supersedes is not null)
        {
            await AdjudicateDiagnosticEpisodeAsync(
                supersedes.ProblemId, DiagnosticEpisodeOutcome.Recoded,
                dxCode, dxName, RevisionReason.Recode,
                $"Cluster '{state.Name}' promoted to {dxName}.");
        }

        string? ambiguity = supersedes is null
            ? "no unambiguous prior working diagnosis to supersede"
            : null;
        await proto.RecordMigrationAsync(
            PatientId, ProtoMigrationStatus.Migrated, problemId, ambiguity, byUser);
        await NotifyPrimaryProviderOfMigrationAsync(dxName, dxCode);
        return problemId;
    }

    /// <summary>
    /// The prior working diagnosis a promoted code replaces, or null when it cannot be
    /// determined without guessing.
    ///
    /// Rule: an ACTIVE problem whose certainty was <b>explicitly recorded as a working
    /// hypothesis</b> — Unconfirmed, Provisional or Differential. <b>Exactly one candidate
    /// supersedes; zero or several abstain.</b>
    ///
    /// <see cref="ProblemVerificationStatus.Unspecified"/> is deliberately NOT a candidate. It is
    /// the legacy default, so on an imported chart every row carries it and it tells us nothing
    /// about whether the clinician held that diagnosis provisionally. Superseding on it would be
    /// guessing. The effect is that supersession fires precisely where certainty was actually
    /// stated, and abstains everywhere else — retiring the wrong active diagnosis is far worse
    /// than leaving two rows for a human to reconcile.
    ///
    /// Certainty alone is not enough: the candidate must also be <b>related to the cluster</b>,
    /// or promoting a respiratory cluster would retire a patient's unrelated "provisional
    /// anemia" merely for being the only working hypothesis on the chart. Related means the
    /// problem's own structured evidence cites the cluster (an <see cref="EvidenceKind.ProtoCondition"/>
    /// ref to it) or shares at least one coded signal — symptom or lab — with the cluster's
    /// feature definition. A hand-entered working diagnosis with no structured evidence
    /// therefore abstains, by design: two rows for a human beats a machine retiring the wrong one.
    /// </summary>
    private async Task<ProblemSummary?> SelectProblemToSupersedeAsync(
        List<ProblemSummary> active, string protoConditionId, ProtoConditionState state)
    {
        List<ProblemSummary> candidates = active
            .Where(p => p.VerificationStatus is ProblemVerificationStatus.Unconfirmed
                        or ProblemVerificationStatus.Provisional
                        or ProblemVerificationStatus.Differential)
            .ToList();
        if (candidates.Count == 0) return null;

        HashSet<string> clusterCodes = state.Features
            .Select(f => f.Code)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        string clusterSourceId = $"PROTO:{protoConditionId}";

        var related = new List<ProblemSummary>();
        foreach (ProblemSummary candidate in candidates)
        {
            ProblemEntry? entry = await GetPatientGrain().GetProblemAsync(candidate.ProblemId);
            if (entry is null) continue;
            bool isRelated = entry.Evidence.Any(e =>
                (e.Kind == EvidenceKind.ProtoCondition
                    && string.Equals(e.SourceId, clusterSourceId, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrWhiteSpace(e.Code) && clusterCodes.Contains(e.Code.Trim())));
            if (isRelated) related.Add(candidate);
        }

        return related.Count == 1 ? related[0] : null;
    }

    /// <summary>
    /// Writes the coded problem with ADR-006 provenance: a structured
    /// <see cref="EvidenceRef"/> citing the cluster, and — when there is one — a supersession
    /// link to the working diagnosis it replaces.
    ///
    /// This replaces the previous prose comment ("Recoded from emerging cluster '{name}'
    /// (proto {id})"), which embedded a real identifier inside an English sentence and could
    /// therefore never be navigated, counted, or invalidated if the cluster were retracted.
    /// </summary>
    private async Task<string> AssertRecodedProblemAsync(
        string protoConditionId, ProtoConditionState state,
        string dxName, string dxCode, ProblemSummary? supersedes, string byUser)
    {
        var evidence = new List<EvidenceRef>
        {
            new()
            {
                Kind = EvidenceKind.ProtoCondition,
                SourceId = $"PROTO:{protoConditionId}",
                Display = $"Emerging cluster '{state.Name}' promoted to {dxName}",
                Polarity = EvidencePolarity.Supports,
                IsMachineCited = true,
                ObservedUtc = state.PromotedDate
            }
        };

        string problemId = await AddProblemAsync(
            diagnosis: dxName, diagnosisCode: dxCode, condition: null, priority: null,
            dateOfOnset: state.PromotedEffectiveFrom,
            // Attributed to the promoting user. Previously all four were null, so a coded
            // diagnosis appeared on the chart with nobody's name against it.
            providerId: byUser, providerName: byUser,
            clinicId: null, clinicName: null, isServiceConnected: false, comments: null);

        await GetPatientGrain().AssessProblemAsync(new ProblemAssessmentCommand
        {
            ProblemId = problemId,
            Evidence = evidence,
            VerificationStatus = ProblemVerificationStatus.Confirmed,
            Narrative = $"Coded from emerging cluster '{state.Name}' on promotion."
        });

        if (supersedes is not null)
        {
            await GetPatientGrain().SupersedeProblemAsync(
                supersedes.ProblemId, problemId, RevisionReason.Recode,
                $"Superseded by {dxCode} on promotion of cluster '{state.Name}'.",
                state.PromotedDate);
        }

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
