// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Text;
using System.Xml;
using Orleans;
using Orleans.Concurrency;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// DS4P C-CDA Generator Grain — generates C-CDA R2.1 documents with HL7 Data Segmentation
/// for Privacy (DS4P) security labels.
/// §170.315(b)(7) — Security tags — summary of care — send.
///
/// Template: 2.16.840.1.113883.3.3251.1.1 (DS4P)
/// Grain Key: "DS4P-GEN:{patientId}"
///
/// [StatelessWorker]: pure compute — reads via the workflow grain and builds
/// XML; holds nothing between calls, so concurrent requests scale out instead
/// of queuing on one activation per patient.
/// </summary>
[StatelessWorker]
public class Ds4pCcdaGeneratorGrain : Grain, IDs4pCcdaGeneratorGrain
{
    private readonly IGrainFactory _grainFactory;

    public Ds4pCcdaGeneratorGrain(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    public async Task<string> GenerateDs4pCcdAsync(string documentType, List<string> sensitivityCategories)
    {
        string key = this.GetPrimaryKeyString();
        int colonIdx = key.IndexOf(':');
        string patientId = colonIdx >= 0 ? key[(colonIdx + 1)..] : key;

        // Independent reads; the workflow grain is [Reentrant], so issue them together.
        IPatientWorkflowGrain w = _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        Task<PatientState> patientTask = w.GetPatientAsync();
        Task<List<ProblemSummary>> problemsTask = w.GetAllProblemsAsync();
        Task<List<AllergySummary>> allergiesTask = w.GetAllergiesAsync();
        Task<List<MedicationSummary>> medsTask = w.GetActiveMedicationsAsync();
        Task<List<VitalSummary>> vitalsTask = w.GetLatestVitalsAsync();
        Task<List<LabTestSummaryEntry>> labsTask = w.GetLabSummaryAsync();
        await Task.WhenAll(patientTask, problemsTask, allergiesTask, medsTask, vitalsTask, labsTask);

        return GenerateDs4pCcdaXml(
            patientTask.Result, problemsTask.Result, allergiesTask.Result,
            medsTask.Result, vitalsTask.Result, labsTask.Result,
            documentType, sensitivityCategories);
    }

    private static string GenerateDs4pCcdaXml(
        PatientState patient,
        List<ProblemSummary> problems,
        List<AllergySummary> allergies,
        List<MedicationSummary> medications,
        List<VitalSummary> vitals,
        List<LabTestSummaryEntry> labs,
        string documentType,
        List<string> sensitivityCategories)
    {
        var sb = new StringBuilder();
        using var writer = XmlWriter.Create(sb, new XmlWriterSettings { Indent = true, OmitXmlDeclaration = false });

        writer.WriteStartDocument();
        writer.WriteStartElement("ClinicalDocument", "urn:hl7-org:v3");
        writer.WriteAttributeString("xmlns", "sdtc", null, "urn:hl7-org:sdtc");
        writer.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");

        // Template: C-CDA R2.1 CCD
        WriteTemplateId(writer, "2.16.840.1.113883.10.20.22.1.1", "2015-08-01"); // US Realm Header
        WriteTemplateId(writer, "2.16.840.1.113883.10.20.22.1.2", "2015-08-01"); // CCD
        // DS4P Template
        WriteTemplateId(writer, "2.16.840.1.113883.3.3251.1.1"); // DS4P

        // Document ID
        writer.WriteStartElement("id");
        writer.WriteAttributeString("root", "2.16.840.1.113883.19.5.99999.1");
        writer.WriteAttributeString("extension", Guid.NewGuid().ToString("N"));
        writer.WriteEndElement();

        // Code
        WriteCode(writer, "34133-9", "2.16.840.1.113883.6.1",
            "Summarization of Episode Note", "LOINC");

        writer.WriteStartElement("title");
        writer.WriteString(documentType == "Referral"
            ? "Referral Summary" : documentType == "Discharge"
            ? "Discharge Summary" : "Continuity of Care Document");
        writer.WriteEndElement();

        // Effective time
        writer.WriteStartElement("effectiveTime");
        writer.WriteAttributeString("value", DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
        writer.WriteEndElement();

        // Confidentiality — "R" (Restricted) for DS4P-tagged documents
        writer.WriteStartElement("confidentialityCode");
        writer.WriteAttributeString("code", "R");
        writer.WriteAttributeString("codeSystem", "2.16.840.1.113883.5.25");
        writer.WriteAttributeString("displayName", "Restricted");
        writer.WriteEndElement();

        writer.WriteStartElement("languageCode");
        writer.WriteAttributeString("code", "en-US");
        writer.WriteEndElement();

        // Record target (patient)
        WriteRecordTarget(writer, patient);

        // Document-level security observation — DS4P privacy marking
        WriteDocumentSecurityObservation(writer, sensitivityCategories);

        // Component: structured body
        writer.WriteStartElement("component");
        writer.WriteStartElement("structuredBody");

        // Allergies Section (no sensitivity tag by default)
        WriteAllergiesSection(writer, allergies);

        // Medications Section — tagged if ETH or PSY in sensitivity categories
        bool tagMeds = sensitivityCategories.Exists(c =>
            c == Ds4pSensitivityCodes.SubstanceAbuse ||
            c == Ds4pSensitivityCodes.MentalHealth);
        WriteMedicationsSection(writer, medications, tagMeds ? sensitivityCategories : null);

        // Problems Section — tagged if any sensitivity category applies
        WriteProblemsSection(writer, problems, sensitivityCategories);

        // Vitals Section (no sensitivity tag by default)
        WriteVitalsSection(writer, vitals);

        // Results Section — tagged if HIV or Genetic
        bool tagResults = sensitivityCategories.Exists(c =>
            c == Ds4pSensitivityCodes.Hiv ||
            c == Ds4pSensitivityCodes.Genetic ||
            c == Ds4pSensitivityCodes.SickleCellDisease);
        WriteResultsSection(writer, labs, tagResults ? sensitivityCategories : null);

        writer.WriteEndElement(); // structuredBody
        writer.WriteEndElement(); // component

        writer.WriteEndElement(); // ClinicalDocument
        writer.WriteEndDocument();
        writer.Flush();

        return sb.ToString();
    }

    /// <summary>
    /// Writes a document-level authorization element containing the DS4P security observation
    /// with sensitivity category, obligation, and refrain policies.
    /// </summary>
    private static void WriteDocumentSecurityObservation(XmlWriter writer, List<string> sensitivityCategories)
    {
        writer.WriteStartElement("authorization");
        writer.WriteStartElement("consent");
        writer.WriteStartElement("code");
        writer.WriteAttributeString("code", "TREAT");
        writer.WriteAttributeString("codeSystem", "2.16.840.1.113883.5.4");
        writer.WriteAttributeString("displayName", "Treatment");
        writer.WriteEndElement(); // code

        // Status: active consent
        writer.WriteStartElement("statusCode");
        writer.WriteAttributeString("code", "active");
        writer.WriteEndElement();

        writer.WriteEndElement(); // consent
        writer.WriteEndElement(); // authorization

        // Document-level entry relationship for DS4P privacy annotations
        writer.WriteStartElement("component");
        writer.WriteStartElement("nonXMLBody");
        writer.WriteStartElement("text");
        writer.WriteAttributeString("mediaType", "text/plain");

        string catDisplay = string.Join(", ", sensitivityCategories.Select(GetSensitivityDisplayName));
        writer.WriteString($"This document contains restricted health information tagged under DS4P categories: {catDisplay}. " +
            $"Obligations: {Ds4pObligationCodes.NoRedisclosure}. " +
            "Do not re-disclose without patient authorization per 42 CFR Part 2.");
        writer.WriteEndElement(); // text
        writer.WriteEndElement(); // nonXMLBody
        writer.WriteEndElement(); // component
    }

    /// <summary>
    /// Writes a section-level security observation entry for DS4P-tagged sections.
    /// Per HL7 DS4P IG: templateId 2.16.840.1.113883.3.3251.1.4 (Security Observation).
    /// </summary>
    private static void WriteSectionSecurityObservation(XmlWriter writer, List<string> sensitivityCategories)
    {
        writer.WriteStartElement("entry");
        writer.WriteAttributeString("typeCode", "COMP");

        writer.WriteStartElement("organizer");
        writer.WriteAttributeString("classCode", "CLUSTER");
        writer.WriteAttributeString("moodCode", "EVN");

        // DS4P Security Observation template
        WriteTemplateId(writer, "2.16.840.1.113883.3.3251.1.4");

        writer.WriteStartElement("statusCode");
        writer.WriteAttributeString("code", "completed");
        writer.WriteEndElement();

        // Confidentiality security observation
        writer.WriteStartElement("component");
        writer.WriteStartElement("observation");
        writer.WriteAttributeString("classCode", "OBS");
        writer.WriteAttributeString("moodCode", "EVN");

        WriteTemplateId(writer, "2.16.840.1.113883.3.3251.1.4");
        WriteCode(writer, "SECCLASSOBS", "2.16.840.1.113883.1.11.20457",
            "Security Classification", "ObservationValue");

        writer.WriteStartElement("value");
        writer.WriteAttributeString("xsi", "type", "http://www.w3.org/2001/XMLSchema-instance", "CE");
        writer.WriteAttributeString("code", "R");
        writer.WriteAttributeString("codeSystem", "2.16.840.1.113883.5.25");
        writer.WriteAttributeString("displayName", "Restricted");
        writer.WriteEndElement(); // value

        writer.WriteEndElement(); // observation
        writer.WriteEndElement(); // component

        // Sensitivity category observations
        foreach (string category in sensitivityCategories)
        {
            writer.WriteStartElement("component");
            writer.WriteStartElement("observation");
            writer.WriteAttributeString("classCode", "OBS");
            writer.WriteAttributeString("moodCode", "EVN");

            WriteTemplateId(writer, "2.16.840.1.113883.3.3251.1.4");
            WriteCode(writer, "SECCATOBS", "2.16.840.1.113883.1.11.20457",
                "Security Category", "ObservationValue");

            writer.WriteStartElement("value");
            writer.WriteAttributeString("xsi", "type", "http://www.w3.org/2001/XMLSchema-instance", "CE");
            writer.WriteAttributeString("code", category);
            writer.WriteAttributeString("codeSystem", "2.16.840.1.113883.1.11.20428");
            writer.WriteAttributeString("displayName", GetSensitivityDisplayName(category));
            writer.WriteEndElement(); // value

            writer.WriteEndElement(); // observation
            writer.WriteEndElement(); // component
        }

        // Obligation policy — no redisclosure
        writer.WriteStartElement("component");
        writer.WriteStartElement("observation");
        writer.WriteAttributeString("classCode", "OBS");
        writer.WriteAttributeString("moodCode", "EVN");

        WriteTemplateId(writer, "2.16.840.1.113883.3.3251.1.4");
        WriteCode(writer, "SECCONOBS", "2.16.840.1.113883.1.11.20457",
            "Security Control", "ObservationValue");

        writer.WriteStartElement("value");
        writer.WriteAttributeString("xsi", "type", "http://www.w3.org/2001/XMLSchema-instance", "CE");
        writer.WriteAttributeString("code", Ds4pObligationCodes.NoRedisclosure);
        writer.WriteAttributeString("codeSystem", "2.16.840.1.113883.1.11.20445");
        writer.WriteAttributeString("displayName", "No Redisclosure without Authorization");
        writer.WriteEndElement(); // value

        writer.WriteEndElement(); // observation
        writer.WriteEndElement(); // component

        // Refrain policy — no reuse beyond original purpose
        writer.WriteStartElement("component");
        writer.WriteStartElement("observation");
        writer.WriteAttributeString("classCode", "OBS");
        writer.WriteAttributeString("moodCode", "EVN");

        WriteTemplateId(writer, "2.16.840.1.113883.3.3251.1.4");
        WriteCode(writer, "SECCONOBS", "2.16.840.1.113883.1.11.20457",
            "Security Control", "ObservationValue");

        writer.WriteStartElement("value");
        writer.WriteAttributeString("xsi", "type", "http://www.w3.org/2001/XMLSchema-instance", "CE");
        writer.WriteAttributeString("code", Ds4pRefrainCodes.NoReuse);
        writer.WriteAttributeString("codeSystem", "2.16.840.1.113883.1.11.20446");
        writer.WriteAttributeString("displayName", "No Reuse Beyond Original Purpose");
        writer.WriteEndElement(); // value

        writer.WriteEndElement(); // observation
        writer.WriteEndElement(); // component

        writer.WriteEndElement(); // organizer
        writer.WriteEndElement(); // entry
    }

    private static string GetSensitivityDisplayName(string code) => code switch
    {
        Ds4pSensitivityCodes.SubstanceAbuse => "Substance Abuse (42 CFR Part 2)",
        Ds4pSensitivityCodes.MentalHealth => "Mental Health",
        Ds4pSensitivityCodes.Hiv => "HIV/AIDS",
        Ds4pSensitivityCodes.SexualAssault => "Sexual Assault/Domestic Violence",
        Ds4pSensitivityCodes.Sexuality => "Sexuality/Reproductive Health",
        Ds4pSensitivityCodes.Genetic => "Genetic Information (GINA)",
        Ds4pSensitivityCodes.SickleCellDisease => "Sickle Cell Disease",
        _ => code
    };

    private static void WriteRecordTarget(XmlWriter writer, PatientState patient)
    {
        writer.WriteStartElement("recordTarget");
        writer.WriteStartElement("patientRole");
        writer.WriteStartElement("id");
        writer.WriteAttributeString("root", "2.16.840.1.113883.4.572");
        writer.WriteAttributeString("extension", patient.PatientId);
        writer.WriteEndElement();

        writer.WriteStartElement("patient");

        writer.WriteStartElement("name");
        if (patient.Name.Contains(','))
        {
            string[] parts = patient.Name.Split(',', 2);
            writer.WriteElementString("family", parts[0].Trim());
            writer.WriteElementString("given", parts.Length > 1 ? parts[1].Trim() : string.Empty);
        }
        else
        {
            writer.WriteString(patient.Name);
        }
        writer.WriteEndElement();

        if (!string.IsNullOrEmpty(patient.Sex))
        {
            writer.WriteStartElement("administrativeGenderCode");
            writer.WriteAttributeString("code", patient.Sex);
            writer.WriteAttributeString("codeSystem", "2.16.840.1.113883.5.1");
            writer.WriteEndElement();
        }

        if (patient.DateOfBirth.HasValue)
        {
            writer.WriteStartElement("birthTime");
            writer.WriteAttributeString("value", patient.DateOfBirth.Value.ToString("yyyyMMdd"));
            writer.WriteEndElement();
        }

        writer.WriteEndElement(); // patient
        writer.WriteEndElement(); // patientRole
        writer.WriteEndElement(); // recordTarget
    }

    private static void WriteAllergiesSection(XmlWriter writer, List<AllergySummary> allergies)
    {
        writer.WriteStartElement("component");
        writer.WriteStartElement("section");
        WriteTemplateId(writer, "2.16.840.1.113883.10.20.22.2.6.1", "2015-08-01");
        WriteCode(writer, "48765-2", "2.16.840.1.113883.6.1", "Allergies and adverse reactions", "LOINC");
        writer.WriteElementString("title", "Allergies");

        writer.WriteStartElement("text");
        if (allergies.Count == 0)
            writer.WriteElementString("paragraph", "No known allergies.");
        else
            foreach (AllergySummary allergy in allergies)
            {
                writer.WriteStartElement("paragraph");
                string reactions = allergy.Reactions.Count > 0
                    ? $" — Reactions: {string.Join(", ", allergy.Reactions)}" : string.Empty;
                writer.WriteString($"{allergy.Allergen} ({allergy.AllergenType}){reactions}");
                writer.WriteEndElement();
            }
        writer.WriteEndElement(); // text

        writer.WriteEndElement(); // section
        writer.WriteEndElement(); // component
    }

    private static void WriteMedicationsSection(XmlWriter writer, List<MedicationSummary> medications,
        List<string>? sensitivityCategories)
    {
        writer.WriteStartElement("component");
        writer.WriteStartElement("section");
        WriteTemplateId(writer, "2.16.840.1.113883.10.20.22.2.1.1", "2014-06-09");
        WriteCode(writer, "10160-0", "2.16.840.1.113883.6.1", "History of Medication use", "LOINC");
        writer.WriteElementString("title", "Medications");

        // DS4P section-level confidentiality
        if (sensitivityCategories != null)
        {
            writer.WriteStartElement("confidentialityCode");
            writer.WriteAttributeString("code", "R");
            writer.WriteAttributeString("codeSystem", "2.16.840.1.113883.5.25");
            writer.WriteAttributeString("displayName", "Restricted");
            writer.WriteEndElement();
        }

        writer.WriteStartElement("text");
        if (medications.Count == 0)
            writer.WriteElementString("paragraph", "No active medications.");
        else
            foreach (MedicationSummary med in medications)
            {
                writer.WriteStartElement("paragraph");
                string sig = !string.IsNullOrEmpty(med.Sig) ? $" — {med.Sig}" : string.Empty;
                writer.WriteString($"{med.DrugName}{sig} (Status: {med.Status})");
                writer.WriteEndElement();
            }
        writer.WriteEndElement();

        // DS4P security observation entries for this section
        if (sensitivityCategories != null)
            WriteSectionSecurityObservation(writer, sensitivityCategories);

        writer.WriteEndElement(); // section
        writer.WriteEndElement(); // component
    }

    private static void WriteProblemsSection(XmlWriter writer, List<ProblemSummary> problems,
        List<string>? sensitivityCategories)
    {
        writer.WriteStartElement("component");
        writer.WriteStartElement("section");
        WriteTemplateId(writer, "2.16.840.1.113883.10.20.22.2.5.1", "2015-08-01");
        WriteCode(writer, "11450-4", "2.16.840.1.113883.6.1", "Problem list", "LOINC");
        writer.WriteElementString("title", "Problems");

        if (sensitivityCategories != null)
        {
            writer.WriteStartElement("confidentialityCode");
            writer.WriteAttributeString("code", "R");
            writer.WriteAttributeString("codeSystem", "2.16.840.1.113883.5.25");
            writer.WriteAttributeString("displayName", "Restricted");
            writer.WriteEndElement();
        }

        writer.WriteStartElement("text");
        if (problems.Count == 0)
            writer.WriteElementString("paragraph", "No active problems.");
        else
            foreach (ProblemSummary problem in problems)
            {
                writer.WriteStartElement("paragraph");
                string code = !string.IsNullOrEmpty(problem.DiagnosisCode) ? $" ({problem.DiagnosisCode})" : string.Empty;
                writer.WriteString($"{problem.Diagnosis}{code} — {problem.Status}");
                writer.WriteEndElement();
            }
        writer.WriteEndElement();

        if (sensitivityCategories != null)
            WriteSectionSecurityObservation(writer, sensitivityCategories);

        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteVitalsSection(XmlWriter writer, List<VitalSummary> vitals)
    {
        writer.WriteStartElement("component");
        writer.WriteStartElement("section");
        WriteTemplateId(writer, "2.16.840.1.113883.10.20.22.2.4.1", "2015-08-01");
        WriteCode(writer, "8716-3", "2.16.840.1.113883.6.1", "Vital signs", "LOINC");
        writer.WriteElementString("title", "Vital Signs");

        writer.WriteStartElement("text");
        if (vitals.Count == 0)
            writer.WriteElementString("paragraph", "No vitals recorded.");
        else
            foreach (VitalSummary vital in vitals)
            {
                writer.WriteStartElement("paragraph");
                string units = !string.IsNullOrEmpty(vital.Units) ? $" {vital.Units}" : string.Empty;
                writer.WriteString($"{vital.VitalType}: {vital.Value}{units} ({vital.DateTimeTaken:yyyy-MM-dd})");
                writer.WriteEndElement();
            }
        writer.WriteEndElement();

        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteResultsSection(XmlWriter writer, List<LabTestSummaryEntry> labs,
        List<string>? sensitivityCategories)
    {
        writer.WriteStartElement("component");
        writer.WriteStartElement("section");
        WriteTemplateId(writer, "2.16.840.1.113883.10.20.22.2.3.1", "2015-08-01");
        WriteCode(writer, "30954-2", "2.16.840.1.113883.6.1", "Relevant diagnostic tests/laboratory data", "LOINC");
        writer.WriteElementString("title", "Results");

        if (sensitivityCategories != null)
        {
            writer.WriteStartElement("confidentialityCode");
            writer.WriteAttributeString("code", "R");
            writer.WriteAttributeString("codeSystem", "2.16.840.1.113883.5.25");
            writer.WriteAttributeString("displayName", "Restricted");
            writer.WriteEndElement();
        }

        writer.WriteStartElement("text");
        if (labs.Count == 0)
            writer.WriteElementString("paragraph", "No lab results.");
        else
            foreach (LabTestSummaryEntry lab in labs)
            {
                writer.WriteStartElement("paragraph");
                writer.WriteString($"{lab.TestName} (LOINC: {lab.LoincCode}): {lab.Value} {lab.Units} ({lab.ResultDate:yyyy-MM-dd})");
                writer.WriteEndElement();
            }
        writer.WriteEndElement();

        if (sensitivityCategories != null)
            WriteSectionSecurityObservation(writer, sensitivityCategories);

        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteTemplateId(XmlWriter writer, string root, string? extension = null)
    {
        writer.WriteStartElement("templateId");
        writer.WriteAttributeString("root", root);
        if (extension != null)
            writer.WriteAttributeString("extension", extension);
        writer.WriteEndElement();
    }

    private static void WriteCode(XmlWriter writer, string code, string codeSystem,
        string displayName, string codeSystemName)
    {
        writer.WriteStartElement("code");
        writer.WriteAttributeString("code", code);
        writer.WriteAttributeString("codeSystem", codeSystem);
        writer.WriteAttributeString("displayName", displayName);
        writer.WriteAttributeString("codeSystemName", codeSystemName);
        writer.WriteEndElement();
    }
}
