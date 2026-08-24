// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Security;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// A ProtoCondition — a living, versioned cluster representing an emerging disease pattern before
/// it has a code. Grain key: <c>PROTO:{guid}</c>. Mutating operations require <c>EPI MANAGER</c>
/// (surfacing a patient is a lighter clinician action); reads are open (surveillance is part of
/// the chart, not a privacy silo). Definition-changing edits bump <see cref="ProtoConditionState.DefinitionVersion"/>;
/// once promoted, all matching mutators throw and only migration bookkeeping remains.
/// </summary>
public interface IProtoConditionGrain : IGrainWithStringKey
{
    // ─── Lifecycle & definition ─────────────────────────────────────────

    /// <summary>Creates the proto (name/description/creator) in Draft. Idempotent-ish: re-create updates metadata.</summary>
    [RequiresSecurityKey(SecurityKeys.EPI_MANAGER)]
    Task CreateAsync(string name, string description, string createdBy);

    /// <summary>
    /// Draft a cluster from one patient's feature snapshot — the "this matches nothing I know"
    /// gesture. Seeds features from the patient's present symptoms, negative etiologic results
    /// and abnormal labs.
    ///
    /// Deliberately open to a treating clinician (not just <c>EPI MANAGER</c>), because the
    /// person who notices that nothing fits is at the bedside, not in the epidemiology office.
    /// Nobody noticed for weeks in late 2019, and the noticing is the step that failed.
    ///
    /// <b>Always creates at <see cref="GrainStates.ProtoConditionStatus.Draft"/> and offers no
    /// way to create anything else.</b> Draft is already excluded by
    /// <c>ProtoConditionIndexGrain.GetActiveAsync</c>, so the cluster is invisible to every
    /// chart, banner and sweep until an epidemiologist activates it — assemble automatically,
    /// publish never. That matters because naming a cluster creates its population: once a named
    /// entity is visible, ambiguous cases get attributed to it, which grows the cohort, which
    /// appears to confirm it.
    /// </summary>
    [RequiresSecurityKey(SecurityKeys.PROVIDER, SecurityKeys.ORELSE, SecurityKeys.EPI_MANAGER)]
    Task<ProtoConditionState> ProposeFromPatientAsync(
        PatientFeatureSnapshot snapshot, string workingName, string proposedBy);

    /// <summary>Adds or replaces a feature (matched by <c>FeatureId</c>). Bumps the definition version.</summary>
    [RequiresSecurityKey(SecurityKeys.EPI_MANAGER)]
    Task AddOrUpdateFeatureAsync(ProtoFeature feature, string byUser);

    /// <summary>Removes a feature by id. Bumps the definition version.</summary>
    [RequiresSecurityKey(SecurityKeys.EPI_MANAGER)]
    Task RemoveFeatureAsync(string featureId, string byUser);

    /// <summary>Sets the machine-match threshold (0..1). Bumps the definition version.</summary>
    [RequiresSecurityKey(SecurityKeys.EPI_MANAGER)]
    Task SetMatchThresholdAsync(double threshold, string byUser);

    /// <summary>Moves Draft → Active (screening goes live).</summary>
    [RequiresSecurityKey(SecurityKeys.EPI_MANAGER)]
    Task ActivateAsync(string byUser);

    /// <summary>Retires the proto (removes it from the active surveillance set).</summary>
    [RequiresSecurityKey(SecurityKeys.EPI_MANAGER)]
    Task RetireAsync(string byUser, string reason);

    // ─── Guidance (recommendation only — no bed writes) ─────────────────

    /// <summary>Sets isolation / PPE / order-set guidance. Does NOT bump the definition version.</summary>
    [RequiresSecurityKey(SecurityKeys.EPI_MANAGER)]
    Task SetGuidanceAsync(BedIsolationType? isolation, string? ppeNotes, List<string> orderSetIds, string byUser);

    /// <summary>Sets the count-threshold alert rule. Does NOT bump the definition version.</summary>
    [RequiresSecurityKey(SecurityKeys.EPI_MANAGER)]
    Task SetAlertRuleAsync(ProtoAlertRule rule, string byUser);

    // ─── Membership ─────────────────────────────────────────────────────

    /// <summary>
    /// Applies a matcher evaluation (called by the screening worker). Enforces the invariants:
    /// stale-version results are dropped, Excluded is never resurrected, a Confirmed member that
    /// stops matching is flagged (never silently reversed), a machine candidate that stops matching
    /// is removed, and a human-suggested candidate persists.
    /// </summary>
    [RequiresSecurityKey(SecurityKeys.EPI_MANAGER)]
    Task UpsertEvaluationAsync(ProtoMatchResult result);

    /// <summary>Clinician surfaces a patient into the cluster as a persistent (human-sourced) candidate.</summary>
    [RequiresSecurityKey(SecurityKeys.PROVIDER, SecurityKeys.ORELSE, SecurityKeys.EPI_MANAGER)]
    Task SuggestMemberAsync(string patientId, string suggestedBy);

    /// <summary>Epidemiologist confirms a candidate into the cluster; evaluates the alert rule inline.</summary>
    [RequiresSecurityKey(SecurityKeys.EPI_MANAGER)]
    Task ConfirmMemberAsync(string patientId, string byUser);

    /// <summary>Epidemiologist excludes a patient from the cluster (permanent unless re-suggested).</summary>
    [RequiresSecurityKey(SecurityKeys.EPI_MANAGER)]
    Task ExcludeMemberAsync(string patientId, string byUser, string reason);

    // ─── Promotion & migration ──────────────────────────────────────────

    /// <summary>
    /// Promotes the (Active) proto to a real coded condition: freezes the definition, expires
    /// candidates, builds the per-confirmed-member migration log, and emits an eCR trigger so newly
    /// coded encounters flow into the official reporting pipeline.
    /// </summary>
    [RequiresSecurityKey(SecurityKeys.EPI_MANAGER)]
    [AuditAction("SURVEILLANCE", "PROMOTE", EntityType = "PROTO")]
    Task PromoteAsync(string officialName, List<string> icd10Codes, string? snomedCode,
        DateTime? effectiveFrom, List<string> jurisdictions, string notes, string byUser);

    /// <summary>Records the outcome of a member's post-promotion problem-list recode (still allowed after promotion).</summary>
    [RequiresSecurityKey(SecurityKeys.EPI_MANAGER)]
    Task RecordMigrationAsync(string patientId, ProtoMigrationStatus status, string? problemId, string? reason, string byUser);

    // ─── Reads (open) ───────────────────────────────────────────────────

    Task<ProtoConditionState> GetAsync();
    Task<List<ProtoMember>> GetMembersByStatusAsync(ProtoMemberStatus status);
    Task<int> GetConfirmedCountAsync();
    Task<ProtoConditionSummary> GetSummaryAsync();
}
