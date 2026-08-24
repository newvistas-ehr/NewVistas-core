// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Text.Json;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Services;

/// <summary>
/// Pure prompt-construction and response-parsing for a live clinical-coding assistant. Kept
/// out of the model-SDK layer so the instruction and the JSON handling are deterministic and
/// unit-testable without a network call.
///
/// The contract handed to the model: extract clinical CLAIMS — never codes. <b>The response
/// schema has no code field</b>, which enforces the design rule at the wire: a model cannot
/// hallucinate an ICD-10 code through a schema that cannot carry one. Codes are resolved
/// afterwards, deterministically, from the site's own index
/// (<see cref="Clinical.ClaimToCodeResolver"/>), and every quote the model returns is
/// re-checked verbatim against the note by <see cref="ClinicalClaimVerifier"/>.
/// </summary>
public static class ClinicalCodingJson
{
    public const string SystemPrompt =
        "You are a clinical assistant that extracts discrete clinical claims from a clinical "
        + "note so diagnosis codes can be looked up SEPARATELY. Do NOT output any codes. For "
        + "EACH clinically codable statement in the note, output an object with: term (the "
        + "condition or symptom in standard clinical vocabulary, e.g. \"muscle weakness\", "
        + "\"cervicalgia\", \"osteoporosis\"), sourceQuote (the exact sentence from the note, "
        + "copied VERBATIM — do not paraphrase), polarity (\"affirmed\" if stated present, "
        + "\"negated\" if explicitly denied such as \"no chest pain\", \"notAssessed\" if the "
        + "note says something was not measured, examined or performed), subject (\"patient\", "
        + "or \"family\" if the statement is about a relative such as \"my mother had "
        + "osteoporosis\"), temporality (\"current\", or \"history\" for a resolved past "
        + "condition such as \"history of melanoma\"), and laterality (\"left\", \"right\", "
        + "\"bilateral\", or \"\" when not stated). Extract only what the note states; do NOT "
        + "infer diagnoses, mechanisms or causes the note does not assert. Return JSON: "
        + "{\"claims\":[{\"term\":string,\"sourceQuote\":string,\"polarity\":string,"
        + "\"subject\":string,\"temporality\":string,\"laterality\":string}]}. Output JSON only.";

    /// <summary>The note becomes the user message verbatim.</summary>
    public static string BuildUserPrompt(string noteText) => noteText ?? string.Empty;

    /// <summary>JSON Schema the live model is constrained to. Deliberately contains no code field.</summary>
    public const string ResponseSchemaJson = """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "claims": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "properties": {
                  "term": { "type": "string" },
                  "sourceQuote": { "type": "string" },
                  "polarity": { "type": "string" },
                  "subject": { "type": "string" },
                  "temporality": { "type": "string" },
                  "laterality": { "type": "string" }
                },
                "required": ["term", "sourceQuote", "polarity", "subject", "temporality", "laterality"]
              }
            }
          },
          "required": ["claims"]
        }
        """;

    /// <summary>The response schema as the top-level property map structured outputs expects.</summary>
    public static Dictionary<string, JsonElement> BuildResponseSchema()
    {
        using JsonDocument doc = JsonDocument.Parse(ResponseSchemaJson);
        return doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());
    }

    /// <summary>
    /// Parses a model JSON response into claims. Tolerates code-fence wrapping and surrounding
    /// prose. Throws <see cref="FormatException"/> on unparseable output so the resilient path
    /// can fall back to the offline lexicon.
    /// </summary>
    public static CodingClaimsResult Parse(string modelText, string providerName)
    {
        string json = ExtractJsonObject(modelText);

        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            List<ClinicalClaim> claims = new();

            if (doc.RootElement.TryGetProperty("claims", out JsonElement arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement c in arr.EnumerateArray())
                {
                    string laterality = Str(c, "laterality").Trim().ToLowerInvariant();
                    claims.Add(new ClinicalClaim
                    {
                        Term = Str(c, "term"),
                        SourceQuote = Str(c, "sourceQuote"),
                        Polarity = Str(c, "polarity").Trim().ToLowerInvariant() switch
                        {
                            "negated" => EvidencePolarity.Refutes,
                            "notassessed" or "not_assessed" or "not assessed" => EvidencePolarity.NotAssessed,
                            _ => EvidencePolarity.Supports,
                        },
                        Subject = Str(c, "subject").Trim().ToLowerInvariant() is "family" or "familymember" or "family member"
                            ? ClaimSubject.FamilyMember
                            : ClaimSubject.Patient,
                        Temporality = Str(c, "temporality").Trim().ToLowerInvariant() is "history" or "past"
                            ? ClaimTemporality.History
                            : ClaimTemporality.Current,
                        Laterality = laterality is "left" or "right" or "bilateral" ? laterality : null,
                    });
                }
            }

            return new CodingClaimsResult { Claims = claims, ProviderName = providerName };
        }
        catch (JsonException ex)
        {
            throw new FormatException("Model response was not valid claims JSON.", ex);
        }
    }

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? string.Empty
            : string.Empty;

    private static string ExtractJsonObject(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new FormatException("Model response was empty.");

        int start = text.IndexOf('{');
        int end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
            throw new FormatException("Model response contained no JSON object.");
        return text[start..(end + 1)];
    }
}
