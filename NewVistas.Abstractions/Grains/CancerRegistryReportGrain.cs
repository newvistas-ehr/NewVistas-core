// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Text;
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Cancer Registry Report Grain — generates NAACCR abstract records from oncology data.
/// §170.315(f)(4) — Transmission to cancer registries.
///
/// Extracts structured data from OncologyTumorGrain, OncologyTreatmentGrain, and PatientGrain
/// to produce a NAACCR-formatted cancer case abstract for submission to state/central registries.
///
/// NAACCR data items reference: NAACCR Standards for Cancer Registries Volume II (v24).
///
/// Grain Key: "CR-REPORT:{reportId}"
/// </summary>
public class CancerRegistryReportGrain : Grain, ICancerRegistryReportGrain
{
    private readonly IPersistentState<CancerRegistryReportState> _state;
    private readonly IGrainFactory _grainFactory;

    public CancerRegistryReportGrain(
        [PersistentState("cancerRegistryReport", "crReportStore")]
        IPersistentState<CancerRegistryReportState> state,
        IGrainFactory grainFactory)
    {
        _state = state;
        _grainFactory = grainFactory;
    }

    public Task<CancerRegistryReportState> GetReportAsync()
        => Task.FromResult(_state.State);

    public async Task GenerateReportAsync(
        string patientId,
        string tumorId,
        string reportingFacility,
        string registrarId,
        string registrarName)
    {
        string reportId = this.GetPrimaryKeyString();

        // Fetch patient demographics
        IPatientWorkflowGrain w = _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        PatientState patient = await w.GetPatientAsync();

        // Fetch tumor data
        OncologyTumorState tumor = await w.GetOncologyTumorAsync(tumorId);

        // Fetch treatments for this tumor
        List<OncologyTreatmentIndexEntry> treatments = await w.GetOncologyTreatmentsByTumorAsync(tumorId);

        // Build treatment summary
        string treatmentSummary = string.Empty;
        DateTime? firstTreatmentDate = null;

        if (treatments.Count > 0)
        {
            treatmentSummary = string.Join(", ", treatments.Select(t => $"{t.TreatmentType}: {t.AgentName}"));
            firstTreatmentDate = treatments
                .Where(t => t.StartDate.HasValue)
                .OrderBy(t => t.StartDate)
                .FirstOrDefault()?.StartDate;
        }

        // Populate state
        _state.State.ReportId = reportId;
        _state.State.PatientId = patientId;
        _state.State.TumorId = tumorId;

        // Demographics
        _state.State.PatientName = patient.Name;
        _state.State.DateOfBirth = patient.DateOfBirth;
        _state.State.Sex = patient.Sex;
        _state.State.Race = patient.Race.Count > 0 ? patient.Race[0] : string.Empty;
        _state.State.Ssn = !string.IsNullOrEmpty(patient.SocialSecurityNumber)
            ? $"***-**-{patient.SocialSecurityNumber[^4..]}" : string.Empty;

        // Tumor data
        _state.State.PrimarySite = tumor.PrimarySite;
        _state.State.PrimarySiteText = tumor.PrimarySiteText;
        _state.State.Histology = tumor.Histology;
        _state.State.HistologyText = tumor.HistologyText;
        _state.State.Laterality = tumor.Laterality.ToString();
        _state.State.DateOfDiagnosis = tumor.DateOfDiagnosis;
        _state.State.DiagnosticConfirmation = tumor.DiagnosisBasis.ToString();
        _state.State.SequenceNumber = tumor.SequenceNumber;

        // Staging
        _state.State.ClinicalT = tumor.ClinicalT;
        _state.State.ClinicalN = tumor.ClinicalN;
        _state.State.ClinicalM = tumor.ClinicalM;
        _state.State.PathologicT = tumor.PathologicT;
        _state.State.PathologicN = tumor.PathologicN;
        _state.State.PathologicM = tumor.PathologicM;
        _state.State.StageGroup = tumor.StageGroup;
        _state.State.SeerSummaryStage = tumor.SeerSummaryStage;

        // Treatment
        _state.State.TreatmentSummary = treatmentSummary;
        _state.State.FirstTreatmentDate = firstTreatmentDate;

        // Reporting metadata
        _state.State.ReportingFacility = reportingFacility;
        _state.State.RegistrarId = registrarId;
        _state.State.RegistrarName = registrarName;
        _state.State.Status = CancerRegistryReportStatus.Generated;
        _state.State.CreatedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        // Generate NAACCR abstract
        _state.State.NaaccrAbstractContent = GenerateNaaccrAbstract(_state.State);

        await _state.WriteStateAsync();
    }

