// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Services;

namespace NewVistas.UnitTests;

// ─── Live-narrative seam: resilience + parsing + grounding guarantee ──────────
// Pure tests — no cluster. Cover everything about wiring a live model behind the
// seam EXCEPT the network call itself: the fallback decorator, the response parser,
// and the fact that a misbehaving model's output is still caught by verification.

[TestFixture]
public class ClinicalNarrativeAiTests
{
    private sealed class ThrowingNarrativeService : IClinicalNarrativeService
    {
        public bool IsLiveModel => true;
        public string ProviderName => "claude";
        public Task<NarrativeResult> ComposeAsync(ClinicalSummaryContext context, CancellationToken ct = default)
            => throw new InvalidOperationException("model unavailable");
    }

    private static ClinicalSummaryContext ContextWith(params (string id, string text)[] facts) => new()
    {
        PatientId = "P",
        Purpose = "pre-op",
        Facts = facts.Select(f => new ClinicalFact
        {
            FactId = f.id,
            Category = ClinicalFactCategory.Medication,
            Text = f.text,
        }).ToList(),
    };

    // ── Resilience: a failing live model degrades to the grounded template ──

    [Test]
    public async Task Resilient_WhenPrimaryThrows_FallsBackToGroundedTemplate()
    {
        IClinicalNarrativeService resilient = new ResilientClinicalNarrativeService(
            new ThrowingNarrativeService(),
            new TemplateClinicalNarrativeService());

        ClinicalSummaryContext ctx = ContextWith(("F1", "LISINOPRIL 10MG"));
        NarrativeResult result = await resilient.ComposeAsync(ctx);

        // Still produced a grounded summary (template), tagged as a fallback.
        Assert.That(result.Narrative, Does.Contain("LISINOPRIL"));
        Assert.That(result.Claims, Is.Not.Empty);
        Assert.That(result.ProviderName, Does.Contain("offline-template"));
        Assert.That(result.ProviderName, Does.Contain("fallback"));
        // The fallback's claims are grounded by construction.
        Assert.That(ClinicalSummaryVerifier.Verify(ctx, result.Claims), Is.EqualTo(0));
    }

    [Test]
    public async Task Resilient_WhenPrimarySucceeds_ReturnsPrimaryResult()
    {
        IClinicalNarrativeService resilient = new ResilientClinicalNarrativeService(
            new TemplateClinicalNarrativeService(),   // primary "succeeds"
            new TemplateClinicalNarrativeService());

        NarrativeResult result = await resilient.ComposeAsync(ContextWith(("F1", "X")));
        Assert.That(result.ProviderName, Is.EqualTo("offline-template")); // no fallback tag
    }

    // ── Parsing a live model's JSON response ───────────────────────────────

    [Test]
    public void ParseJson_ExtractsNarrativeAndCitedClaims_ThroughCodeFencesAndProse()
    {
        const string modelText =
            "Here is the summary:\n```json\n"
            + "{\"narrative\": \"Patient is on lisinopril.\", "
            + "\"claims\": [{\"text\": \"On lisinopril 10mg\", \"factIds\": [\"F2\"]}]}"
            + "\n```\nLet me know if you need more.";

        NarrativeResult result = ClinicalNarrativeJson.Parse(modelText, "claude");

        Assert.That(result.ProviderName, Is.EqualTo("claude"));
        Assert.That(result.Narrative, Is.EqualTo("Patient is on lisinopril."));
        Assert.That(result.Claims, Has.Count.EqualTo(1));
        Assert.That(result.Claims[0].SupportingFactIds, Does.Contain("F2"));
    }

    [Test]
    public void ParseJson_OnUnparseableResponse_Throws()
    {
        Assert.That(() => ClinicalNarrativeJson.Parse("the model said no", "claude"),
            Throws.TypeOf<FormatException>());
    }

    // ── The grounding guarantee still binds a live model ───────────────────

    [Test]
    public void LiveModelHallucination_IsCaughtByVerification()
    {
        // Context has only F2 on file; the "model" cites F9, which doesn't exist.
        ClinicalSummaryContext ctx = ContextWith(("F2", "LISINOPRIL 10MG"));
        const string hallucinated =
            "{\"narrative\": \"Patient is on warfarin.\", "
            + "\"claims\": [{\"text\": \"On warfarin\", \"factIds\": [\"F9\"]}]}";

        NarrativeResult result = ClinicalNarrativeJson.Parse(hallucinated, "claude");
        int flagged = ClinicalSummaryVerifier.Verify(ctx, result.Claims);

        Assert.That(flagged, Is.EqualTo(1));
        Assert.That(result.Claims[0].Verified, Is.False);
        Assert.That(result.Claims[0].VerificationNote, Does.Contain("F9"));
    }

    // ── Prompt construction exposes the FactIds the model must cite ─────────

    [Test]
    public void BuildUserPrompt_ListsFactIdsAndPurpose()
    {
        string prompt = ClinicalNarrativeJson.BuildUserPrompt(
            ContextWith(("F1", "LISINOPRIL 10MG"), ("F2", "METFORMIN 500MG")));

        Assert.That(prompt, Does.Contain("pre-op"));
        Assert.That(prompt, Does.Contain("F1"));
        Assert.That(prompt, Does.Contain("F2"));
        Assert.That(prompt, Does.Contain("LISINOPRIL 10MG"));
    }

    // ── Structured-output schema constrains the live response shape ─────────

    [Test]
    public void ResponseSchema_IsValid_AndDescribesNarrativeAndCitedClaims()
    {
        Dictionary<string, System.Text.Json.JsonElement> schema = ClinicalNarrativeJson.BuildResponseSchema();

        Assert.That(schema["type"].GetString(), Is.EqualTo("object"));
        Assert.That(schema["additionalProperties"].GetBoolean(), Is.False);

        System.Text.Json.JsonElement props = schema["properties"];
        Assert.That(props.TryGetProperty("narrative", out _), Is.True);
        Assert.That(props.TryGetProperty("claims", out _), Is.True);

        List<string?> required = schema["required"].EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.That(required, Does.Contain("narrative"));
        Assert.That(required, Does.Contain("claims"));

        // A schema-valid response (what the API now guarantees) parses cleanly.
        const string conforming =
            "{\"narrative\":\"S.\",\"claims\":[{\"text\":\"x\",\"factIds\":[\"F1\"]}]}";
        NarrativeResult result = ClinicalNarrativeJson.Parse(conforming, "claude");
        Assert.That(result.Claims[0].SupportingFactIds, Does.Contain("F1"));
    }
}
