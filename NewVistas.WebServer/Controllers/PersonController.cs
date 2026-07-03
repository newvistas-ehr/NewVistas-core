// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Person-identity API (ADR-002) — the cross-role anchor for a HUMAN (patient-role, staff-role,
/// relative-appearances), viewer-gated access decisions, the patient's own share preference, access
/// audit reporting, and cascade-testing opportunities.
///
/// Patient-scoped operations route through <see cref="IPatientWorkflowGrain"/> keyed by <c>{patientId}</c>.
/// Person-scoped operations (under <c>persons/...</c>) address <see cref="IPersonGrain"/> /
/// <see cref="IPersonIndexGrain"/> directly.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class PersonController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<PersonController> _logger;

    public PersonController(IGrainFactory grainFactory, ILogger<PersonController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ─── Person-scoped reads (fixed "persons/..." prefix — declared first) ────────

    /// <summary>Returns a Person (the cross-role identity anchor) by its Person id.</summary>
    [HttpGet("persons/{personId}")]
    [ProducesResponseType(typeof(PersonState), StatusCodes.Status200OK)]
    public async Task<ActionResult<PersonState>> GetPerson(string personId)
    {
        try
        {
            PersonState person = await _grainFactory.GetGrain<IPersonGrain>(personId).GetAsync();
            return Ok(person);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving person {PersonId}", personId);
            return StatusCode(500, "An error occurred while retrieving the person");
        }
    }

    /// <summary>Case-insensitive last-name / "Last,First" prefix search over the Person directory.</summary>
    [HttpGet("persons/search")]
    [ProducesResponseType(typeof(List<PersonIndexEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PersonIndexEntry>>> SearchPersons([FromQuery] string q)
    {
        try
        {
            List<PersonIndexEntry> results = await _grainFactory
                .GetGrain<IPersonIndexGrain>("PERSON-INDEX:DEFAULT")
                .SearchByNameAsync(q ?? string.Empty);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching persons for query {Query}", q);
            return StatusCode(500, "An error occurred while searching persons");
        }
    }

    /// <summary>Persons flagged employee-patient (both a patient- and a staff-role) — sensitive.</summary>
    [HttpGet("persons/employee-patients")]
    [ProducesResponseType(typeof(List<PersonIndexEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PersonIndexEntry>>> GetEmployeePatients()
    {
        try
        {
            List<PersonIndexEntry> results = await _grainFactory
                .GetGrain<IPersonIndexGrain>("PERSON-INDEX:DEFAULT")
                .GetEmployeePatientsAsync();
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving employee-patients");
            return StatusCode(500, "An error occurred while retrieving employee-patients");
        }
    }

    // ─── Patient-scoped: viewer-gated access ─────────────────────────────────────

    /// <summary>Viewer-gated cross-role Person read — returns the Person only when access is granted (else null + reason).</summary>
    [HttpGet("{patientId}/view")]
    [ProducesResponseType(typeof(PersonViewResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<PersonViewResult>> ViewPerson(
        string patientId,
        [FromQuery] string viewerUserId,
        [FromQuery] string? viewerName,
        [FromQuery] bool breakTheGlass,
        [FromQuery] string? justification)
    {
        try
        {
            PersonViewResult result = await GetWorkflow(patientId).GetPatientPersonForViewerAsync(
                viewerUserId ?? string.Empty,
                viewerName ?? string.Empty,
                breakTheGlass,
                justification);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving person view for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving the person view");
        }
    }

    /// <summary>Decides + audits a viewer's access to this chart (treatment relationship never gated; BTG attest-and-proceed).</summary>
    [HttpPost("{patientId}/access")]
    [ProducesResponseType(typeof(PatientAccessDecision), StatusCodes.Status200OK)]
    public async Task<ActionResult<PatientAccessDecision>> AccessPatient(
        string patientId, [FromBody] AccessRequest request)
    {
        try
        {
            PatientAccessDecision decision = await GetWorkflow(patientId).AccessPatientAsync(
                request.ViewerUserId ?? string.Empty,
                request.ViewerName ?? string.Empty,
                request.BreakTheGlassAttested,
                request.Justification);
            return Ok(decision);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deciding patient access for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while deciding patient access");
        }
    }

    // ─── Patient-scoped: Person creation / linking ───────────────────────────────

    /// <summary>Bootstraps a Person from this patient's demographics and links the chart (idempotent). Returns the PersonId.</summary>
    [HttpPost("{patientId}/person")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateOrGetPerson(
        string patientId, [FromBody] CreatePersonRequest request)
    {
        try
        {
            string personId = await GetWorkflow(patientId).CreateOrGetPersonForPatientAsync(
                request.FacilityId ?? string.Empty,
                request.Confidence,
                request.ByUser ?? string.Empty);
            return Created($"/api/person/persons/{personId}", new { PersonId = personId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating/getting person for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while creating the person");
        }
    }

    /// <summary>Links an already-created Person to this chart (registrar-confirmed).</summary>
    [HttpPost("{patientId}/link-person")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LinkPatientToPerson(
        string patientId, [FromBody] LinkPersonRequest request)
    {
        try
        {
            await GetWorkflow(patientId).LinkPatientToPersonAsync(
                request.PersonId,
                request.FacilityId ?? string.Empty,
                request.Confidence,
                request.ByUser ?? string.Empty);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error linking patient {PatientId} to person {PersonId}",
                patientId, request.PersonId);
            return StatusCode(500, "An error occurred while linking the patient to the person");
        }
    }

    /// <summary>Links a family-history relative on this chart to a known Person (+ records the reverse appearance).</summary>
    [HttpPost("{patientId}/family-members/{memberId}/link-person")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LinkFamilyMemberToPerson(
        string patientId, string memberId, [FromBody] LinkFamilyMemberRequest request)
    {
        try
        {
            await GetWorkflow(patientId).LinkFamilyMemberToPersonAsync(
                memberId,
                request.PersonId,
                request.ByUser ?? string.Empty);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error linking family member {MemberId} on patient {PatientId} to person {PersonId}",
                memberId, patientId, request.PersonId);
            return StatusCode(500, "An error occurred while linking the family member to the person");
        }
    }

    // ─── Patient-scoped: share preference ────────────────────────────────────────

    /// <summary>Sets this patient's own sharing preference (maximal-openness is a first-class choice).</summary>
    [HttpPut("{patientId}/share-preference")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetSharePreference(
        string patientId, [FromBody] SharePreferenceRequest request)
    {
        try
        {
            await GetWorkflow(patientId).SetPatientSharePreferenceAsync(request.Preference);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting share preference for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while setting the share preference");
        }
    }

    // ─── Patient-scoped: access audit reporting ──────────────────────────────────

    /// <summary>This patient's "who viewed my chart" access report (accountability).</summary>
    [HttpGet("{patientId}/access-log")]
    [ProducesResponseType(typeof(List<PatientAccessLog>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PatientAccessLog>>> GetAccessLog(string patientId)
    {
        try
        {
            List<PatientAccessLog> log = await GetWorkflow(patientId).GetMyAccessLogAsync();
            return Ok(log);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving access log for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving the access log");
        }
    }

    /// <summary>Suspicious accesses to this chart — break-the-glass / blocked attempts (anomaly surface).</summary>
    [HttpGet("{patientId}/suspicious-access")]
    [ProducesResponseType(typeof(List<PatientAccessLog>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PatientAccessLog>>> GetSuspiciousAccess(string patientId)
    {
        try
        {
            List<PatientAccessLog> log = await GetWorkflow(patientId).GetSuspiciousAccessesAsync();
            return Ok(log);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving suspicious accesses for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving suspicious accesses");
        }
    }

    // ─── Patient-scoped: cascade-testing opportunities (Phase 5) ─────────────────

    /// <summary>Cascade-testing opportunities from relatives linked to a Person with a confirmed germline finding.</summary>
    [HttpGet("{patientId}/cascade-opportunities")]
    [ProducesResponseType(typeof(List<Abstractions.Clinical.CascadeOpportunity>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<Abstractions.Clinical.CascadeOpportunity>>> GetCascadeOpportunities(
        string patientId)
    {
        try
        {
            List<Abstractions.Clinical.CascadeOpportunity> opportunities =
                await GetWorkflow(patientId).GetCascadeOpportunitiesAsync();
            return Ok(opportunities);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cascade opportunities for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving cascade opportunities");
        }
    }
}

// ─── Request DTOs ─────────────────────────────────────────────────────────────

public record AccessRequest
{
    public string ViewerUserId { get; init; } = string.Empty;
    public string? ViewerName { get; init; }
    public bool BreakTheGlassAttested { get; init; }
    public string? Justification { get; init; }
}

public record CreatePersonRequest
{
    public string? FacilityId { get; init; }
    public PersonLinkConfidence Confidence { get; init; }
    public string? ByUser { get; init; }
}

public record LinkPersonRequest
{
    public string PersonId { get; init; } = string.Empty;
    public string? FacilityId { get; init; }
    public PersonLinkConfidence Confidence { get; init; }
    public string? ByUser { get; init; }
}

public record LinkFamilyMemberRequest
{
    public string PersonId { get; init; } = string.Empty;
    public string? ByUser { get; init; }
}

public record SharePreferenceRequest
{
    public PatientSharePreference Preference { get; init; }
}
