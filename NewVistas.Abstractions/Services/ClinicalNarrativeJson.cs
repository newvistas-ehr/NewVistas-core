// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Text;
using System.Text.Json;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Services;

/// <summary>
/// Pure prompt-construction and response-parsing for a live narrative model. Kept out
/// of the model-SDK layer so the grounding instruction and the (fiddly) JSON parsing
/// are deterministic and unit-testable without any network call.
///
/// The contract handed to the model: narrate ONLY the supplied facts and cite the
/// FactId(s) each claim is built from. Whatever the model returns is still run through
/// <see cref="ClinicalSummaryVerifier"/>, so a model that ignores the instruction has
/// its ungrounded claims flagged rather than trusted — the prompt asks for grounding,
/// the verifier enforces it.
/// </summary>
public static class ClinicalNarrativeJson
{
    /// <summary>System instruction for the summarization model.</summary>
    public const string SystemPrompt =
        "You are a clinical summarization assistant. You will be given a patient's "
        + "discrete clinical facts, each with a FactId. Write a concise summary using "
        + "ONLY those facts — never add, infer, or invent anything not present. Return a "
        + "JSON object with this exact shape: "
        + "{\"narrative\": string, \"claims\": [{\"text\": string, \"factIds\": [string]}]}. "
        + "Every claim's factIds must reference FactIds from the input. Output JSON only.";

    /// <summary>
    /// Renders the grounded context into the user message: the purpose plus the FactId-tagged
    /// fact list the model must summarize from.
    /// </summary>
    public static string BuildUserPrompt(ClinicalSummaryContext context)
    {
        StringBuilder sb = new();
        sb.Append("Purpose: ").Append(string.IsNullOrWhiteSpace(context.Purpose) ? "general summary" : context.Purpose);
        sb.Append('\n').Append("Facts:");

        if (context.Facts.Count == 0)
            sb.Append("\n(none on file)");

        foreach (ClinicalFact fact in context.Facts)
            sb.Append('\n').Append(fact.FactId).Append(" [").Append(fact.Category).Append("] ").Append(fact.Text);

        return sb.ToString();
    }

    /// <summary>
    /// Parses a model JSON response into a <see cref="NarrativeResult"/>. Tolerates code-fence
    /// wrapping and surrounding prose by extracting the outermost JSON object. Throws
    /// <see cref="FormatException"/> on anything unparseable so the resilient decorator can
    /// fall back to the grounded template.
    /// </summary>
    public static NarrativeResult Parse(string modelText, string providerName)
    {
        string json = ExtractJsonObject(modelText);

        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            string narrative = root.TryGetProperty("narrative", out JsonElement n) && n.ValueKind == JsonValueKind.String
                ? n.GetString() ?? string.Empty
                : string.Empty;

            List<SummaryClaim> claims = new();
            if (root.TryGetProperty("claims", out JsonElement claimsEl) && claimsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement c in claimsEl.EnumerateArray())
                {
                    string text = c.TryGetProperty("text", out JsonElement t) && t.ValueKind == JsonValueKind.String
                        ? t.GetString() ?? string.Empty
                        : string.Empty;

                    List<string> factIds = new();
                    if (c.TryGetProperty("factIds", out JsonElement f) && f.ValueKind == JsonValueKind.Array)
                        foreach (JsonElement id in f.EnumerateArray())
                            if (id.ValueKind == JsonValueKind.String && id.GetString() is { Length: > 0 } s)
                                factIds.Add(s);

                    claims.Add(new SummaryClaim { Text = text, SupportingFactIds = factIds });
                }
            }

            return new NarrativeResult { Narrative = narrative, Claims = claims, ProviderName = providerName };
        }
        catch (JsonException ex)
        {
            throw new FormatException("Model response was not valid summary JSON.", ex);
        }
    }

    private static string ExtractJsonObject(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new FormatException("Model response was empty.");

        int start = text.IndexOf('{');
        int end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
            throw new FormatException("Model response contained no JSON object.");

        return text.Substring(start, end - start + 1);
    }
}
