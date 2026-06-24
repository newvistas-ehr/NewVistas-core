// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Services;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Drug Safety Advisory API — manage FDA-sourced patient safety warnings keyed by
/// VA drug class, and dispatch them (provider-reviewed and optionally edited) to
/// affected patients, recording a verbatim receipt for each.
///
/// Flow: ingest candidate warnings (openFDA/DailyMed/MedWatch via
/// <see cref="IFdaDrugWarningSource"/>) → pharmacy/informatics promotes one to an
/// advisory → a provider reviews it against their affected-patient cohort, edits the
/// message, and sends. Cohort resolution (patients on a target class within the
/// provider's panel) is intentionally left to the caller here — see the note on
/// <see cref="Dispatch"/>.
/// </summary>
[Authorize]
[ApiController]
[Route("api/drugsafetyadvisory")]
[Produces("application/json")]
public class DrugSafetyAdvisoryController : ControllerBase
{
    private const string IndexKey = "DSA-INDEX";

    private readonly IGrainFactory _grainFactory;
    private readonly IFdaDrugWarningSource _warningSource;
    private readonly ILogger<DrugSafetyAdvisoryController> _logger;

    public DrugSafetyAdvisoryController(
        IGrainFactory grainFactory,
        IFdaDrugWarningSource warningSource,
        ILogger<DrugSafetyAdvisoryController> logger)
    {
        _grainFactory = grainFactory;
        _warningSource = warningSource;
        _logger = logger;
    }

    private IDrugSafetyAdvisoryGrain Advisory(string id) =>
        _grainFactory.GetGrain<IDrugSafetyAdvisoryGrain>(id);

    private IDrugSafetyAdvisoryIndexGrain Index() =>
        _grainFactory.GetGrain<IDrugSafetyAdvisoryIndexGrain>(IndexKey);

    private IPatientSafetyAdvisoryGrain PatientLog(string patientId) =>
        _grainFactory.GetGrain<IPatientSafetyAdvisoryGrain>(patientId);

    // ─── Candidate ingestion (admin / informatics) ──────────────────────────────

