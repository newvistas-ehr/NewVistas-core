// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Xml.Linq;
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// DS4P Processor Grain — parses received C-CDA documents for DS4P security tags.
/// §170.315(b)(8) — Security tags — summary of care — receive.
///
/// Extracts document-level and section-level DS4P tags including:
/// - Confidentiality codes (N, R, V)
/// - DS4P template ID presence (2.16.840.1.113883.3.3251.1.1)
/// - Section-level security observation entries
/// - Sensitivity category codes (ETH, PSY, HIV, etc.)
/// - Obligation and refrain policy codes
///
/// Grain Key: "DS4P-PROC:{messageId}"
/// </summary>
public class Ds4pProcessorGrain : Grain, IDs4pProcessorGrain
{
    private readonly IPersistentState<Ds4pProcessorState> _state;

    public Ds4pProcessorGrain(
        [PersistentState("ds4pProcessor", "ds4pProcessorStore")]
        IPersistentState<Ds4pProcessorState> state)
    {
        _state = state;
    }

    public async Task<Ds4pAnalysisResult> AnalyzeCcdaAsync(string ccdaXml)
    {
        string key = this.GetPrimaryKeyString();
        int colonIdx = key.IndexOf(':');
        string messageId = colonIdx >= 0 ? key[(colonIdx + 1)..] : key;

        Ds4pAnalysisResult result = ParseDs4pTags(ccdaXml);

        _state.State.MessageId = messageId;
        _state.State.AnalysisResult = result;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();

        return result;
    }

    public Task<Ds4pAnalysisResult> GetAnalysisAsync()
        => Task.FromResult(_state.State.AnalysisResult ?? new Ds4pAnalysisResult());

    /// <summary>
    /// Parses a C-CDA XML document and extracts all DS4P security tags.
    /// </summary>
    private static Ds4pAnalysisResult ParseDs4pTags(string ccdaXml)
    {
        var result = new Ds4pAnalysisResult();

        XDocument doc;
        try
        {
            doc = XDocument.Parse(ccdaXml);
        }
        catch
        {
            return result; // Return empty result for unparseable XML
        }

        XNamespace hl7 = "urn:hl7-org:v3";

        // Check for DS4P template ID at document level
        IEnumerable<XElement> templateIds = doc.Descendants(hl7 + "templateId");
        foreach (XElement tid in templateIds)
        {
            string? root = tid.Attribute("root")?.Value;
            if (root == "2.16.840.1.113883.3.3251.1.1")
            {
                result.HasDs4pTemplateId = true;
                break;
            }
        }

        // Extract document-level confidentiality code
        XElement? docConfCode = doc.Root?.Element(hl7 + "confidentialityCode");
        if (docConfCode != null)
        {
            string? code = docConfCode.Attribute("code")?.Value;
            if (!string.IsNullOrEmpty(code))
            {
                result.DocumentConfidentialityCode = code;
                if (code is "R" or "V")
                    result.HasDs4pTags = true;
            }
        }

        // If DS4P template is present, also mark as having tags
        if (result.HasDs4pTemplateId)
            result.HasDs4pTags = true;

        // Parse sections for section-level security tags
        IEnumerable<XElement> sections = doc.Descendants(hl7 + "section");
        foreach (XElement section in sections)
        {
            var sectionTag = new Ds4pSectionTag();

            // Section title
            XElement? titleEl = section.Element(hl7 + "title");
            sectionTag.SectionTitle = titleEl?.Value ?? string.Empty;

            // Section code (LOINC)
            XElement? codeEl = section.Element(hl7 + "code");
            sectionTag.SectionCode = codeEl?.Attribute("code")?.Value ?? string.Empty;

            // Section-level confidentiality code
            XElement? sectionConfCode = section.Element(hl7 + "confidentialityCode");
            if (sectionConfCode != null)
            {
                string? confCode = sectionConfCode.Attribute("code")?.Value;
                if (!string.IsNullOrEmpty(confCode))
                {
                    sectionTag.ConfidentialityCode = confCode;
                    result.HasDs4pTags = true;
                }
            }

            // Look for DS4P security observation entries within the section
            IEnumerable<XElement> entries = section.Descendants(hl7 + "observation");
            foreach (XElement obs in entries)
            {
                // Check for DS4P security observation template
                bool isDs4pObs = obs.Elements(hl7 + "templateId")
                    .Any(t => t.Attribute("root")?.Value == "2.16.840.1.113883.3.3251.1.4");

                if (!isDs4pObs) continue;

                XElement? obsCode = obs.Element(hl7 + "code");
                string? obsCodeValue = obsCode?.Attribute("code")?.Value;

                XElement? valueEl = obs.Element(hl7 + "value");
                string? valueCode = valueEl?.Attribute("code")?.Value;

                if (string.IsNullOrEmpty(valueCode)) continue;

                switch (obsCodeValue)
                {
                    case "SECCLASSOBS":
                        sectionTag.ConfidentialityCode = valueCode;
                        break;
                    case "SECCATOBS":
                        sectionTag.SensitivityCodes.Add(valueCode);
                        break;
                    case "SECCONOBS":
                        string? codeSystem = valueEl?.Attribute("codeSystem")?.Value;
                        if (codeSystem == "2.16.840.1.113883.1.11.20445")
                        {
                            sectionTag.ObligationPolicies.Add(valueCode);
                            if (!result.ObligationPolicies.Contains(valueCode))
                                result.ObligationPolicies.Add(valueCode);
                        }
                        else if (codeSystem == "2.16.840.1.113883.1.11.20446")
                        {
                            sectionTag.RefrainPolicies.Add(valueCode);
                            if (!result.RefrainPolicies.Contains(valueCode))
                                result.RefrainPolicies.Add(valueCode);
                        }
                        break;
                }
            }

            // Only add sections that have DS4P tags
            if (!string.IsNullOrEmpty(sectionTag.ConfidentialityCode) ||
                sectionTag.SensitivityCodes.Count > 0 ||
                sectionTag.ObligationPolicies.Count > 0 ||
                sectionTag.RefrainPolicies.Count > 0)
            {
                result.SectionTags.Add(sectionTag);
                result.HasDs4pTags = true;
            }
        }

        return result;
    }
}
