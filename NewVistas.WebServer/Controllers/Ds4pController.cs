// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.AspNetCore.Mvc;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// DS4P Controller — Data Segmentation for Privacy endpoints.
/// §170.315(b)(7) — Security tags — summary of care — send.
/// §170.315(b)(8) — Security tags — summary of care — receive.
///
/// Provides endpoints for generating DS4P-tagged C-CDA documents and
/// analyzing received C-CDAs for DS4P security tags.
/// </summary>
[ApiController]
[Route("api/ds4p")]
public class Ds4pController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<Ds4pController> _logger;

    public Ds4pController(IGrainFactory grainFactory, ILogger<Ds4pController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ─── §170.315(b)(7) — DS4P Send ─────────────────────────────────────

    /// <summary>
    /// Generate a C-CDA document with DS4P security tags for the specified patient.
    /// The document-level confidentialityCode is set to "R" (Restricted) and
    /// sections containing sensitive data receive HL7 DS4P security observation entries.
    /// </summary>
    [HttpPost("generate/{patientId}")]
    public async Task<IActionResult> GenerateDs4pCcda(string patientId, [FromBody] GenerateDs4pRequest request)
    {
        try
        {
            string decodedId = Uri.UnescapeDataString(patientId);
            string ccda = await GetWorkflow(decodedId).GenerateDs4pCcdaAsync(
                request.DocumentType, request.SensitivityCategories);
            return Ok(new { PatientId = decodedId, CcdaContent = ccda });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating DS4P C-CDA for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while generating the DS4P-tagged C-CDA.");
        }
    }

    /// <summary>
    /// Get the list of supported DS4P sensitivity category codes.
    /// </summary>
    [HttpGet("sensitivity-codes")]
    public IActionResult GetSensitivityCodes()
    {
        var codes = Ds4pSensitivityCodes.AllCodes.Select(code => new
        {
            Code = code,
            DisplayName = code switch
            {
                Ds4pSensitivityCodes.SubstanceAbuse => "Substance Abuse (42 CFR Part 2)",
                Ds4pSensitivityCodes.MentalHealth => "Mental Health",
                Ds4pSensitivityCodes.Hiv => "HIV/AIDS",
                Ds4pSensitivityCodes.SexualAssault => "Sexual Assault/Domestic Violence",
                Ds4pSensitivityCodes.Sexuality => "Sexuality/Reproductive Health",
                Ds4pSensitivityCodes.Genetic => "Genetic Information (GINA)",
                Ds4pSensitivityCodes.SickleCellDisease => "Sickle Cell Disease",
                _ => code
            }
        });
        return Ok(codes);
    }

    // ─── §170.315(b)(8) — DS4P Receive ──────────────────────────────────

    /// <summary>
    /// Analyze a received C-CDA document for DS4P security tags.
    /// Extracts document-level and section-level confidentiality codes,
    /// sensitivity categories, obligation and refrain policies.
    /// </summary>
    [HttpPost("analyze/{patientId}")]
    public async Task<IActionResult> AnalyzeDs4pCcda(string patientId, [FromBody] AnalyzeDs4pRequest request)
    {
        try
        {
            string decodedId = Uri.UnescapeDataString(patientId);
            Ds4pAnalysisResult result = await GetWorkflow(decodedId)
                .AnalyzeDs4pCcdaAsync(request.MessageId, request.CcdaContent);
            return Ok(new Ds4pAnalysisResponse(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing DS4P C-CDA for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while analyzing the C-CDA for DS4P tags.");
        }
    }

    /// <summary>
    /// Retrieve a previously stored DS4P analysis result by message ID.
    /// </summary>
    [HttpGet("analysis/{patientId}/{messageId}")]
    public async Task<IActionResult> GetDs4pAnalysis(string patientId, string messageId)
    {
        try
        {
            string decodedId = Uri.UnescapeDataString(patientId);
            string decodedMsgId = Uri.UnescapeDataString(messageId);
            Ds4pAnalysisResult result = await GetWorkflow(decodedId)
                .GetDs4pAnalysisAsync(decodedMsgId);
            return Ok(new Ds4pAnalysisResponse(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving DS4P analysis for message {MessageId}", messageId);
            return StatusCode(500, "An error occurred while retrieving the DS4P analysis.");
        }
    }
}

// ─── Request DTOs ────────────────────────────────────────────────────────────

public record GenerateDs4pRequest(
    string DocumentType,
    List<string> SensitivityCategories);

public record AnalyzeDs4pRequest(
    string MessageId,
    string CcdaContent);

// ─── Response DTOs ───────────────────────────────────────────────────────────

public record Ds4pAnalysisResponse(
    bool HasDs4pTags,
    string DocumentConfidentialityCode,
    bool HasDs4pTemplateId,
    List<Ds4pSectionTagResponse> SectionTags,
    List<string> ObligationPolicies,
    List<string> RefrainPolicies,
    DateTime AnalyzedDate)
{
    public Ds4pAnalysisResponse(Ds4pAnalysisResult r) : this(
        r.HasDs4pTags,
        r.DocumentConfidentialityCode,
        r.HasDs4pTemplateId,
        r.SectionTags.Select(s => new Ds4pSectionTagResponse(s)).ToList(),
        r.ObligationPolicies,
        r.RefrainPolicies,
        r.AnalyzedDate)
    { }
}

public record Ds4pSectionTagResponse(
    string SectionTitle,
    string SectionCode,
    string ConfidentialityCode,
    List<string> SensitivityCodes,
    List<string> ObligationPolicies,
    List<string> RefrainPolicies)
{
    public Ds4pSectionTagResponse(Ds4pSectionTag s) : this(
        s.SectionTitle,
        s.SectionCode,
        s.ConfidentialityCode,
        s.SensitivityCodes,
        s.ObligationPolicies,
        s.RefrainPolicies)
    { }
}