    /// <summary>
    /// Returns candidate warnings surfaced from FDA/NLM sources, awaiting review and
    /// VA drug-class mapping before promotion to an advisory.
    /// </summary>
    [HttpGet("candidates")]
    [ProducesResponseType(typeof(List<FdaDrugWarningDraft>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<FdaDrugWarningDraft>>> GetCandidates()
    {
        try
        {
            return Ok(await _warningSource.FetchCandidateWarningsAsync(HttpContext.RequestAborted));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching FDA candidate warnings");
            return StatusCode(500, "An error occurred while fetching candidate warnings.");
        }
    }

    // ─── Advisory list / detail ─────────────────────────────────────────────────

    /// <summary>Lists active advisories (the provider dashboard feed).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<DrugSafetyAdvisorySummary>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<DrugSafetyAdvisorySummary>>> GetActive([FromQuery] string? drugClassCode = null)
    {
        try
        {
            List<DrugSafetyAdvisorySummary> results = string.IsNullOrWhiteSpace(drugClassCode)
                ? await Index().GetActiveAsync()
                : await Index().GetActiveByDrugClassAsync(drugClassCode);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing safety advisories");
            return StatusCode(500, "An error occurred while listing advisories.");
        }
    }

    /// <summary>Returns the full advisory state.</summary>
    [HttpGet("{advisoryId}")]
    [ProducesResponseType(typeof(DrugSafetyAdvisoryState), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DrugSafetyAdvisoryState>> GetDetail(string advisoryId)
    {
        try
        {
            DrugSafetyAdvisoryState state = await Advisory(advisoryId).GetAsync();
            if (string.IsNullOrEmpty(state.Title))
                return NotFound($"Advisory '{advisoryId}' not found.");
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving advisory {AdvisoryId}", advisoryId);
            return StatusCode(500, "An error occurred while retrieving the advisory.");
        }
    }

    // ─── Authoring & lifecycle (pharmacy / informatics) ─────────────────────────

    /// <summary>Creates or replaces an advisory (Draft until activated).</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Save([FromBody] SaveAdvisoryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.AdvisoryId) || string.IsNullOrWhiteSpace(request.Title))
            return BadRequest("AdvisoryId and Title are required.");
        if (request.TargetDrugClassCodes is null || request.TargetDrugClassCodes.Count == 0)
            return BadRequest("At least one target drug class code is required.");

        try
        {
            await Advisory(request.AdvisoryId).SaveAsync(new DrugSafetyAdvisoryState
            {
                AdvisoryId = request.AdvisoryId,
                Title = request.Title,
                SourceType = request.SourceType,
                SourceReference = request.SourceReference ?? string.Empty,
                SourcePublishedDate = request.SourcePublishedDate,
                Severity = request.Severity,
                ActionType = request.ActionType,
                TargetDrugClassCodes = request.TargetDrugClassCodes,
                DefaultMessage = request.DefaultMessage ?? string.Empty,
                ClinicalSummary = request.ClinicalSummary ?? string.Empty,
                CreatedBy = request.CreatedBy ?? string.Empty,
            });
            return Created($"/api/drugsafetyadvisory/{request.AdvisoryId}", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving advisory {AdvisoryId}", request.AdvisoryId);
            return StatusCode(500, "An error occurred while saving the advisory.");
        }
    }

    /// <summary>Releases a Draft advisory for provider dispatch.</summary>
    [HttpPost("{advisoryId}/activate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Activate(string advisoryId)
    {
        try
        {
            await Advisory(advisoryId).ActivateAsync();
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating advisory {AdvisoryId}", advisoryId);
            return StatusCode(500, "An error occurred while activating the advisory.");
        }
    }

    /// <summary>Withdraws an advisory so it can no longer be dispatched.</summary>
    [HttpPost("{advisoryId}/retire")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Retire(string advisoryId)
    {
        try
        {
            await Advisory(advisoryId).RetireAsync();
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retiring advisory {AdvisoryId}", advisoryId);
            return StatusCode(500, "An error occurred while retiring the advisory.");
        }
    }

    // ─── Dispatch (provider) ────────────────────────────────────────────────────

    /// <summary>
    /// Dispatches an advisory to the provider's confirmed list of affected patients,
    /// using the provider's (optionally edited) message. Patients already reached are
    /// skipped. The UI supplies <c>PatientIds</c> — the affected-patient cohort it
    /// derived from the advisory's target classes and the provider's panel.
    /// </summary>
    [HttpPost("{advisoryId}/dispatch")]
    [ProducesResponseType(typeof(AdvisoryDispatchResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AdvisoryDispatchResult>> Dispatch(string advisoryId, [FromBody] DispatchAdvisoryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FinalMessage))
            return BadRequest("FinalMessage is required.");
        if (request.PatientIds is null || request.PatientIds.Count == 0)
            return BadRequest("At least one patient id is required.");
        if (string.IsNullOrWhiteSpace(request.ProviderId))
            return BadRequest("ProviderId is required.");

        try
        {
            AdvisoryDispatchResult result = await Advisory(advisoryId).DispatchAsync(
                request.FinalMessage, request.PatientIds,
                request.ProviderId, request.ProviderName ?? string.Empty, request.Channel);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error dispatching advisory {AdvisoryId}", advisoryId);
            return StatusCode(500, "An error occurred while dispatching the advisory.");
        }
    }

    // ─── Patient receipt history ────────────────────────────────────────────────

    /// <summary>Returns the advisory receipts on a patient's record (what they got).</summary>
    [HttpGet("/api/patient/{patientId}/safety-advisories")]
    [ProducesResponseType(typeof(List<PatientAdvisoryReceipt>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PatientAdvisoryReceipt>>> GetPatientReceipts(string patientId)
    {
        try
        {
            return Ok(await PatientLog(patientId).GetReceiptsAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving advisory receipts for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving advisory receipts.");
        }
    }
}

// ─── DTOs ───────────────────────────────────────────────────────────────────────

/// <summary>Request to create or update a drug safety advisory.</summary>
public record SaveAdvisoryRequest(
    string AdvisoryId,
    string Title,
    AdvisorySourceType SourceType,
    string? SourceReference,
    DateTime? SourcePublishedDate,
    AdvisorySeverity Severity,
    AdvisoryActionType ActionType,
    List<string> TargetDrugClassCodes,
    string? DefaultMessage,
    string? ClinicalSummary,
    string? CreatedBy);

/// <summary>Request to dispatch an advisory to a provider's confirmed patient cohort.</summary>
public record DispatchAdvisoryRequest(
    string FinalMessage,
    List<string> PatientIds,
    string ProviderId,
    string? ProviderName,
    AdvisoryChannel Channel);
