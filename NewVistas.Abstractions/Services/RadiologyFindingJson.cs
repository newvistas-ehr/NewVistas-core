// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Text.Json;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Services;

/// <summary>
/// Pure prompt-construction and response-parsing for a live radiology-finding extractor.
/// Kept out of the model-SDK layer so the extraction instruction and JSON parsing are
/// deterministic and unit-testable without a network call.
///
/// The contract handed to the model: surface each discrete finding the radiologist
/// documented and quote the exact sentence verbatim (<c>sourceQuote</c>); add nothing the
/// report does not state. Whatever it returns is still checked sentence-by-sentence against
/// the report by <see cref="RadiologyFindingVerifier"/> — the prompt asks for grounding, the
/// verifier enforces it.
/// </summary>
public static class RadiologyFindingJson
{
    public const string SystemPrompt =
        "You are a clinical assistant that extracts discrete findings from a radiology report. "
        + "For EACH finding the radiologist states, output an object with: findingType (e.g. "
        + "\"Neural foraminal stenosis\", \"Central canal stenosis\", \"Disc herniation\"), level "
        + "(e.g. \"C5-C6\"), laterality (Left/Right/Bilateral/Unspecified), severity "
        + "(Minimal/Mild/Moderate/Severe), severityText (the report's exact wording, e.g. "
        + "\"moderate to severe\"), and sourceQuote (the exact sentence from the report, copied "
        + "VERBATIM — do not paraphrase). Do NOT invent findings the report does not state. Return "
        + "JSON: {\"findings\":[{\"findingType\":string,\"level\":string,\"laterality\":string,"
        + "\"severity\":string,\"severityText\":string,\"sourceQuote\":string}]}. Output JSON only.";

    /// <summary>The report becomes the user message verbatim.</summary>
    public static string BuildUserPrompt(string reportText) => reportText ?? string.Empty;

    /// <summary>JSON Schema the live model is constrained to via structured outputs.</summary>
    public const string ResponseSchemaJson = """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "findings": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "properties": {
                  "findingType": { "type": "string" },
                  "level": { "type": "string" },
                  "laterality": { "type": "string" },
                  "severity": { "type": "string" },
                  "severityText": { "type": "string" },
                  "sourceQuote": { "type": "string" }
                },
                "required": ["findingType", "level", "laterality", "severity", "severityText", "sourceQuote"]
              }
            }
          },
          "required": ["findings"]
        }
        """;

    /// <summary>The response schema as the top-level property map structured outputs expects.</summary>
    public static Dictionary<string, JsonElement> BuildResponseSchema()
    {
        using JsonDocument doc = JsonDocument.Parse(ResponseSchemaJson);
        return doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());
    }

    /// <summary>
    /// Parses a model JSON response into extracted findings. Tolerates code-fence wrapping and
    /// surrounding prose. Throws <see cref="FormatException"/> on unparseable output so the
    /// resilient path can fall back to the offline heuristic.
    /// </summary>
    public static RadiologyExtractionResult Parse(string modelText, string providerName)
    {
        string json = ExtractJsonObject(modelText);
        int n = 0;

        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            List<RadiologyFinding> findings = new();

            if (doc.RootElement.TryGetProperty("findings", out JsonElement arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement f in arr.EnumerateArray())
                {
                    string severityText = Str(f, "severityText");
                    findings.Add(new RadiologyFinding
                    {
                        FindingId = $"RF{++n}",
                        FindingType = Str(f, "findingType"),
                        Level = Str(f, "level").ToUpperInvariant(),
                        Laterality = ParseLaterality(Str(f, "laterality")),
                        Severity = ParseSeverity(Str(f, "severity"), severityText),
                        SeverityText = severityText,
                        SourceQuote = Str(f, "sourceQuote"),
                    });
                }
            }

            return new RadiologyExtractionResult { Findings = findings, ProviderName = providerName };
        }
        catch (JsonException ex)
        {
            throw new FormatException("Model response was not valid findings JSON.", ex);
        }
    }

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? string.Empty
            : string.Empty;

    private static FindingLaterality ParseLaterality(string s)
    {
        string l = s.ToLowerInvariant();
        if (l.Contains("bilateral") || (l.Contains("left") && l.Contains("right"))) return FindingLaterality.Bilateral;
        if (l.Contains("left")) return FindingLaterality.Left;
        if (l.Contains("right")) return FindingLaterality.Right;
        return FindingLaterality.Unspecified;
    }

    private static FindingSeverity ParseSeverity(string severity, string severityText)
    {
        string s = (severity + " " + severityText).ToLowerInvariant();
        if (s.Contains("severe")) return FindingSeverity.Severe;
        if (s.Contains("moderate")) return FindingSeverity.Moderate;
        if (s.Contains("mild")) return FindingSeverity.Mild;
        if (s.Contains("minimal")) return FindingSeverity.Minimal;
        return FindingSeverity.Unspecified;
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
