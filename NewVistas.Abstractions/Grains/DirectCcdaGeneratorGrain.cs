// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Text;
using System.Xml;
using Orleans;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Direct C-CDA Generator Grain — generates Consolidated CDA R2.1 documents.
/// Produces Continuity of Care Documents (CCD) from patient clinical data.
///
/// Template: 2.16.840.1.113883.10.20.22.1.2 (CCD)
/// Grain Key: "DIRECT-CCDA-GEN:{patientId}"
/// </summary>
public class DirectCcdaGeneratorGrain : Grain, IDirectCcdaGeneratorGrain
{
    private readonly IGrainFactory _grainFactory;

    public DirectCcdaGeneratorGrain(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    public async Task<string> GenerateCcdAsync(string documentType)
    {
        string key = this.GetPrimaryKeyString();
        int colonIdx = key.IndexOf(':');
        string patientId = colonIdx >= 0 ? key[(colonIdx + 1)..] : key;

        IPatientWorkflowGrain w = _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        PatientState patient = await w.GetPatientAsync();
        List<ProblemSummary> problems = await w.GetAllProblemsAsync();
        List<AllergySummary> allergies = await w.GetAllergiesAsync();
        List<MedicationSummary> meds = await w.GetActiveMedicationsAsync();
        List<VitalSummary> vitals = await w.GetLatestVitalsAsync();
        List<LabTestSummaryEntry> labs = await w.GetLabSummaryAsync();

        return GenerateCcdaXml(patient, problems, allergies, meds, vitals, labs, documentType);
    }

    private static string GenerateCcdaXml(
        PatientState patient,
        List<ProblemSummary> problems,
        List<AllergySummary> allergies,
        List<MedicationSummary> medications,
        List<VitalSummary> vitals,
        List<LabTestSummaryEntry> labs,
        string documentType)
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

        // Confidentiality
        writer.WriteStartElement("confidentialityCode");
        writer.WriteAttributeString("code", "N");
        writer.WriteAttributeString("codeSystem", "2.16.840.1.113883.5.25");
        writer.WriteEndElement();

        writer.WriteStartElement("languageCode");
        writer.WriteAttributeString("code", "en-US");
        writer.WriteEndElement();

        // Record target (patient)
        WriteRecordTarget(writer, patient);

        // Component: structured body
        writer.WriteStartElement("component");
        writer.WriteStartElement("structuredBody");

        // Allergies Section
        WriteAllergiesSection(writer, allergies);

        // Medications Section
        WriteMedicationsSection(writer, medications);

        // Problems Section
        WriteProblemsSection(writer, problems);

        // Vitals Section
        WriteVitalsSection(writer, vitals);

        // Results Section (Labs)
        WriteResultsSection(writer, labs);

        writer.WriteEndElement(); // structuredBody
        writer.WriteEndElement(); // component

        writer.WriteEndElement(); // ClinicalDocument
        writer.WriteEndDocument();
        writer.Flush();

        return sb.ToString();
    }

