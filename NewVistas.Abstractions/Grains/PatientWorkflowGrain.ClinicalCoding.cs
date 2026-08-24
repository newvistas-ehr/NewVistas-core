// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Security;
using NewVistas.Abstractions.Services;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// ICD-10 suggestion from a clinical note (the coding-assist façade). Suggestion is read-only
/// decoration; applying a suggestion is a clinical write that goes through the same problem
/// machinery as a hand-entered problem — plus ADR-006 provenance marking it machine-cited, so
/// a suggested code stays permanently distinguishable from a clinician's own assertion.
/// </summary>
public partial class PatientWorkflowGrain
{
    /// <summary>
    /// Suggest ICD-10 candidates from a signed note's text. Pull-only: nothing is written.
    /// </summary>
    public async Task<NoteCodingSuggestions> SuggestCodesForNoteAsync(string documentId)
    {
        TiuDocumentState note = await GetNoteAsync(documentId);
        if (string.IsNullOrWhiteSpace(note.ReportText))
        {
            return new NoteCodingSuggestions
            {
                ProviderName = "none",
                GeneratedAt = DateTime.UtcNow,
            };
        }

        return await GrainFactory
            .GetGrain<IClinicalCodingWorkerGrain>(ClinicalCodingWorkerGrain.Key)
            .SuggestForTextAsync(note.ReportText);
    }

    /// <summary>
    /// The clinician accepted a suggestion: create the problem at
    /// <see cref="ProblemVerificationStatus.Unconfirmed"/> with an ADR-006 evidence citation
    /// carrying <c>IsMachineCited = true</c>, the source note id and the quoted sentence.
    /// Returns the new problem id.
    /// </summary>
    public async Task<string> ApplySuggestedCodeAsync(
        string documentId, string code, string display, string sourceQuote, EvidencePolarity polarity)
    {
        // Only an affirmed claim may become a problem. A Refutes claim is an informative
        // negative and a NotAssessed claim is a recorded gap — filing either as a diagnosis
        // would assert the opposite of what the note says. The UI never offers the button for
        // them; this guard holds regardless of caller.
        if (polarity != EvidencePolarity.Supports)
            throw new InvalidOperationException(
                "Only an affirmed (Supports) claim can be applied as a problem. Negated and "
                + "not-assessed claims are evidence, not diagnoses.");
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(display))
            throw new ArgumentException("A suggested code and its display text are required.");

        string? userId = RequestContext.Get(RequestContextKeys.UserId) as string;
        string? userName = RequestContext.Get(RequestContextKeys.UserName) as string;

        string problemId = await AddProblemAsync(
            display, code, null, null, null,
            userId, userName, null, null, false,
            "Accepted from a machine suggestion; see the cited note evidence.");

        await GetPatientGrain().AssessProblemAsync(new ProblemAssessmentCommand
        {
            ProblemId = problemId,
            VerificationStatus = ProblemVerificationStatus.Unconfirmed,
            Narrative = "Machine-suggested from note text and accepted by the clinician; "
                        + "certainty Unconfirmed pending clinical confirmation.",
            Evidence = new List<EvidenceRef>
            {
                new()
                {
                    Kind = EvidenceKind.Note,
                    SourceId = documentId,
                    CodeSystem = "ICD-10",
                    Code = code,
                    Display = $"Note text supporting {code}",
                    Polarity = EvidencePolarity.Supports,
                    IsMachineCited = true,
                    Note = sourceQuote,
                },
            },
        });

        return problemId;
    }
}
