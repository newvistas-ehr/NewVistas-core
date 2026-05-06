// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Text;
using System.Xml;
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Electronic Case Report (eICR) Grain — manages the lifecycle of a single case report.
/// §170.315(f)(5) — Electronic Case Reporting.
///
/// Lifecycle: triggered → generated → submitted → reportable/not-reportable/may-be-reportable
/// </summary>
public class EcrCaseGrain : Grain, IEcrCaseGrain
{
    private readonly IPersistentState<EcrCaseState> _state;
    private readonly IGrainFactory _grainFactory;

    public EcrCaseGrain(
        [PersistentState("ecrCaseState", "ecrCaseStore")] IPersistentState<EcrCaseState> state,
        IGrainFactory grainFactory)
    {
        _state = state;
        _grainFactory = grainFactory;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.CaseId))
            _state.State.CaseId = this.GetPrimaryKeyString();
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task CreateCaseAsync(
        string patientId,
        string triggerId,
        string conditionName,
        string triggeringCode,
        string triggeringCodeSystem,
        string triggeringDescription,
        List<string> jurisdictions,
        List<string> clinicalEvidence,
        string? responsibleProvider,
        string? facilityName)
    {
        _state.State.PatientId = patientId;
        _state.State.TriggerId = triggerId;
        _state.State.ConditionName = conditionName;
        _state.State.TriggeringCode = triggeringCode;
        _state.State.TriggeringCodeSystem = triggeringCodeSystem;
        _state.State.TriggeringDescription = triggeringDescription;
        _state.State.Jurisdictions = jurisdictions;
        _state.State.ClinicalEvidence = clinicalEvidence;
        _state.State.ResponsibleProvider = responsibleProvider;
        _state.State.FacilityName = facilityName;
        _state.State.Status = "triggered";
        _state.State.TriggeredDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        // Fetch patient name
        IPatientWorkflowGrain w = _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        PatientState patient = await w.GetPatientAsync();
        _state.State.PatientName = patient.Name;

        await _state.WriteStateAsync();
    }

    public async Task GenerateEicrAsync()
    {
        if (_state.State.Status != "triggered")
            throw new InvalidOperationException($"Cannot generate eICR in status '{_state.State.Status}'.");

        // Fetch patient demographics for the eICR
        IPatientWorkflowGrain w = _grainFactory.GetGrain<IPatientWorkflowGrain>(_state.State.PatientId);
        PatientState patient = await w.GetPatientAsync();

        _state.State.EicrDocument = GenerateEicrXml(patient, _state.State);
        _state.State.GeneratedDate = DateTime.UtcNow;
        _state.State.Status = "generated";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task MarkSubmittedAsync()
    {
        if (_state.State.Status != "generated")
            throw new InvalidOperationException($"Cannot submit eICR in status '{_state.State.Status}'.");

        _state.State.Status = "submitted";
        _state.State.SubmittedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordReportabilityResponseAsync(string determination, string responseContent)
    {
        _state.State.ReportabilityDetermination = determination;
        _state.State.ReportabilityResponse = responseContent;
        _state.State.ResponseDate = DateTime.UtcNow;
        _state.State.Status = determination; // "reportable", "not-reportable", "may-be-reportable", "no-rule-met"
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<EcrCaseState> GetCaseAsync() => Task.FromResult(_state.State);

    // ─── eICR Document Generation ────────────────────────────────────────────

    /// <summary>
    /// Generate an eICR XML document conforming to HL7 CDA eICR IG.
    /// Template: 2.16.840.1.113883.10.20.15.2 (Public Health Case Report)
    /// </summary>
    private static string GenerateEicrXml(PatientState patient, EcrCaseState ecrCase)
    {
        var sb = new StringBuilder();
        using var writer = XmlWriter.Create(sb, new XmlWriterSettings { Indent = true, OmitXmlDeclaration = false });

        writer.WriteStartDocument();
        writer.WriteStartElement("ClinicalDocument", "urn:hl7-org:v3");
        writer.WriteAttributeString("xmlns", "sdtc", null, "urn:hl7-org:sdtc");

        // Template: eICR (electronic Initial Case Report)
        WriteTemplateId(writer, "2.16.840.1.113883.10.20.15.2", "2016-12-01");
        // Template: US Realm Header
        WriteTemplateId(writer, "2.16.840.1.113883.10.20.22.1.1", "2015-08-01");

        // Document ID
        writer.WriteStartElement("id");
        writer.WriteAttributeString("root", "2.16.840.1.113883.19.5.99999.1");
        writer.WriteAttributeString("extension", ecrCase.CaseId);
        writer.WriteEndElement();

        // Code: Public Health Case Report
        WriteCode(writer, "55751-2", "2.16.840.1.113883.6.1",
            "Public Health Case Report", "LOINC");

        writer.WriteStartElement("title");
        writer.WriteString($"electronic Initial Case Report — {ecrCase.ConditionName}");
        writer.WriteEndElement();

        // Effective time
        writer.WriteStartElement("effectiveTime");
        writer.WriteAttributeString("value", ecrCase.TriggeredDate.ToString("yyyyMMddHHmmss"));
        writer.WriteEndElement();

        // Confidentiality
        writer.WriteStartElement("confidentialityCode");
        writer.WriteAttributeString("code", "N");
        writer.WriteAttributeString("codeSystem", "2.16.840.1.113883.5.25");
        writer.WriteEndElement();

        // Record target (patient)
        writer.WriteStartElement("recordTarget");
        writer.WriteStartElement("patientRole");
        writer.WriteStartElement("id");
        writer.WriteAttributeString("root", "2.16.840.1.113883.4.572");
        writer.WriteAttributeString("extension", ecrCase.PatientId);
        writer.WriteEndElement();

        writer.WriteStartElement("patient");
        WriteName(writer, patient.Name);

        if (!string.IsNullOrEmpty(patient.Sex))
        {
            writer.WriteStartElement("administrativeGenderCode");
            writer.WriteAttributeString("code", patient.Sex == "M" ? "M" : "F");
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

        // Author (facility/provider)
        if (!string.IsNullOrEmpty(ecrCase.FacilityName))
        {
            writer.WriteStartElement("author");
            writer.WriteStartElement("time");
            writer.WriteAttributeString("value", ecrCase.TriggeredDate.ToString("yyyyMMddHHmmss"));
            writer.WriteEndElement();
            writer.WriteStartElement("assignedAuthor");
            writer.WriteStartElement("id");
            writer.WriteAttributeString("nullFlavor", "NI");
            writer.WriteEndElement();
            writer.WriteStartElement("representedOrganization");
            writer.WriteStartElement("name");
            writer.WriteString(ecrCase.FacilityName);
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        // Component: structured body
        writer.WriteStartElement("component");
        writer.WriteStartElement("structuredBody");

        // Section: Reportable Condition
        writer.WriteStartElement("component");
        writer.WriteStartElement("section");
        WriteTemplateId(writer, "2.16.840.1.113883.10.20.15.2.3.2");
        WriteCode(writer, "55752-0", "2.16.840.1.113883.6.1",
            "Clinical Information", "LOINC");
        writer.WriteElementString("title", "Reportable Condition");

        // Text narrative
        writer.WriteStartElement("text");
        writer.WriteStartElement("paragraph");
        writer.WriteString($"Reportable condition detected: {ecrCase.ConditionName}");
        writer.WriteEndElement();
        writer.WriteStartElement("paragraph");
        writer.WriteString($"Triggering code: {ecrCase.TriggeringCode} ({ecrCase.TriggeringCodeSystem}) — {ecrCase.TriggeringDescription}");
        writer.WriteEndElement();
        foreach (string ev in ecrCase.ClinicalEvidence)
        {
            writer.WriteStartElement("paragraph");
            writer.WriteString(ev);
            writer.WriteEndElement();
        }
        writer.WriteEndElement(); // text

        // Entry: Problem Observation (the reportable condition)
        writer.WriteStartElement("entry");
        writer.WriteStartElement("observation");
        writer.WriteAttributeString("classCode", "OBS");
        writer.WriteAttributeString("moodCode", "EVN");
        WriteTemplateId(writer, "2.16.840.1.113883.10.20.22.4.4", "2015-08-01");
        WriteCode(writer, "ASSERTION", "2.16.840.1.113883.5.4", "Assertion", "ActCode");
        writer.WriteStartElement("value");
        writer.WriteAttributeString("xsi", "type", "http://www.w3.org/2001/XMLSchema-instance", "CD");
        writer.WriteAttributeString("code", ecrCase.TriggeringCode);
        writer.WriteAttributeString("codeSystem", ecrCase.TriggeringCodeSystem == "ICD-10"
            ? "2.16.840.1.113883.6.90" : "2.16.840.1.113883.6.96");
        writer.WriteAttributeString("displayName", ecrCase.TriggeringDescription);
        writer.WriteEndElement();
        writer.WriteEndElement(); // observation
        writer.WriteEndElement(); // entry

        writer.WriteEndElement(); // section
        writer.WriteEndElement(); // component

        // Section: Jurisdictions
        if (ecrCase.Jurisdictions.Count > 0)
        {
            writer.WriteStartElement("component");
            writer.WriteStartElement("section");
            WriteCode(writer, "77983-5", "2.16.840.1.113883.6.1",
                "Jurisdiction", "LOINC");
            writer.WriteElementString("title", "Responsible Jurisdictions");
            writer.WriteStartElement("text");
            foreach (string j in ecrCase.Jurisdictions)
            {
                writer.WriteStartElement("paragraph");
                writer.WriteString(j);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        writer.WriteEndElement(); // structuredBody
        writer.WriteEndElement(); // component

        writer.WriteEndElement(); // ClinicalDocument
        writer.WriteEndDocument();
        writer.Flush();

        return sb.ToString();
    }

    // ─── XML Helpers ──────────────────────────────────────────────────────────

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

    private static void WriteName(XmlWriter writer, string fullName)
    {
        writer.WriteStartElement("name");
        if (fullName.Contains(','))
        {
            string[] parts = fullName.Split(',', 2);
            writer.WriteElementString("family", parts[0].Trim());
            writer.WriteElementString("given", parts.Length > 1 ? parts[1].Trim() : string.Empty);
        }
        else
        {
            writer.WriteString(fullName);
        }
        writer.WriteEndElement();
    }
}

/// <summary>
/// eCR Case Index Grain — listing of all case reports.
/// </summary>
public class EcrCaseIndexGrain : Grain, IEcrCaseIndexGrain
{
    private readonly IPersistentState<EcrCaseIndexState> _state;

    public EcrCaseIndexGrain(
        [PersistentState("ecrCaseIndexState", "ecrCaseIndexStore")] IPersistentState<EcrCaseIndexState> state)
    {
        _state = state;
    }

    public async Task AddCaseAsync(EcrCaseSummary summary)
    {
        _state.State.Cases.RemoveAll(c => c.CaseId == summary.CaseId);
        _state.State.Cases.Add(summary);
        await _state.WriteStateAsync();
    }

    public async Task UpdateCaseStatusAsync(string caseId, string status, string? determination)
    {
        EcrCaseSummary? existing = _state.State.Cases.FirstOrDefault(c => c.CaseId == caseId);
        if (existing != null)
        {
            existing.Status = status;
            existing.ReportabilityDetermination = determination;
            await _state.WriteStateAsync();
        }
    }

    public Task<List<EcrCaseSummary>> GetAllCasesAsync()
        => Task.FromResult(_state.State.Cases.OrderByDescending(c => c.TriggeredDate).ToList());

    public Task<List<EcrCaseSummary>> GetCasesByStatusAsync(string status)
        => Task.FromResult(_state.State.Cases
            .Where(c => c.Status.Equals(status, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(c => c.TriggeredDate).ToList());

    public Task<List<EcrCaseSummary>> GetCasesByPatientAsync(string patientId)
        => Task.FromResult(_state.State.Cases
            .Where(c => c.PatientId == patientId)
            .OrderByDescending(c => c.TriggeredDate).ToList());

    public Task<List<EcrCaseSummary>> GetCasesByConditionAsync(string conditionName)
        => Task.FromResult(_state.State.Cases
            .Where(c => c.ConditionName.Equals(conditionName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(c => c.TriggeredDate).ToList());
}