    private static void WriteRecordTarget(XmlWriter writer, PatientState patient)
    {
        writer.WriteStartElement("recordTarget");
        writer.WriteStartElement("patientRole");
        writer.WriteStartElement("id");
        writer.WriteAttributeString("root", "2.16.840.1.113883.4.572");
        writer.WriteAttributeString("extension", patient.PatientId);
        writer.WriteEndElement();

        writer.WriteStartElement("patient");

        // Name
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

        // Gender
        if (!string.IsNullOrEmpty(patient.Sex))
        {
            writer.WriteStartElement("administrativeGenderCode");
            writer.WriteAttributeString("code", patient.Sex);
            writer.WriteAttributeString("codeSystem", "2.16.840.1.113883.5.1");
            writer.WriteEndElement();
        }

        // DOB
        if (patient.DateOfBirth.HasValue)
        {
            writer.WriteStartElement("birthTime");
            writer.WriteAttributeString("value", patient.DateOfBirth.Value.ToString("yyyyMMdd"));
            writer.WriteEndElement();
        }

        // Race
        if (patient.Race.Count > 0)
        {
            writer.WriteStartElement("raceCode");
            writer.WriteAttributeString("displayName", patient.Race[0]);
            writer.WriteAttributeString("codeSystem", "2.16.840.1.113883.6.238");
            writer.WriteEndElement();
        }

        // Ethnicity
        if (patient.Ethnicity.Count > 0)
        {
            writer.WriteStartElement("ethnicGroupCode");
            writer.WriteAttributeString("displayName", patient.Ethnicity[0]);
            writer.WriteAttributeString("codeSystem", "2.16.840.1.113883.6.238");
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
        {
            writer.WriteElementString("paragraph", "No known allergies.");
        }
        else
        {
            foreach (AllergySummary allergy in allergies)
            {
                writer.WriteStartElement("paragraph");
                string reactions = allergy.Reactions.Count > 0
                    ? $" — Reactions: {string.Join(", ", allergy.Reactions)}" : string.Empty;
                writer.WriteString($"{allergy.Allergen} ({allergy.AllergenType}){reactions}");
                writer.WriteEndElement();
            }
        }
        writer.WriteEndElement(); // text

        writer.WriteEndElement(); // section
        writer.WriteEndElement(); // component
    }

    private static void WriteMedicationsSection(XmlWriter writer, List<MedicationSummary> medications)
    {
        writer.WriteStartElement("component");
        writer.WriteStartElement("section");
        WriteTemplateId(writer, "2.16.840.1.113883.10.20.22.2.1.1", "2014-06-09");
        WriteCode(writer, "10160-0", "2.16.840.1.113883.6.1", "History of Medication use", "LOINC");
        writer.WriteElementString("title", "Medications");

        writer.WriteStartElement("text");
        if (medications.Count == 0)
        {
            writer.WriteElementString("paragraph", "No active medications.");
        }
        else
        {
            foreach (MedicationSummary med in medications)
            {
                writer.WriteStartElement("paragraph");
                string sig = !string.IsNullOrEmpty(med.Sig) ? $" — {med.Sig}" : string.Empty;
                writer.WriteString($"{med.DrugName}{sig} (Status: {med.Status})");
                writer.WriteEndElement();
            }
        }
        writer.WriteEndElement();

        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteProblemsSection(XmlWriter writer, List<ProblemSummary> problems)
    {
        writer.WriteStartElement("component");
        writer.WriteStartElement("section");
        WriteTemplateId(writer, "2.16.840.1.113883.10.20.22.2.5.1", "2015-08-01");
        WriteCode(writer, "11450-4", "2.16.840.1.113883.6.1", "Problem list", "LOINC");
        writer.WriteElementString("title", "Problems");

        writer.WriteStartElement("text");
        if (problems.Count == 0)
        {
            writer.WriteElementString("paragraph", "No active problems.");
        }
        else
        {
            foreach (ProblemSummary problem in problems)
            {
                writer.WriteStartElement("paragraph");
                string code = !string.IsNullOrEmpty(problem.DiagnosisCode) ? $" ({problem.DiagnosisCode})" : string.Empty;
                writer.WriteString($"{problem.Diagnosis}{code} — {problem.Status}");
                writer.WriteEndElement();
            }
        }
        writer.WriteEndElement();

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
        {
            writer.WriteElementString("paragraph", "No vitals recorded.");
        }
        else
        {
            foreach (VitalSummary vital in vitals)
            {
                writer.WriteStartElement("paragraph");
                string units = !string.IsNullOrEmpty(vital.Units) ? $" {vital.Units}" : string.Empty;
                writer.WriteString($"{vital.VitalType}: {vital.Value}{units} ({vital.DateTimeTaken:yyyy-MM-dd})");
                writer.WriteEndElement();
            }
        }
        writer.WriteEndElement();

        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteResultsSection(XmlWriter writer, List<LabTestSummaryEntry> labs)
    {
        writer.WriteStartElement("component");
        writer.WriteStartElement("section");
        WriteTemplateId(writer, "2.16.840.1.113883.10.20.22.2.3.1", "2015-08-01");
        WriteCode(writer, "30954-2", "2.16.840.1.113883.6.1", "Relevant diagnostic tests/laboratory data", "LOINC");
        writer.WriteElementString("title", "Results");

        writer.WriteStartElement("text");
        if (labs.Count == 0)
        {
            writer.WriteElementString("paragraph", "No lab results.");
        }
        else
        {
            foreach (LabTestSummaryEntry lab in labs)
            {
                writer.WriteStartElement("paragraph");
                writer.WriteString($"{lab.TestName} (LOINC: {lab.LoincCode}): {lab.Value} {lab.Units} ({lab.ResultDate:yyyy-MM-dd})");
                writer.WriteEndElement();
            }
        }
        writer.WriteEndElement();

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
