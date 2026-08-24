// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewVistas.Abstractions.Clinical;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Diagnosis provenance and revision statistics (ADR-006).
///
/// Gated by DIAGNOSTIC_STEWARDSHIP, which is on by default and one-way: once disabled it can
/// never be re-enabled, because the counters would resume against a denominator that silently
/// missed the dark period.
/// </summary>
[Authorize]
[ApiController]
[Route("api/diagnostic-stewardship")]
public class DiagnosticStewardshipController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<DiagnosticStewardshipController> _logger;

    public DiagnosticStewardshipController(
        IGrainFactory grainFactory, ILogger<DiagnosticStewardshipController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    /// <summary>
    /// The revision advisory for a working diagnosis.
    ///
    /// Display contract (see <see cref="DiagnosisRevisionAdvisory"/>): render the counts, not the
    /// percentage. <c>revisionRate</c> is null whenever <c>band</c> is Insufficient and must not
    /// be rendered then.
    /// </summary>
    [HttpGet("{patientId}/advisory")]
    public async Task<IActionResult> GetAdvisory(
        string patientId,
        [FromQuery] string code,
        [FromQuery] string? display = null,
        [FromQuery] string? problemId = null)
    {
        try
        {
            DiagnosisRevisionAdvisory advisory = await GetWorkflow(patientId)
                .GetDiagnosisRevisionAdvisoryAsync(code, display ?? code, problemId);
            return Ok(advisory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building revision advisory for {PatientId} / {Code}", patientId, code);
            return StatusCode(500, "Error building revision advisory");
        }
    }

    /// <summary>This patient's diagnostic episodes — the provenance view.</summary>
    [HttpGet("{patientId}/episodes")]
    public async Task<IActionResult> GetEpisodes(string patientId)
    {
        try
        {
            List<DiagnosticEpisode> episodes = await GetWorkflow(patientId).GetDiagnosticEpisodesAsync();
            return Ok(episodes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading diagnostic episodes for {PatientId}", patientId);
            return StatusCode(500, "Error reading diagnostic episodes");
        }
    }

    /// <summary>
    /// Adjudicate an open episode — state how the working diagnosis turned out.
    ///
    /// The outcome recorded is the clinician's own choice. Callers should offer
    /// <c>DiagnosisCodeRelation.Propose</c>'s suggestion as a default, but what is counted is
    /// what the clinician said.
    /// </summary>
    [HttpPost("{patientId}/episodes/{problemId}/adjudicate")]
    public async Task<IActionResult> Adjudicate(
        string patientId, string problemId, [FromBody] AdjudicateEpisodeRequest request)
    {
        try
        {
            bool ok = await GetWorkflow(patientId).AdjudicateDiagnosticEpisodeAsync(
                problemId, request.Outcome, request.OutcomeCode, request.OutcomeDisplay,
                request.Reason, request.OutcomeNote);

            if (!ok)
                return NotFound("No open diagnostic episode for that problem, or the feature is disabled.");

            return Ok(new { patientId, problemId, outcome = request.Outcome.ToString() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adjudicating episode {ProblemId} for {PatientId}", problemId, patientId);
            return StatusCode(500, "Error adjudicating diagnostic episode");
        }
    }

    /// <summary>
    /// What the system would propose for a (from → to) code change, so a UI can pre-select the
    /// radio button without duplicating the rule.
    /// </summary>
    [HttpGet("propose")]
    public IActionResult Propose([FromQuery] string from, [FromQuery] string to)
        => Ok(new
        {
            from,
            to,
            outcome = DiagnosisCodeRelation.Propose(from, to).ToString(),
            reason = DiagnosisCodeRelation.ProposeReason(from, to).ToString()
        });
}

public record AdjudicateEpisodeRequest
{
    public DiagnosticEpisodeOutcome Outcome { get; init; }
    public string? OutcomeCode { get; init; }
    public string? OutcomeDisplay { get; init; }
    public RevisionReason? Reason { get; init; }
    public string? OutcomeNote { get; init; }
}
