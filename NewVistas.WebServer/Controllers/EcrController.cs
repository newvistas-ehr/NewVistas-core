// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Electronic Case Reporting (eCR) Controller — §170.315(f)(5).
///
/// Implements:
///   - Reportable condition trigger management (RCTC value sets)
///   - Patient screening for reportable conditions
///   - eICR document generation and lifecycle management
///   - Case reporting dashboard (status tracking, filtering)
///
/// Based on HL7 eICR IG and FHIR eCR IG.
/// </summary>
[ApiController]
[Route("api/ecr")]
[Authorize]
public class EcrController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<EcrController> _logger;

    public EcrController(IGrainFactory grainFactory, ILogger<EcrController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    // ─── Trigger Management ──────────────────────────────────────────────────

    /// <summary>GET api/ecr/triggers — List all reportable condition triggers.</summary>
    [HttpGet("triggers")]
    public async Task<IActionResult> GetAllTriggers()
    {
        try
        {
            IEcrTriggerIndexGrain index = _grainFactory.GetGrain<IEcrTriggerIndexGrain>("ECR-TRIGGER-INDEX");
            List<EcrTriggerSummary> triggers = await index.GetAllTriggersAsync();
            return Ok(triggers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing eCR triggers");
            return StatusCode(500, "An error occurred listing reportable condition triggers.");
        }
    }

    /// <summary>GET api/ecr/triggers/active — List only active triggers.</summary>
    [HttpGet("triggers/active")]
    public async Task<IActionResult> GetActiveTriggers()
    {
        try
        {
            IEcrTriggerIndexGrain index = _grainFactory.GetGrain<IEcrTriggerIndexGrain>("ECR-TRIGGER-INDEX");
            List<EcrTriggerSummary> triggers = await index.GetActiveTriggersAsync();
            return Ok(triggers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing active eCR triggers");
            return StatusCode(500, "An error occurred listing active triggers.");
        }
    }

    /// <summary>GET api/ecr/triggers/{triggerId} — Get a trigger definition.</summary>
    [HttpGet("triggers/{triggerId}")]
    public async Task<IActionResult> GetTrigger(string triggerId)
    {
        try
        {
            IEcrTriggerGrain grain = _grainFactory.GetGrain<IEcrTriggerGrain>($"ECR-TRIGGER:{triggerId}");
            EcrTriggerState trigger = await grain.GetTriggerAsync();
            if (string.IsNullOrEmpty(trigger.ConditionName))
                return NotFound($"Trigger {triggerId} not found.");
            return Ok(trigger);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting trigger {TriggerId}", triggerId);
            return StatusCode(500, "An error occurred getting the trigger.");
        }
    }

    /// <summary>POST api/ecr/triggers — Create or update a reportable condition trigger.</summary>
    [HttpPost("triggers")]
    public async Task<IActionResult> SaveTrigger([FromBody] EcrTriggerState trigger)
    {
        try
        {
            IEcrTriggerGrain grain = _grainFactory.GetGrain<IEcrTriggerGrain>($"ECR-TRIGGER:{trigger.TriggerId}");
            await grain.SaveTriggerAsync(trigger);

            IEcrTriggerIndexGrain index = _grainFactory.GetGrain<IEcrTriggerIndexGrain>("ECR-TRIGGER-INDEX");
            await index.AddTriggerAsync(new EcrTriggerSummary
            {
                TriggerId = trigger.TriggerId,
                ConditionName = trigger.ConditionName,
                Category = trigger.Category,
                IsActive = trigger.IsActive,
                ReportingTimeframe = trigger.ReportingTimeframe,
                TriggerCodeCount = trigger.TriggerCodes.Count
            });

            return Created($"api/ecr/triggers/{trigger.TriggerId}", trigger);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving trigger {TriggerId}", trigger.TriggerId);
            return StatusCode(500, "An error occurred saving the trigger.");
        }
    }

    /// <summary>PUT api/ecr/triggers/{triggerId}/active — Activate or deactivate a trigger.</summary>
    [HttpPut("triggers/{triggerId}/active")]
    public async Task<IActionResult> SetTriggerActive(string triggerId, [FromBody] bool isActive)
    {
        try
        {
            IEcrTriggerGrain grain = _grainFactory.GetGrain<IEcrTriggerGrain>($"ECR-TRIGGER:{triggerId}");
            await grain.SetActiveAsync(isActive);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating trigger {TriggerId}", triggerId);
            return StatusCode(500, "An error occurred updating the trigger.");
        }
    }

    /// <summary>DELETE api/ecr/triggers/{triggerId} — Remove a trigger from the index.</summary>
    [HttpDelete("triggers/{triggerId}")]
    public async Task<IActionResult> RemoveTrigger(string triggerId)
    {
        try
        {
            IEcrTriggerIndexGrain index = _grainFactory.GetGrain<IEcrTriggerIndexGrain>("ECR-TRIGGER-INDEX");
            await index.RemoveTriggerAsync(triggerId);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing trigger {TriggerId}", triggerId);
            return StatusCode(500, "An error occurred removing the trigger.");
        }
    }

    // ─── Patient Screening ───────────────────────────────────────────────────

    /// <summary>
    /// POST api/ecr/screen/{patientId} — Screen a patient for reportable conditions.
    /// Evaluates the patient's problems and labs against all active triggers.
    /// </summary>
    [HttpPost("screen/{patientId}")]
    public async Task<IActionResult> ScreenPatient(string patientId)
    {
        try
        {
            IEcrScreeningGrain grain = _grainFactory.GetGrain<IEcrScreeningGrain>($"ECR-SCREEN:{patientId}");
            List<EcrScreeningMatch> matches = await grain.ScreenPatientAsync();
            return Ok(matches);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error screening patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred screening the patient.");
        }
    }

    /// <summary>
    /// POST api/ecr/screen/{patientId}/report — Screen and auto-create case reports for matches.
    /// Returns the list of created case IDs.
    /// </summary>
    [HttpPost("screen/{patientId}/report")]
    public async Task<IActionResult> ScreenAndReport(string patientId)
    {
        try
        {
            IEcrScreeningGrain screenGrain = _grainFactory.GetGrain<IEcrScreeningGrain>($"ECR-SCREEN:{patientId}");
            List<EcrScreeningMatch> matches = await screenGrain.ScreenPatientAsync();

            List<string> caseIds = new();
            IEcrCaseIndexGrain caseIndex = _grainFactory.GetGrain<IEcrCaseIndexGrain>("ECR-CASE-INDEX");

            foreach (EcrScreeningMatch match in matches)
            {
                string caseId = $"ECR-CASE:{Guid.NewGuid():N}";
                IEcrCaseGrain caseGrain = _grainFactory.GetGrain<IEcrCaseGrain>(caseId);
                await caseGrain.CreateCaseAsync(
                    patientId, match.TriggerId, match.ConditionName,
                    match.MatchedCode, match.MatchedCodeSystem, match.MatchedDescription,
                    match.Jurisdictions, match.ClinicalEvidence,
                    User.Identity?.Name, null);

                // Auto-generate the eICR
                await caseGrain.GenerateEicrAsync();

                EcrCaseState caseState = await caseGrain.GetCaseAsync();
                await caseIndex.AddCaseAsync(new EcrCaseSummary
                {
                    CaseId = caseId,
                    PatientId = patientId,
                    PatientName = caseState.PatientName,
                    ConditionName = match.ConditionName,
                    Status = caseState.Status,
                    TriggeredDate = caseState.TriggeredDate
                });

                caseIds.Add(caseId);
            }

            return Created("api/ecr/cases", new { matches = matches.Count, caseIds });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error screening and reporting for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred during screening and reporting.");
        }
    }

    // ─── Case Management ─────────────────────────────────────────────────────

    /// <summary>GET api/ecr/cases — List all case reports.</summary>
    [HttpGet("cases")]
    public async Task<IActionResult> GetAllCases()
    {
        try
        {
            IEcrCaseIndexGrain index = _grainFactory.GetGrain<IEcrCaseIndexGrain>("ECR-CASE-INDEX");
            List<EcrCaseSummary> cases = await index.GetAllCasesAsync();
            return Ok(cases);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing eCR cases");
            return StatusCode(500, "An error occurred listing case reports.");
        }
    }

    /// <summary>GET api/ecr/cases/status/{status} — Filter cases by status.</summary>
    [HttpGet("cases/status/{status}")]
    public async Task<IActionResult> GetCasesByStatus(string status)
    {
        try
        {
            IEcrCaseIndexGrain index = _grainFactory.GetGrain<IEcrCaseIndexGrain>("ECR-CASE-INDEX");
            List<EcrCaseSummary> cases = await index.GetCasesByStatusAsync(status);
            return Ok(cases);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing cases by status {Status}", status);
            return StatusCode(500, "An error occurred listing case reports.");
        }
    }

    /// <summary>GET api/ecr/cases/patient/{patientId} — Cases for a specific patient.</summary>
    [HttpGet("cases/patient/{patientId}")]
    public async Task<IActionResult> GetCasesByPatient(string patientId)
    {
        try
        {
            IEcrCaseIndexGrain index = _grainFactory.GetGrain<IEcrCaseIndexGrain>("ECR-CASE-INDEX");
            List<EcrCaseSummary> cases = await index.GetCasesByPatientAsync(patientId);
            return Ok(cases);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing cases for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred listing case reports.");
        }
    }

    /// <summary>GET api/ecr/cases/{caseId} — Get full case details.</summary>
    [HttpGet("cases/{caseId}")]
    public async Task<IActionResult> GetCase(string caseId)
    {
        try
        {
            IEcrCaseGrain grain = _grainFactory.GetGrain<IEcrCaseGrain>(caseId);
            EcrCaseState ecrCase = await grain.GetCaseAsync();
            if (string.IsNullOrEmpty(ecrCase.PatientId))
                return NotFound($"Case {caseId} not found.");
            return Ok(ecrCase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting case {CaseId}", caseId);
            return StatusCode(500, "An error occurred getting the case.");
        }
    }

    /// <summary>POST api/ecr/cases/{caseId}/submit — Mark a case as submitted to public health.</summary>
    [HttpPost("cases/{caseId}/submit")]
    public async Task<IActionResult> SubmitCase(string caseId)
    {
        try
        {
            IEcrCaseGrain grain = _grainFactory.GetGrain<IEcrCaseGrain>(caseId);
            await grain.MarkSubmittedAsync();

            IEcrCaseIndexGrain index = _grainFactory.GetGrain<IEcrCaseIndexGrain>("ECR-CASE-INDEX");
            await index.UpdateCaseStatusAsync(caseId, "submitted", null);

            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting case {CaseId}", caseId);
            return StatusCode(500, "An error occurred submitting the case.");
        }
    }

    /// <summary>
    /// POST api/ecr/cases/{caseId}/response — Record a Reportability Response from public health.
    /// </summary>
    [HttpPost("cases/{caseId}/response")]
    public async Task<IActionResult> RecordResponse(string caseId, [FromBody] EcrReportabilityResponseRequest request)
    {
        try
        {
            IEcrCaseGrain grain = _grainFactory.GetGrain<IEcrCaseGrain>(caseId);
            await grain.RecordReportabilityResponseAsync(request.Determination, request.ResponseContent);

            IEcrCaseIndexGrain index = _grainFactory.GetGrain<IEcrCaseIndexGrain>("ECR-CASE-INDEX");
            await index.UpdateCaseStatusAsync(caseId, request.Determination, request.Determination);

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording response for case {CaseId}", caseId);
            return StatusCode(500, "An error occurred recording the response.");
        }
    }

    /// <summary>GET api/ecr/cases/{caseId}/eicr — Get the generated eICR document.</summary>
    [HttpGet("cases/{caseId}/eicr")]
    [Produces("application/xml")]
    public async Task<IActionResult> GetEicr(string caseId)
    {
        try
        {
            IEcrCaseGrain grain = _grainFactory.GetGrain<IEcrCaseGrain>(caseId);
            EcrCaseState ecrCase = await grain.GetCaseAsync();
            if (string.IsNullOrEmpty(ecrCase.EicrDocument))
                return NotFound("eICR document not yet generated.");
            return Content(ecrCase.EicrDocument, "application/xml");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting eICR for case {CaseId}", caseId);
            return StatusCode(500, "An error occurred getting the eICR document.");
        }
    }

    // ── Lab Surveillance Taxonomies (RPMS File #9999999.05) ──────────────────

    private ILabSurveillanceTaxonomyGrain GetTaxonomyGrain(string taxonomyId)
        => _grainFactory.GetGrain<ILabSurveillanceTaxonomyGrain>(taxonomyId);

    private ILabSurveillanceTaxonomyIndexGrain GetTaxonomyIndex()
        => _grainFactory.GetGrain<ILabSurveillanceTaxonomyIndexGrain>("LAB-SURV-TAX-IDX");

    /// <summary>GET api/ecr/taxonomies — List all lab surveillance taxonomies.</summary>
    [HttpGet("taxonomies")]
    public async Task<ActionResult<List<LabSurveillanceTaxonomyIndexEntry>>> GetTaxonomies()
    {
        try { return Ok(await GetTaxonomyIndex().GetAllAsync()); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving lab surveillance taxonomies");
            return StatusCode(500, "An error occurred retrieving taxonomies.");
        }
    }

    /// <summary>GET api/ecr/taxonomies/active — List active taxonomies only.</summary>
    [HttpGet("taxonomies/active")]
    public async Task<ActionResult<List<LabSurveillanceTaxonomyIndexEntry>>> GetActiveTaxonomies()
    {
        try { return Ok(await GetTaxonomyIndex().GetActiveAsync()); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active taxonomies");
            return StatusCode(500, "An error occurred retrieving active taxonomies.");
        }
    }

    /// <summary>GET api/ecr/taxonomies/{taxonomyId} — Get full taxonomy detail.</summary>
    [HttpGet("taxonomies/{taxonomyId}")]
    public async Task<ActionResult<LabSurveillanceTaxonomyState>> GetTaxonomy(string taxonomyId)
    {
        try
        {
            LabSurveillanceTaxonomyState state = await GetTaxonomyGrain(taxonomyId).GetAsync();
            if (string.IsNullOrEmpty(state.TaxonomyName))
                return NotFound(new { Message = $"Taxonomy '{taxonomyId}' not found" });
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving taxonomy {TaxonomyId}", taxonomyId);
            return StatusCode(500, "An error occurred retrieving the taxonomy.");
        }
    }

    /// <summary>POST api/ecr/taxonomies — Create or update a lab surveillance taxonomy.</summary>
    [HttpPost("taxonomies")]
    public async Task<IActionResult> SaveTaxonomy([FromBody] SaveLabSurveillanceTaxonomyRequest req)
    {
        try
        {
            string taxonomyId = req.TaxonomyId ?? $"LAB-SURV-TAX:{Guid.NewGuid()}";
            await GetTaxonomyGrain(taxonomyId).SaveAsync(
                req.TaxonomyName, req.ConditionName, req.ConditionCode,
                req.Category, req.Jurisdictions, req.ReportingTimeframe, req.IsActive);

            await GetTaxonomyIndex().UpsertAsync(new LabSurveillanceTaxonomyIndexEntry
            {
                TaxonomyId    = taxonomyId,
                TaxonomyName  = req.TaxonomyName,
                ConditionName = req.ConditionName,
                Category      = req.Category,
                CodeCount     = 0,
                IsActive      = req.IsActive,
            });

            return Created($"api/ecr/taxonomies/{taxonomyId}",
                new { Id = taxonomyId, Message = "Taxonomy saved" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving taxonomy");
            return StatusCode(500, "An error occurred saving the taxonomy.");
        }
    }

    /// <summary>POST api/ecr/taxonomies/{taxonomyId}/codes — Add a code to a taxonomy.</summary>
    [HttpPost("taxonomies/{taxonomyId}/codes")]
    public async Task<IActionResult> AddTaxonomyCode(string taxonomyId,
        [FromBody] AddLabSurveillanceCodeRequest req)
    {
        try
        {
            LabSurveillanceTaxonomyCode code = new()
            {
                Code                 = req.Code,
                CodeSystem           = req.CodeSystem,
                Description          = req.Description,
                SpecimenType         = req.SpecimenType,
                ValueOperator        = req.ValueOperator,
                ThresholdValue       = req.ThresholdValue,
                ResultInterpretation = req.ResultInterpretation,
            };
            await GetTaxonomyGrain(taxonomyId).AddCodeAsync(code);
            return Ok(new { TaxonomyId = taxonomyId, Message = "Code added" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding code to taxonomy {TaxonomyId}", taxonomyId);
            return StatusCode(500, "An error occurred adding the code.");
        }
    }

    /// <summary>POST api/ecr/taxonomies/{taxonomyId}/codes/remove — Remove a code from a taxonomy.</summary>
    [HttpPost("taxonomies/{taxonomyId}/codes/remove")]
    public async Task<IActionResult> RemoveTaxonomyCode(string taxonomyId,
        [FromBody] RemoveLabSurveillanceCodeRequest req)
    {
        try
        {
            await GetTaxonomyGrain(taxonomyId).RemoveCodeAsync(req.Code, req.CodeSystem);
            return Ok(new { TaxonomyId = taxonomyId, Message = "Code removed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing code from taxonomy {TaxonomyId}", taxonomyId);
            return StatusCode(500, "An error occurred removing the code.");
        }
    }

    /// <summary>PUT api/ecr/taxonomies/{taxonomyId}/active — Activate or deactivate a taxonomy.</summary>
    [HttpPut("taxonomies/{taxonomyId}/active")]
    public async Task<IActionResult> SetTaxonomyActive(string taxonomyId, [FromBody] bool isActive)
    {
        try
        {
            await GetTaxonomyGrain(taxonomyId).SetActiveAsync(isActive);
            return Ok(new { TaxonomyId = taxonomyId, IsActive = isActive });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting taxonomy {TaxonomyId} active={IsActive}", taxonomyId, isActive);
            return StatusCode(500, "An error occurred updating the taxonomy.");
        }
    }
}

// ─── Request DTOs ────────────────────────────────────────────────────────────

/// <summary>Reportability Response from public health decision support.</summary>
public record EcrReportabilityResponseRequest(
    string Determination,
    string ResponseContent);

public record SaveLabSurveillanceTaxonomyRequest(
    string? TaxonomyId,
    string TaxonomyName,
    string ConditionName,
    string? ConditionCode,
    string Category,
    List<string>? Jurisdictions,
    string ReportingTimeframe,
    bool IsActive);

public record AddLabSurveillanceCodeRequest(
    string Code,
    string CodeSystem,
    string Description,
    string? SpecimenType,
    string? ValueOperator,
    string? ThresholdValue,
    string? ResultInterpretation);

public record RemoveLabSurveillanceCodeRequest(
    string Code,
    string CodeSystem);
