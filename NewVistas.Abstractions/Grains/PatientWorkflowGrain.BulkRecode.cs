// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.Clinical;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// One patient's share of a code-set migration (ADR-006 "bulk recode"). The semantics are
/// pinned by construction: the revision reason is always <see cref="RevisionReason.Recode"/>
/// and any open episode closes as <see cref="DiagnosticEpisodeOutcome.Recoded"/> — never what
/// <see cref="DiagnosisCodeRelation.Propose"/> would suggest, because B34.2 → U07.1 shares no
/// prefix and would be scored a Correction, teaching the outcome shard that B34.2 was wrong
/// 100% of the time.
/// </summary>
public partial class PatientWorkflowGrain
{
    /// <summary>
    /// Serializes recodes per patient. The workflow grain is [Reentrant], so without this a
    /// population sweep interleaving with a targeted spot-fix could both pass the
    /// already-coded check before either writes, minting two replacement problems.
    /// </summary>
    private readonly SemaphoreSlim _recodeLock = new(1, 1);

    public async Task<ProblemRecodeResult> RecodeProblemCodeAsync(BulkRecodeCommand command)
    {
        await _recodeLock.WaitAsync();
        try
        {
            return await RecodeProblemCodeCoreAsync(command);
        }
        finally
        {
            _recodeLock.Release();
        }
    }

    private async Task<ProblemRecodeResult> RecodeProblemCodeCoreAsync(BulkRecodeCommand command)
    {
        string from = DiagnosisCodeRelation.Normalize(command.FromCode);
        string to = DiagnosisCodeRelation.Normalize(command.ToCode);

        List<ProblemEntry> problems = await GetPatientGrain().GetProblemsAsync();
        List<ProblemEntry> active = problems
            .Where(p => string.Equals(p.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            .ToList();

        ProblemEntry? existingTarget = active
            .FirstOrDefault(p => DiagnosisCodeRelation.Normalize(p.DiagnosisCode ?? "") == to);
        List<ProblemEntry> matches = active
            .Where(p => DiagnosisCodeRelation.Normalize(p.DiagnosisCode ?? "") == from)
            .ToList();

        // Idempotency, with a repair path. A patient carrying ONLY the replacement code is a
        // completed migration — untouched, so re-running a directive is a no-op rather than a
        // duplicate row. But a patient carrying the replacement code AND still-active old-code
        // problems is an interrupted earlier run (the write sequence is not atomic); returning
        // AlreadyCoded there would make the documented recovery — re-run the sweep — permanently
        // unable to finish the supersession. Instead, reuse the existing replacement problem as
        // the target and complete the remaining supersessions.
        if (existingTarget is not null && matches.Count == 0)
            return new ProblemRecodeResult { Outcome = ProblemRecodeOutcome.AlreadyCoded };
        if (matches.Count == 0)
            return new ProblemRecodeResult { Outcome = ProblemRecodeOutcome.NoMatch };

        ProblemEntry primary = matches[0];
        DateTime now = DateTime.UtcNow;

        string newProblemId;
        if (existingTarget is null)
        {
            // One new assertion under the replacement code, carrying the old problem's clinical
            // shape. The onset date travels — the condition did not begin when the code set changed.
            newProblemId = await AddProblemAsync(
                diagnosis: command.ToDisplay, diagnosisCode: command.ToCode,
                condition: primary.Condition, priority: primary.Priority,
                dateOfOnset: primary.DateOfOnset,
                providerId: command.RunBy, providerName: command.RunBy,
                clinicId: primary.ClinicId, clinicName: primary.ClinicName,
                isServiceConnected: primary.IsServiceConnected, comments: null);
        }
        else
        {
            newProblemId = existingTarget.ProblemId;
        }

        // The evidence for the new assertion is the old assertion itself — a recode invents no
        // clinical facts. Certainty carries over unchanged: a code-set change is not new
        // confidence, so a Provisional diagnosis stays Provisional under its new code. On the
        // repair path the target's certainty is left as it stands, and the citation dedupe
        // makes re-asserting the same evidence a no-op.
        await GetPatientGrain().AssessProblemAsync(new ProblemAssessmentCommand
        {
            ProblemId = newProblemId,
            VerificationStatus = existingTarget?.VerificationStatus ?? primary.VerificationStatus,
            Narrative = command.Narrative,
            Evidence = matches.Select(old => new EvidenceRef
            {
                Kind = EvidenceKind.Problem,
                SourceId = old.ProblemId,
                Code = command.FromCode,
                CodeSystem = "ICD-10-CM",
                Display = $"Recoded from {command.FromCode} — {old.Diagnosis}",
                Polarity = EvidencePolarity.Supports,
                IsMachineCited = true,
                ObservedUtc = now,
            }).ToList()
        });

        var result = new ProblemRecodeResult { Outcome = ProblemRecodeOutcome.Recoded, NewProblemId = newProblemId };
        foreach (ProblemEntry old in matches)
        {
            await GetPatientGrain().SupersedeProblemAsync(
                old.ProblemId, newProblemId, RevisionReason.Recode, command.Narrative, now);
            result.RecodedProblemIds.Add(old.ProblemId);

            // Close any open episode as Recoded — excluded from the revision numerator, the
            // denominator and the coverage ratio, so a mass migration moves no reported number.
            // Pre-feature and imported rows have no open episode; false here is not an error.
            if (await AdjudicateDiagnosticEpisodeAsync(
                    old.ProblemId, DiagnosticEpisodeOutcome.Recoded,
                    command.ToCode, command.ToDisplay, RevisionReason.Recode, command.Narrative))
            {
                result.EpisodesClosed++;
            }
        }

        return result;
    }
}
