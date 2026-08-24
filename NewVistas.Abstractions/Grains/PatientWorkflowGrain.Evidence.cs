// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// The evidence façade (ADR-006): reading a problem's full assertion head — evidence list,
/// certainty, revision number, supersession links — and recording an assessment against it.
///
/// This is what makes structured evidence reachable by a clinician at all. The model shipped
/// with ADR-006, but until this façade only seed code could write an <see cref="EvidenceRef"/>
/// — the "we looked and found nothing" versus "we never looked" distinction the whole design
/// is built around existed in the data and nowhere in the product's hands.
/// </summary>
public partial class PatientWorkflowGrain
{
    /// <summary>
    /// The full problem head, including evidence. Open read, like the problem list itself —
    /// the coarse summaries deliberately omit the evidence payload, so the detail view
    /// fetches it per problem instead of shipping it on every list row.
    /// </summary>
    public Task<ProblemEntry?> GetProblemWithEvidenceAsync(string problemId)
        => GetPatientGrain().GetProblemAsync(problemId);

    /// <summary>
    /// Record an assessment: append evidence (deduped by kind+source+code) and set the
    /// certainty. Never moves the revision number — an assessment is the workup proceeding,
    /// not a clinician changing their mind. Returns the event id, or null when the problem
    /// does not exist.
    /// </summary>
    public Task<string?> AssessProblemAsync(ProblemAssessmentCommand command)
        => GetPatientGrain().AssessProblemAsync(command);
}