    public async Task SubmitReportAsync(string registryName, string? confirmationNumber)
    {
        _state.State.Status = CancerRegistryReportStatus.Submitted;
        _state.State.RegistryName = registryName;
        _state.State.ConfirmationNumber = confirmationNumber;
        _state.State.SubmittedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AcceptReportAsync(string? registryResponse)
    {
        _state.State.Status = CancerRegistryReportStatus.Accepted;
        _state.State.RegistryResponse = registryResponse;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RejectReportAsync(string rejectionReason)
    {
        _state.State.Status = CancerRegistryReportStatus.Rejected;
        _state.State.RejectionReason = rejectionReason;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<string> GetNaaccrAbstractAsync()
        => Task.FromResult(_state.State.NaaccrAbstractContent);

    /// <summary>
    /// Generates a NAACCR-formatted abstract in pipe-delimited flat file format.
    /// Covers key NAACCR data items per the NAACCR Volume II standard (v24).
    /// </summary>
    private static string GenerateNaaccrAbstract(CancerRegistryReportState report)
    {
        var sb = new StringBuilder();

        // NAACCR Record Header
        sb.AppendLine("NAACCR|V24|ABSTRACT");
        sb.AppendLine($"NAACCR_RECORD_TYPE|A"); // A = Abstract

        // Patient Demographics (Items 20–380)
        sb.AppendLine($"PATIENT_ID|{report.PatientId}");
        sb.AppendLine($"PATIENT_NAME|{report.PatientName}");
        sb.AppendLine($"DATE_OF_BIRTH|{FormatNaaccrDate(report.DateOfBirth)}");
        sb.AppendLine($"SEX|{MapSexCode(report.Sex)}");
        sb.AppendLine($"RACE_1|{report.Race}");
        sb.AppendLine($"SSN|{report.Ssn}");

        // Tumor Identification (Items 380–530)
        sb.AppendLine($"SEQUENCE_NUMBER|{report.SequenceNumber:00}");
        sb.AppendLine($"DATE_OF_DIAGNOSIS|{FormatNaaccrDate(report.DateOfDiagnosis)}");
        sb.AppendLine($"PRIMARY_SITE|{report.PrimarySite}");
        sb.AppendLine($"PRIMARY_SITE_TEXT|{report.PrimarySiteText}");
        sb.AppendLine($"HISTOLOGIC_TYPE|{report.Histology}");
        sb.AppendLine($"HISTOLOGY_TEXT|{report.HistologyText}");
        sb.AppendLine($"LATERALITY|{report.Laterality}");
        sb.AppendLine($"DIAGNOSTIC_CONFIRMATION|{report.DiagnosticConfirmation}");

        // Staging (Items 759–1060)
        sb.AppendLine($"SEER_SUMMARY_STAGE|{report.SeerSummaryStage ?? "9"}");
        sb.AppendLine($"CLINICAL_T|{report.ClinicalT ?? string.Empty}");
        sb.AppendLine($"CLINICAL_N|{report.ClinicalN ?? string.Empty}");
        sb.AppendLine($"CLINICAL_M|{report.ClinicalM ?? string.Empty}");
        sb.AppendLine($"PATHOLOGIC_T|{report.PathologicT ?? string.Empty}");
        sb.AppendLine($"PATHOLOGIC_N|{report.PathologicN ?? string.Empty}");
        sb.AppendLine($"PATHOLOGIC_M|{report.PathologicM ?? string.Empty}");
        sb.AppendLine($"AJCC_STAGE_GROUP|{report.StageGroup ?? string.Empty}");

        // Treatment (Items 1270–1640)
        sb.AppendLine($"DATE_FIRST_TREATMENT|{FormatNaaccrDate(report.FirstTreatmentDate)}");
        sb.AppendLine($"TREATMENT_SUMMARY|{report.TreatmentSummary}");

        // Reporting (Items 540–580)
        sb.AppendLine($"REPORTING_FACILITY|{report.ReportingFacility}");
        sb.AppendLine($"REGISTRAR_ID|{report.RegistrarId}");
        sb.AppendLine($"DATE_OF_ABSTRACT|{FormatNaaccrDate(report.CreatedDate)}");

        return sb.ToString();
    }

    private static string FormatNaaccrDate(DateTime? date)
        => date.HasValue ? date.Value.ToString("yyyyMMdd") : string.Empty;

    private static string FormatNaaccrDate(DateTime date)
        => date.ToString("yyyyMMdd");

    private static string MapSexCode(string sex) => sex.ToUpperInvariant() switch
    {
        "M" or "MALE" => "1",
        "F" or "FEMALE" => "2",
        _ => "9" // Unknown
    };
}
