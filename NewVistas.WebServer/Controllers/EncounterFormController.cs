// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EncounterFormController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<EncounterFormController> _logger;

    public EncounterFormController(IGrainFactory grainFactory, ILogger<EncounterFormController> logger)
    { _grainFactory = grainFactory; _logger = logger; }

    private IPatientWorkflowGrain GetWorkflow(string patientId) => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    [HttpGet("feature-status")]
    public async Task<IActionResult> GetFeatureStatus()
    {
        try
        {
            var siteParams = _grainFactory.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");
            bool enabled = await siteParams.IsFeatureEnabledAsync("ENCOUNTER_FORM_TEMPLATES");
            return Ok(new { Feature = "ENCOUNTER_FORM_TEMPLATES", Enabled = enabled });
        }
        catch (Exception ex) { _logger.LogError(ex, "Error checking feature"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    // ── Templates (system-level, not patient-specific) ──────────

    [HttpPost("templates")]
    public async Task<IActionResult> CreateTemplate([FromBody] CreateEncounterFormTemplateRequest req)
    {
        try
        {
            string templateId = $"EF-TPL:{Guid.NewGuid()}";
            var grain = _grainFactory.GetGrain<IEncounterFormTemplateGrain>(templateId);
            var result = await grain.CreateTemplateAsync(req.Name, req.Description, req.FormType, req.ClinicId, req.Fields, req.CreatedByName);
            return Created($"api/encounterform/templates/{templateId}", result);
        }
        catch (Exception ex) { _logger.LogError(ex, "Error creating template"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpGet("templates/{templateId}")]
    public async Task<IActionResult> GetTemplate(string templateId)
    {
        try { return Ok(await _grainFactory.GetGrain<IEncounterFormTemplateGrain>(templateId).GetTemplateAsync()); }
        catch (Exception ex) { _logger.LogError(ex, "Error getting template"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPut("templates/{templateId}")]
    public async Task<IActionResult> UpdateTemplate(string templateId, [FromBody] UpdateEncounterFormTemplateRequest req)
    {
        try
        {
            await _grainFactory.GetGrain<IEncounterFormTemplateGrain>(templateId).UpdateTemplateAsync(req.Name, req.Description, req.Fields, req.UpdatedByName);
            return Ok(new { Message = "Template updated." });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error updating template"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPost("templates/{templateId}/publish")]
    public async Task<IActionResult> PublishTemplate(string templateId, [FromBody] EncounterFormActionRequest req)
    {
        try
        {
            await _grainFactory.GetGrain<IEncounterFormTemplateGrain>(templateId).PublishAsync(req.PerformedByName);
            return Ok(new { Message = "Template published." });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error publishing template"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPost("templates/{templateId}/retire")]
    public async Task<IActionResult> RetireTemplate(string templateId, [FromBody] EncounterFormActionRequest req)
    {
        try
        {
            await _grainFactory.GetGrain<IEncounterFormTemplateGrain>(templateId).RetireAsync(req.PerformedByName);
            return Ok(new { Message = "Template retired." });
        }
        catch (Exception ex) { _logger.LogError(ex, "Error retiring template"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpGet("templates")]
    public async Task<IActionResult> GetTemplates([FromQuery] string? formType, [FromQuery] string? status, [FromQuery] string? clinicId, [FromQuery] int maxResults = 50)
    {
        try
        {
            var index = _grainFactory.GetGrain<IEncounterFormTemplateIndexGrain>("EF-TPL-IDX");
            return Ok(await index.SearchAsync(formType, status, clinicId, maxResults));
        }
        catch (Exception ex) { _logger.LogError(ex, "Error listing templates"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    // ── Instances (patient-specific) ────────────────────────────

    [HttpPost("{patientId}/instances")]
    public async Task<IActionResult> CreateInstance(string patientId, [FromBody] CreateEncounterFormInstanceRequest req)
    {
        try
        {
            var result = await GetWorkflow(patientId).CreateEncounterFormInstanceAsync(
                req.TemplateId, req.TemplateName, req.EncounterId, req.CreatedByProviderId, req.CreatedByProviderName);
            return Created($"api/encounterform/{patientId}/instances/{result.InstanceId}", result);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error creating instance"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpGet("{patientId}/instances")]
    public async Task<IActionResult> GetInstances(string patientId)
    {
        try { return Ok(await GetWorkflow(patientId).GetEncounterFormInstancesAsync()); }
        catch (Exception ex) { _logger.LogError(ex, "Error getting instances"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpGet("{patientId}/instances/{instanceId}")]
    public async Task<IActionResult> GetInstance(string patientId, string instanceId)
    {
        try { return Ok(await GetWorkflow(patientId).GetEncounterFormInstanceAsync(instanceId)); }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error getting instance"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPost("{patientId}/instances/{instanceId}/values")]
    public async Task<IActionResult> SetValues(string patientId, string instanceId, [FromBody] SetEncounterFormValuesRequest req)
    {
        try
        {
            await GetWorkflow(patientId).SetEncounterFormFieldValuesAsync(instanceId, req.FieldValues);
            return Ok(new { Message = "Values saved." });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error setting values"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPost("{patientId}/instances/{instanceId}/submit")]
    public async Task<IActionResult> Submit(string patientId, string instanceId, [FromBody] EncounterFormActionRequest req)
    {
        try
        {
            await GetWorkflow(patientId).SubmitEncounterFormAsync(instanceId, req.PerformedByName);
            return Ok(new { Message = "Form submitted." });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error submitting"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpPost("{patientId}/instances/{instanceId}/void")]
    public async Task<IActionResult> VoidInstance(string patientId, string instanceId, [FromBody] VoidEncounterFormRequest req)
    {
        try
        {
            await GetWorkflow(patientId).VoidEncounterFormAsync(instanceId, req.VoidedByName, req.Reason);
            return Ok(new { Message = "Form voided." });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error voiding"); return StatusCode(500, new { Error = "An error occurred." }); }
    }

    [HttpGet("instances/dashboard")]
    public async Task<IActionResult> GetInstanceDashboard([FromQuery] string? patientId, [FromQuery] string? templateId, [FromQuery] string? status, [FromQuery] int maxResults = 50)
    {
        try
        {
            var index = _grainFactory.GetGrain<IEncounterFormInstanceIndexGrain>("EF-INST-IDX");
            return Ok(await index.SearchAsync(patientId, templateId, status, maxResults));
        }
        catch (Exception ex) { _logger.LogError(ex, "Error searching instances"); return StatusCode(500, new { Error = "An error occurred." }); }
    }
}

public record CreateEncounterFormTemplateRequest(string Name, string Description, string FormType, string? ClinicId, List<EncounterFormFieldDefinition> Fields, string CreatedByName);
public record UpdateEncounterFormTemplateRequest(string Name, string Description, List<EncounterFormFieldDefinition> Fields, string UpdatedByName);
public record CreateEncounterFormInstanceRequest(string TemplateId, string TemplateName, string? EncounterId, string CreatedByProviderId, string CreatedByProviderName);
public record SetEncounterFormValuesRequest(Dictionary<string, string?> FieldValues);
public record EncounterFormActionRequest(string PerformedByName);
public record VoidEncounterFormRequest(string VoidedByName, string Reason);
