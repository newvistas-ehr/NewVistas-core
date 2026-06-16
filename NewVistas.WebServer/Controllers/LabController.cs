// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.WebServer.Infrastructure;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Laboratory Information System (LR) REST API.
/// Maps to VistA LAB DATA file (#63), LABORATORY TEST file (#60),
/// and MUMPS routines LRWU.m, LRFN.m, LRVER1.m.
/// </summary>
[Authorize]
[ApiController]
[Route("api/lab")]
public class LabController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<LabController> _logger;

    public LabController(IGrainFactory grainFactory, ILogger<LabController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain GetWorkflow(string patientId) =>
        _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ─── Lab Orders ──────────────────────────────────────────────────────

    /// <summary>List all lab test orders for a patient (uses LabTestGrain fan-out).</summary>
    [HttpGet("{patientId}/results")]
    public async Task<IActionResult> GetResults(string patientId)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            List<LabResultSummary> results = await GetWorkflow(pid).GetLabResultsAsync();
            return Ok(results.Select(r => new LabResultDto(r)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving lab results for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred retrieving lab results.");
        }
    }

    /// <summary>Get the full state of a single lab test order.</summary>
    [HttpGet("{patientId}/results/{labTestId}")]
    public async Task<IActionResult> GetResult(string patientId, string labTestId)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            LabTestState state = await GetWorkflow(pid).GetLabTestAsync(labTestId);
            return Ok(new LabTestDetailDto(state));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving lab test {LabTestId} for patient {PatientId}",
                labTestId, patientId);
            return StatusCode(500, "An error occurred retrieving the lab test.");
        }
    }

    /// <summary>Get the current-labs summary (one entry per test type, most recent value).</summary>
    [HttpGet("{patientId}/summary")]
    public async Task<IActionResult> GetSummary(string patientId)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            List<LabTestSummaryEntry> summary = await GetWorkflow(pid).GetLabSummaryAsync();
            return Ok(summary.Select(e => new LabSummaryDto(e)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving lab summary for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred retrieving the lab summary.");
        }
    }

    /// <summary>Get all test types where the most recent result is flagged abnormal.</summary>
    [HttpGet("{patientId}/abnormal")]
    public async Task<IActionResult> GetAbnormal(string patientId)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            List<LabTestSummaryEntry> abnormal = await GetWorkflow(pid).GetAbnormalLabResultsAsync();
            return Ok(abnormal.Select(e => new LabSummaryDto(e)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving abnormal labs for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred retrieving abnormal lab results.");
        }
    }

    // ─── Lab Order Lifecycle ─────────────────────────────────────────────

    /// <summary>Place a new lab test order. LRWU ORDER.</summary>
    [HttpPost("{patientId}/orders")]
    public async Task<IActionResult> OrderTest(string patientId, [FromBody] LabOrderRequest req)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            string labTestId = await GetWorkflow(pid).OrderLabTestAsync(
                req.TestId, req.TestName, req.TestCode,
                req.OrderId, req.OrderingProviderId, req.OrderingProviderName,
                req.SpecimenType, req.Category);
            return Created($"api/lab/{patientId}/results/{labTestId}", new { labTestId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ordering lab test for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred placing the lab order.");
        }
    }

    /// <summary>Record specimen collection for an existing lab order. LRFN COLLECT.</summary>
    [HttpPost("{patientId}/orders/{labTestId}/collect")]
    public async Task<IActionResult> CollectSpecimen(
        string patientId, string labTestId, [FromBody] LabCollectRequest req)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            await GetWorkflow(pid).CollectSpecimenAsync(
                labTestId, req.CollectionDateTime, req.CollectionSample, req.PerformingLab);
            return Ok(new { message = "Specimen collection recorded." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording specimen collection for {LabTestId}", labTestId);
            return StatusCode(500, "An error occurred recording specimen collection.");
        }
    }

    /// <summary>Record result values for a collected specimen. LRVER1 RESULT.</summary>
    [HttpPost("{patientId}/orders/{labTestId}/result")]
    public async Task<IActionResult> RecordResult(
        string patientId, string labTestId, [FromBody] LabResultRequest req)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            await GetWorkflow(pid).RecordLabResultAsync(
                labTestId, req.ResultDateTime, req.ResultValue,
                req.ResultUnit, req.ReferenceLow, req.ReferenceHigh, req.AbnormalFlag);
            return Ok(new { message = "Lab result recorded." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording lab result for {LabTestId}", labTestId);
            return StatusCode(500, "An error occurred recording the lab result.");
        }
    }

    /// <summary>Verify a completed lab result. LRVER1 VERIFY.</summary>
    [HttpPost("{patientId}/orders/{labTestId}/verify")]
    public async Task<IActionResult> VerifyResult(
        string patientId, string labTestId, [FromBody] LabVerifyRequest req)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            await GetWorkflow(pid).VerifyLabResultAsync(
                labTestId, req.VerifyingProviderId, req.VerifyingProviderName, req.VerifiedDateTime);
            return Ok(new { message = "Lab result verified." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying lab result for {LabTestId}", labTestId);
            return StatusCode(500, "An error occurred verifying the lab result.");
        }
    }

    // ─── Ingestion (HL7-style drop) ──────────────────────────────────────

    /// <summary>Ingest a result via the LabResultIngestion grain (HL7 feed / VistA extract).</summary>
    [HttpPost("{patientId}/ingest")]
    public async Task<IActionResult> IngestResult(
        string patientId, [FromBody] LabIngestRequest req)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            await GetWorkflow(pid).IngestLabResultAsync(
                req.ResultId, req.LoincCode, req.TestName,
                req.Value, req.Units, req.ReferenceRange,
                req.AbnormalFlag, req.ResultDate,
                req.FacilityCode, req.FacilityName, req.Specimen, req.PanelName);
            return Ok(new { message = "Lab result ingested." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ingesting lab result for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred ingesting the lab result.");
        }
    }

    // ─── Demo Data ───────────────────────────────────────────────────────

    /// <summary>
    /// Load realistic demo lab data: CBC, BMP, and LFT panels with results and verification.
    /// </summary>
    [HttpPost("demo/load")]
    public async Task<IActionResult> LoadDemo([FromQuery] string patientId = "PATIENT-DEMO-001")
    {
        var saved = DemoSeedHelper.SetSystemContext();
        try
        {
            IPatientWorkflowGrain wf = GetWorkflow(patientId);
            DateTime now = DateTime.UtcNow;
            string providerId = "PROVIDER-001";
            string providerName = "Dr. Smith";
            string verifierId = "PATHOLOGIST-001";
            string verifierName = "Dr. Pathologist";

            // ── CBC Panel ──────────────────────────────────────────────
            string wbcId = await wf.OrderLabTestAsync("LR-6690-2", "WBC", "6690-2",
                null, providerId, providerName, "Blood", "HEMATOLOGY");
            await wf.CollectSpecimenAsync(wbcId, now.AddHours(-2), "LAVENDER", "Hematology Lab");
            await wf.RecordLabResultAsync(wbcId, now.AddHours(-1), "7.5", "K/cmm", "4.5", "11.0", null);
            await wf.VerifyLabResultAsync(wbcId, verifierId, verifierName, now.AddMinutes(-30));

            string rbcId = await wf.OrderLabTestAsync("LR-789-8", "RBC", "789-8",
                null, providerId, providerName, "Blood", "HEMATOLOGY");
            await wf.CollectSpecimenAsync(rbcId, now.AddHours(-2), "LAVENDER", "Hematology Lab");
            await wf.RecordLabResultAsync(rbcId, now.AddHours(-1), "4.8", "M/cmm", "4.7", "6.1", null);
            await wf.VerifyLabResultAsync(rbcId, verifierId, verifierName, now.AddMinutes(-30));

            string hgbId = await wf.OrderLabTestAsync("LR-718-7", "HGB", "718-7",
                null, providerId, providerName, "Blood", "HEMATOLOGY");
            await wf.CollectSpecimenAsync(hgbId, now.AddHours(-2), "LAVENDER", "Hematology Lab");
            await wf.RecordLabResultAsync(hgbId, now.AddHours(-1), "14.2", "g/dL", "14.0", "18.0", null);
            await wf.VerifyLabResultAsync(hgbId, verifierId, verifierName, now.AddMinutes(-30));

            string hctId = await wf.OrderLabTestAsync("LR-4544-3", "HCT", "4544-3",
                null, providerId, providerName, "Blood", "HEMATOLOGY");
            await wf.CollectSpecimenAsync(hctId, now.AddHours(-2), "LAVENDER", "Hematology Lab");
            await wf.RecordLabResultAsync(hctId, now.AddHours(-1), "42.5", "%", "42.0", "52.0", null);
            await wf.VerifyLabResultAsync(hctId, verifierId, verifierName, now.AddMinutes(-30));

            string pltId = await wf.OrderLabTestAsync("LR-777-3", "PLT", "777-3",
                null, providerId, providerName, "Blood", "HEMATOLOGY");
            await wf.CollectSpecimenAsync(pltId, now.AddHours(-2), "LAVENDER", "Hematology Lab");
            await wf.RecordLabResultAsync(pltId, now.AddHours(-1), "220", "K/cmm", "150", "400", null);
            await wf.VerifyLabResultAsync(pltId, verifierId, verifierName, now.AddMinutes(-30));

            // ── BMP Panel ─────────────────────────────────────────────
            string naId = await wf.OrderLabTestAsync("LR-2951-2", "Sodium", "2951-2",
                null, providerId, providerName, "Serum", "CHEMISTRY");
            await wf.CollectSpecimenAsync(naId, now.AddHours(-2), "MARBLED TOP", "Chemistry Lab");
            await wf.RecordLabResultAsync(naId, now.AddHours(-1), "138", "mEq/L", "136", "145", null);
            await wf.VerifyLabResultAsync(naId, verifierId, verifierName, now.AddMinutes(-30));

            string kId = await wf.OrderLabTestAsync("LR-2823-3", "Potassium", "2823-3",
                null, providerId, providerName, "Serum", "CHEMISTRY");
            await wf.CollectSpecimenAsync(kId, now.AddHours(-2), "MARBLED TOP", "Chemistry Lab");
            await wf.RecordLabResultAsync(kId, now.AddHours(-1), "4.1", "mEq/L", "3.5", "5.0", null);
            await wf.VerifyLabResultAsync(kId, verifierId, verifierName, now.AddMinutes(-30));

            string bunId = await wf.OrderLabTestAsync("LR-3094-0", "BUN", "3094-0",
                null, providerId, providerName, "Serum", "CHEMISTRY");
            await wf.CollectSpecimenAsync(bunId, now.AddHours(-2), "MARBLED TOP", "Chemistry Lab");
            await wf.RecordLabResultAsync(bunId, now.AddHours(-1), "18", "mg/dL", "7", "20", null);
            await wf.VerifyLabResultAsync(bunId, verifierId, verifierName, now.AddMinutes(-30));

            string creatId = await wf.OrderLabTestAsync("LR-2160-0", "Creatinine", "2160-0",
                null, providerId, providerName, "Serum", "CHEMISTRY");
            await wf.CollectSpecimenAsync(creatId, now.AddHours(-2), "MARBLED TOP", "Chemistry Lab");
            await wf.RecordLabResultAsync(creatId, now.AddHours(-1), "1.1", "mg/dL", "0.7", "1.3", null);
            await wf.VerifyLabResultAsync(creatId, verifierId, verifierName, now.AddMinutes(-30));

            // ── LFT Panel — with one abnormal result ────────────────────
            string altId = await wf.OrderLabTestAsync("LR-1742-6", "ALT", "1742-6",
                null, providerId, providerName, "Serum", "CHEMISTRY");
            await wf.CollectSpecimenAsync(altId, now.AddHours(-2), "MARBLED TOP", "Chemistry Lab");
            await wf.RecordLabResultAsync(altId, now.AddHours(-1), "78", "IU/L", "0", "40", "H");
            await wf.VerifyLabResultAsync(altId, verifierId, verifierName, now.AddMinutes(-30));

            string astId = await wf.OrderLabTestAsync("LR-1920-8", "AST", "1920-8",
                null, providerId, providerName, "Serum", "CHEMISTRY");
            await wf.CollectSpecimenAsync(astId, now.AddHours(-2), "MARBLED TOP", "Chemistry Lab");
            await wf.RecordLabResultAsync(astId, now.AddHours(-1), "65", "IU/L", "0", "40", "H");
            await wf.VerifyLabResultAsync(astId, verifierId, verifierName, now.AddMinutes(-30));

            string tbiliId = await wf.OrderLabTestAsync("LR-1975-2", "Total Bilirubin", "1975-2",
                null, providerId, providerName, "Serum", "CHEMISTRY");
            await wf.CollectSpecimenAsync(tbiliId, now.AddHours(-2), "MARBLED TOP", "Chemistry Lab");
            await wf.RecordLabResultAsync(tbiliId, now.AddHours(-1), "1.0", "mg/dL", "0.3", "1.2", null);
            await wf.VerifyLabResultAsync(tbiliId, verifierId, verifierName, now.AddMinutes(-30));

            // ── Ingest a historical CBC via ingestion grain ────────────
            await wf.IngestLabResultAsync($"HIST-WBC-{Guid.NewGuid()}", "6690-2", "WBC",
                "6.2", "K/cmm", "4.5-11.0", LabAbnormalFlag.Normal,
                DateTimeOffset.UtcNow.AddDays(-30), "688", "Washington DC VAMC", "Blood", "CBC");
            await wf.IngestLabResultAsync($"HIST-HGB-{Guid.NewGuid()}", "718-7", "HGB",
                "13.8", "g/dL", "14.0-18.0", LabAbnormalFlag.Low,
                DateTimeOffset.UtcNow.AddDays(-30), "688", "Washington DC VAMC", "Blood", "CBC");
            await wf.IngestLabResultAsync($"HIST-CREAT-{Guid.NewGuid()}", "2160-0", "Creatinine",
                "1.3", "mg/dL", "0.7-1.3", LabAbnormalFlag.Normal,
                DateTimeOffset.UtcNow.AddDays(-30), "688", "Washington DC VAMC", "Serum", "BMP");

            return Ok(new
            {
                message = "Demo lab data loaded",
                patientId,
                orderedTests = 12,
                panels = new[] { "CBC (5)", "BMP (4)", "LFT (3)" },
                abnormalResults = new[] { "ALT (H)", "AST (H)", "HGB 30d ago (L)" }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading lab demo data for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred loading demo data.");
        }
        finally
        {
            DemoSeedHelper.RestoreContext(saved);
        }
    }
}

// ─── DTOs ─────────────────────────────────────────────────────────────────────

public record LabResultDto(
    string LabTestId,
    string TestName,
    string? ResultValue,
    string? Units,
    string? Flag,
    string Status,
    DateTime? CollectionDate)
{
    public LabResultDto(LabResultSummary s)
        : this(s.LabTestId, s.TestName, s.ResultValue, s.Units, s.Flag, s.Status, s.CollectionDate) { }
}

public record LabTestDetailDto(
    string LabTestId,
    string PatientId,
    string TestName,
    string? TestCode,
    string? OrderId,
    string? SpecimenType,
    string? Category,
    string? OrderingProviderName,
    DateTime? CollectionDateTime,
    DateTime? ResultDateTime,
    string? ResultValue,
    string? ResultUnit,
    string? ReferenceRangeLow,
    string? ReferenceRangeHigh,
    string? AbnormalFlag,
    string Status,
    bool IsCritical,
    string? VerifyingProviderName,
    DateTime? VerifiedDateTime,
    string? Comments)
{
    public LabTestDetailDto(LabTestState s)
        : this(s.LabTestId, s.PatientId, s.TestName, s.TestCode, s.OrderId,
               s.SpecimenType, s.Category, s.OrderingProviderName,
               s.CollectionDateTime, s.ResultDateTime, s.ResultValue, s.ResultUnit,
               s.ReferenceRangeLow, s.ReferenceRangeHigh, s.AbnormalFlag,
               s.Status, s.IsCritical, s.VerifyingProviderName,
               s.VerifiedDateTime, s.Comments) { }
}

public record LabSummaryDto(
    string LoincCode,
    string TestName,
    string Value,
    string Units,
    string ReferenceRange,
    string AbnormalFlag,
    DateTimeOffset ResultDate,
    string FacilityCode,
    List<LabTrendDto> RecentTrend)
{
    public LabSummaryDto(LabTestSummaryEntry e)
        : this(e.LoincCode, e.TestName, e.Value, e.Units, e.ReferenceRange,
               e.AbnormalFlag.ToString(), e.ResultDate, e.FacilityCode,
               e.RecentTrend.Select(t => new LabTrendDto(t.Value, t.ResultDate, t.AbnormalFlag.ToString()))
                             .ToList()) { }
}

public record LabTrendDto(string Value, DateTimeOffset ResultDate, string AbnormalFlag);

public record LabOrderRequest(
    string TestId,
    string TestName,
    string? TestCode,
    string? OrderId,
    string? OrderingProviderId,
    string? OrderingProviderName,
    string? SpecimenType,
    string? Category);

public record LabCollectRequest(
    DateTime CollectionDateTime,
    string? CollectionSample,
    string? PerformingLab);

public record LabResultRequest(
    DateTime ResultDateTime,
    string ResultValue,
    string? ResultUnit,
    string? ReferenceLow,
    string? ReferenceHigh,
    string? AbnormalFlag);

public record LabVerifyRequest(
    string VerifyingProviderId,
    string VerifyingProviderName,
    DateTime VerifiedDateTime);

public record LabIngestRequest(
    string ResultId,
    string LoincCode,
    string TestName,
    string Value,
    string Units,
    string? ReferenceRange,
    LabAbnormalFlag AbnormalFlag,
    DateTimeOffset ResultDate,
    string FacilityCode,
    string? FacilityName,
    string? Specimen,
    string? PanelName);
