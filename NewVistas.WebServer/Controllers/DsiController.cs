// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Decision Support Interventions (DSI) Controller — §170.315(b)(11).
///
/// Implements:
///   - CDS intervention management (evidence-based and predictive/AI)
///   - Patient evaluation against active interventions
///   - Event logging (firing → clinician response)
///   - HTI-1 transparency for predictive DSI
///   - Audit dashboard (all events, filtering, pending responses)
/// </summary>
[ApiController]
[Route("api/dsi")]
[Authorize]
public class DsiController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<DsiController> _logger;

    public DsiController(IGrainFactory grainFactory, ILogger<DsiController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    // ─── Intervention Management ──────────────────────────────────────────────

    /// <summary>GET api/dsi/interventions — List all registered interventions.</summary>
    [HttpGet("interventions")]
    public async Task<IActionResult> GetAllInterventions()
    {
        try
        {
            IDsiInterventionIndexGrain index = _grainFactory.GetGrain<IDsiInterventionIndexGrain>("DSI-INDEX");
            List<DsiInterventionSummary> interventions = await index.GetAllInterventionsAsync();
            return Ok(interventions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing DSI interventions");
            return StatusCode(500, "An error occurred listing interventions.");
        }
    }

    /// <summary>GET api/dsi/interventions/active — List only active interventions.</summary>
    [HttpGet("interventions/active")]
    public async Task<IActionResult> GetActiveInterventions()
    {
        try
        {
            IDsiInterventionIndexGrain index = _grainFactory.GetGrain<IDsiInterventionIndexGrain>("DSI-INDEX");
            List<DsiInterventionSummary> interventions = await index.GetActiveInterventionsAsync();
            return Ok(interventions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing active DSI interventions");
            return StatusCode(500, "An error occurred listing active interventions.");
        }
    }

    /// <summary>GET api/dsi/interventions/{interventionId} — Get intervention details.</summary>
    [HttpGet("interventions/{interventionId}")]
    public async Task<IActionResult> GetIntervention(string interventionId)
    {
        try
        {
            IDsiInterventionGrain grain = _grainFactory.GetGrain<IDsiInterventionGrain>($"DSI:{interventionId}");
            DsiInterventionState intervention = await grain.GetInterventionAsync();
            if (string.IsNullOrEmpty(intervention.Title))
                return NotFound($"Intervention {interventionId} not found.");
            return Ok(intervention);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting intervention {InterventionId}", interventionId);
            return StatusCode(500, "An error occurred getting the intervention.");
        }
    }

    /// <summary>POST api/dsi/interventions — Create or update an intervention.</summary>
    [HttpPost("interventions")]
    public async Task<IActionResult> SaveIntervention([FromBody] DsiInterventionState intervention)
    {
        try
        {
            IDsiInterventionGrain grain = _grainFactory.GetGrain<IDsiInterventionGrain>($"DSI:{intervention.InterventionId}");
            await grain.SaveInterventionAsync(intervention);

            IDsiInterventionIndexGrain index = _grainFactory.GetGrain<IDsiInterventionIndexGrain>("DSI-INDEX");
            await index.AddInterventionAsync(new DsiInterventionSummary
            {
                InterventionId = intervention.InterventionId,
                Title = intervention.Title,
                InterventionType = intervention.InterventionType,
                ClinicalDomain = intervention.ClinicalDomain,
                IsActive = intervention.IsActive,
                Severity = intervention.Severity,
                Developer = intervention.Developer
            });

            return Created($"api/dsi/interventions/{intervention.InterventionId}", intervention);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving intervention {InterventionId}", intervention.InterventionId);
            return StatusCode(500, "An error occurred saving the intervention.");
        }
    }

    /// <summary>PUT api/dsi/interventions/{interventionId}/active — Activate or deactivate.</summary>
    [HttpPut("interventions/{interventionId}/active")]
    public async Task<IActionResult> SetInterventionActive(string interventionId, [FromBody] bool isActive)
    {
        try
        {
            IDsiInterventionGrain grain = _grainFactory.GetGrain<IDsiInterventionGrain>($"DSI:{interventionId}");
            await grain.SetActiveAsync(isActive);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating intervention {InterventionId}", interventionId);
            return StatusCode(500, "An error occurred updating the intervention.");
        }
    }

    /// <summary>DELETE api/dsi/interventions/{interventionId} — Remove from index.</summary>
    [HttpDelete("interventions/{interventionId}")]
    public async Task<IActionResult> RemoveIntervention(string interventionId)
    {
        try
        {
            IDsiInterventionIndexGrain index = _grainFactory.GetGrain<IDsiInterventionIndexGrain>("DSI-INDEX");
            await index.RemoveInterventionAsync(interventionId);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing intervention {InterventionId}", interventionId);
            return StatusCode(500, "An error occurred removing the intervention.");
        }
    }

    // ─── Patient Evaluation ───────────────────────────────────────────────────

    /// <summary>
    /// POST api/dsi/evaluate/{patientId} — Evaluate all active interventions against a patient.
    /// Returns triggered alerts with source attribution and HTI-1 transparency.
    /// </summary>
    [HttpPost("evaluate/{patientId}")]
    public async Task<IActionResult> EvaluatePatient(string patientId)
    {
        try
        {
            IDsiEvaluationGrain grain = _grainFactory.GetGrain<IDsiEvaluationGrain>($"DSI-EVAL:{patientId}");
            List<DsiEvaluationResult> results = await grain.EvaluatePatientAsync();
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating DSI for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred evaluating decision support.");
        }
    }

    /// <summary>
    /// POST api/dsi/evaluate/{patientId}/fire — Evaluate and auto-record firing events.
    /// Returns the list of event IDs for triggered interventions.
    /// </summary>
    [HttpPost("evaluate/{patientId}/fire")]
    public async Task<IActionResult> EvaluateAndFire(string patientId)
    {
        try
        {
            IDsiEvaluationGrain evalGrain = _grainFactory.GetGrain<IDsiEvaluationGrain>($"DSI-EVAL:{patientId}");
            List<DsiEvaluationResult> results = await evalGrain.EvaluatePatientAsync();

            List<string> eventIds = new();
            IDsiEventIndexGrain eventIndex = _grainFactory.GetGrain<IDsiEventIndexGrain>("DSI-EVENT-INDEX");

            foreach (DsiEvaluationResult result in results)
            {
                string eventId = $"DSI-EVENT:{Guid.NewGuid():N}";
                IDsiEventGrain eventGrain = _grainFactory.GetGrain<IDsiEventGrain>(eventId);
                await eventGrain.RecordFiringAsync(
                    result.InterventionId, result.Title, result.InterventionType,
                    patientId, User.Identity?.Name, result.RecommendedAction,
                    result.Severity, result.TriggerEvidence, result.SourceCitation);

                await eventIndex.AddEventAsync(new DsiEventSummary
                {
                    EventId = eventId,
                    InterventionId = result.InterventionId,
                    InterventionTitle = result.Title,
                    PatientId = patientId,
                    FiredDate = DateTime.UtcNow,
                    Severity = result.Severity,
                    UserResponse = "pending"
                });

                eventIds.Add(eventId);
            }

            return Created("api/dsi/events", new { alerts = results.Count, eventIds, results });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating and firing DSI for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred during evaluation and firing.");
        }
    }

    // ─── Event Management ─────────────────────────────────────────────────────

    /// <summary>GET api/dsi/events — List all DSI events (audit log).</summary>
    [HttpGet("events")]
    public async Task<IActionResult> GetAllEvents()
    {
        try
        {
            IDsiEventIndexGrain index = _grainFactory.GetGrain<IDsiEventIndexGrain>("DSI-EVENT-INDEX");
            List<DsiEventSummary> events = await index.GetAllEventsAsync();
            return Ok(events);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing DSI events");
            return StatusCode(500, "An error occurred listing events.");
        }
    }

    /// <summary>GET api/dsi/events/pending — List events awaiting clinician response.</summary>
    [HttpGet("events/pending")]
    public async Task<IActionResult> GetPendingEvents()
    {
        try
        {
            IDsiEventIndexGrain index = _grainFactory.GetGrain<IDsiEventIndexGrain>("DSI-EVENT-INDEX");
            List<DsiEventSummary> events = await index.GetPendingEventsAsync();
            return Ok(events);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing pending DSI events");
            return StatusCode(500, "An error occurred listing pending events.");
        }
    }

    /// <summary>GET api/dsi/events/patient/{patientId} — Events for a specific patient.</summary>
    [HttpGet("events/patient/{patientId}")]
    public async Task<IActionResult> GetEventsByPatient(string patientId)
    {
        try
        {
            IDsiEventIndexGrain index = _grainFactory.GetGrain<IDsiEventIndexGrain>("DSI-EVENT-INDEX");
            List<DsiEventSummary> events = await index.GetEventsByPatientAsync(patientId);
            return Ok(events);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing events for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred listing events.");
        }
    }

    /// <summary>GET api/dsi/events/intervention/{interventionId} — Events for an intervention.</summary>
    [HttpGet("events/intervention/{interventionId}")]
    public async Task<IActionResult> GetEventsByIntervention(string interventionId)
    {
        try
        {
            IDsiEventIndexGrain index = _grainFactory.GetGrain<IDsiEventIndexGrain>("DSI-EVENT-INDEX");
            List<DsiEventSummary> events = await index.GetEventsByInterventionAsync(interventionId);
            return Ok(events);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing events for intervention {InterventionId}", interventionId);
            return StatusCode(500, "An error occurred listing events.");
        }
    }

    /// <summary>GET api/dsi/events/{eventId} — Get full event details.</summary>
    [HttpGet("events/{eventId}")]
    public async Task<IActionResult> GetEvent(string eventId)
    {
        try
        {
            IDsiEventGrain grain = _grainFactory.GetGrain<IDsiEventGrain>(eventId);
            DsiEventState evt = await grain.GetEventAsync();
            if (string.IsNullOrEmpty(evt.InterventionId))
                return NotFound($"Event {eventId} not found.");
            return Ok(evt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting event {EventId}", eventId);
            return StatusCode(500, "An error occurred getting the event.");
        }
    }

    /// <summary>
    /// POST api/dsi/events/{eventId}/respond — Record clinician response to an alert.
    /// </summary>
    [HttpPost("events/{eventId}/respond")]
    public async Task<IActionResult> RecordResponse(string eventId, [FromBody] DsiResponseRequest request)
    {
        try
        {
            IDsiEventGrain grain = _grainFactory.GetGrain<IDsiEventGrain>(eventId);
            await grain.RecordResponseAsync(request.Response, request.OverrideReason);

            IDsiEventIndexGrain index = _grainFactory.GetGrain<IDsiEventIndexGrain>("DSI-EVENT-INDEX");
            await index.UpdateResponseAsync(eventId, request.Response);

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording response for event {EventId}", eventId);
            return StatusCode(500, "An error occurred recording the response.");
        }
    }
}

// ─── Request DTOs ────────────────────────────────────────────────────────────

/// <summary>Clinician response to a DSI alert.</summary>
public record DsiResponseRequest(
    string Response,
    string? OverrideReason);
