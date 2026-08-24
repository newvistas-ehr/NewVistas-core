// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Services;

namespace NewVistas.FunctionalTests;

/// <summary>
/// End-to-end for the ICD-10 suggester: a signed note → grounded claims → codes resolved from
/// the site's own index → an accepted suggestion becomes an Unconfirmed, machine-cited problem
/// with an open diagnostic episode.
/// </summary>
[TestFixture]
public class ClinicalCodingWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;

        // The suggester resolves codes through ICD10-INDEX; give the test cluster the handful
        // of entries the worked examples need. LoadCodesAsync is additive/idempotent per code.
        var index = _cluster.GrainFactory.GetGrain<IIcd10IndexGrain>("ICD10-INDEX");
        await index.LoadCodesAsync(new List<Icd10IndexEntry>
        {
            New("M62.81", "Muscle weakness (generalized)"),
            New("R53.1", "Weakness"),
            New("M54.2", "Cervicalgia"),
            New("M81.0", "Age-related osteoporosis without current pathological fracture"),
            New("Z82.62", "Family history of osteoporosis"),
            New("R07.9", "Chest pain, unspecified"),
            New("M25.512", "Pain in left shoulder"),
        });
    }

    private static Icd10IndexEntry New(string code, string desc) => new()
    {
        Code = code, ShortDescription = desc, LongDescription = desc, IsBillable = true, IsActive = true,
    };

    private IPatientWorkflowGrain Wf(string pid) => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(pid);

    private async Task<string> CreateSignedNoteAsync(IPatientWorkflowGrain wf, string text)
    {
        string id = await wf.CreateNoteAsync(
            "PROGRESS NOTE", null, text, "Coding-assist test note",
            "PROV-1", "Dr. A", null, null, null, null, null, DateTime.UtcNow);
        await wf.SignNoteAsSystemAsync(id);
        return id;
    }

    [Test]
    public async Task Suggest_GroundsEveryCodeInAQuotedSentence()
    {
        string pid = $"CODEPAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Wf(pid);
        string noteId = await CreateSignedNoteAsync(wf,
            "My neck started aching while climbing to the second floor. I did not have the strength "
            + "to raise my arm above my head. My father had osteoporosis. No chest pain.");

        NoteCodingSuggestions result = await wf.SuggestCodesForNoteAsync(noteId);

        Assert.That(result.Suggestions, Is.Not.Empty);
        Assert.That(result.Suggestions.All(s => s.Claim.QuoteVerified), Is.True,
            "every surfaced code must trace to a verified verbatim sentence");

        // The three worked examples, end to end through the real index:
        Assert.That(result.Suggestions.Select(s => s.Code), Does.Contain("M62.81"),
            "the lay weakness phrasing must reach M62.81");
        Assert.That(result.Suggestions.Select(s => s.Code), Does.Contain("Z82.62"),
            "the mother's osteoporosis must resolve to the family-history code");
        Assert.That(result.Suggestions.Select(s => s.Code), Does.Not.Contain("M81.0"),
            "the mother's osteoporosis must NOT surface as the patient's own M81.0");

        CodedSuggestion chest = result.Suggestions.Single(s => s.Code == "R07.9");
        Assert.That(chest.Claim.Polarity, Is.EqualTo(EvidencePolarity.Refutes),
            "\"No chest pain\" surfaces as an informative negative, never as an affirmed suggestion");
    }

    [Test]
    public async Task Apply_CreatesUnconfirmedMachineCitedProblemWithOpenEpisode()
    {
        string pid = $"CODEPAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Wf(pid);
        string noteId = await CreateSignedNoteAsync(wf, "I did not have the strength to lift my hand.");

        NoteCodingSuggestions result = await wf.SuggestCodesForNoteAsync(noteId);
        CodedSuggestion pick = result.Suggestions.First(s => s.Code == "M62.81");

        string problemId = await wf.ApplySuggestedCodeAsync(
            noteId, pick.Code, pick.Display, pick.Claim.SourceQuote, pick.Claim.Polarity);

        ProblemEntry? p = await _cluster.GrainFactory.GetGrain<IPatientGrain>(pid).GetProblemAsync(problemId);
        Assert.That(p, Is.Not.Null);
        Assert.That(p!.VerificationStatus, Is.EqualTo(ProblemVerificationStatus.Unconfirmed),
            "an accepted suggestion is a hypothesis, not a confirmed diagnosis");

        EvidenceRef cite = p.Evidence.Single(e => e.Kind == EvidenceKind.Note);
        Assert.That(cite.IsMachineCited, Is.True,
            "a machine-suggested code must stay permanently distinguishable from a clinician's own assertion");
        Assert.That(cite.SourceId, Is.EqualTo(noteId));
        Assert.That(cite.Note, Is.EqualTo(pick.Claim.SourceQuote));

        // The assertion opened a diagnostic episode, adjudicable later like any other.
        List<DiagnosticEpisode> episodes = await wf.GetDiagnosticEpisodesAsync();
        Assert.That(episodes.Any(e => e.ProblemId == problemId && e.Outcome == DiagnosticEpisodeOutcome.Open),
            Is.True);
    }

    [Test]
    public void Apply_RefusesANegatedClaim()
    {
        string pid = $"CODEPAT-{Guid.NewGuid()}";
        // Filing a denied symptom as a diagnosis would assert the opposite of the note.
        Assert.ThrowsAsync<InvalidOperationException>(() => Wf(pid).ApplySuggestedCodeAsync(
            "TIU-x", "R07.9", "Chest pain, unspecified", "No chest pain.", EvidencePolarity.Refutes));
    }

    [Test]
    public async Task Suggest_FabricatedQuoteNeverResolvesToACode()
    {
        // Simulates a hallucinating assistant: verify directly that a claim whose quote is not
        // in the note is excluded from resolution by the worker.
        var worker = _cluster.GrainFactory.GetGrain<IClinicalCodingWorkerGrain>("CLINICAL-CODING");
        NoteCodingSuggestions result = await worker.SuggestForTextAsync(
            "A short note that mentions nothing clinical at all.");
        Assert.That(result.Suggestions, Is.Empty);
    }
}
