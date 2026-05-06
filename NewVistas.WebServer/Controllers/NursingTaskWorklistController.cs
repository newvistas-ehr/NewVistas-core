// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// REST API for Nursing Task Worklist — unified view of all due nursing tasks.
/// </summary>
[Authorize]
[ApiController]
[Route("api/nursing/tasks")]
[Produces("application/json")]
public class NursingTaskWorklistController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<NursingTaskWorklistController> _logger;

    public NursingTaskWorklistController(IGrainFactory grainFactory, ILogger<NursingTaskWorklistController> logger)
    { _grainFactory = grainFactory; _logger = logger; }

    private IPatientWorkflowGrain W(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    [HttpPost("{patientId}/refresh")]
    public async Task<IActionResult> Refresh(string patientId)
    {
        try { return Ok(await W(patientId).RefreshNursingTaskWorklistAsync()); }
        catch (Exception ex) { _logger.LogError(ex, "Error refreshing worklist for {PatientId}", patientId); return StatusCode(500, "Failed to refresh worklist."); }
    }

    [HttpGet("{patientId}")]
    public async Task<IActionResult> GetWorklist(string patientId)
    {
        try { return Ok(await W(patientId).GetNursingTaskWorklistAsync()); }
        catch (Exception ex) { _logger.LogError(ex, "Error getting worklist"); return StatusCode(500, "Failed to get worklist."); }
    }

    [HttpGet("{patientId}/due")]
    public async Task<IActionResult> GetDueTasks(string patientId)
    {
        try { return Ok(await W(patientId).GetDueNursingTasksAsync()); }
        catch (Exception ex) { _logger.LogError(ex, "Error getting due tasks"); return StatusCode(500, "Failed to get due tasks."); }
    }

    [HttpPost("{patientId}/tasks/{taskId}/complete")]
    public async Task<IActionResult> CompleteTask(string patientId, string taskId, [FromBody] CompleteTaskRequest req)
    {
        try { await W(patientId).CompleteNursingTaskAsync(taskId, req.NurseId, req.NurseName, req.Notes); return Ok(); }
        catch (Exception ex) { _logger.LogError(ex, "Error completing task"); return StatusCode(500, "Failed to complete task."); }
    }

    [HttpPost("{patientId}/tasks/{taskId}/defer")]
    public async Task<IActionResult> DeferTask(string patientId, string taskId, [FromBody] DeferTaskRequest? req)
    {
        try { await W(patientId).DeferNursingTaskAsync(taskId, req?.Reason); return Ok(); }
        catch (Exception ex) { _logger.LogError(ex, "Error deferring task"); return StatusCode(500, "Failed to defer task."); }
    }

    [HttpPost("{patientId}/tasks")]
    public async Task<IActionResult> AddTask(string patientId, [FromBody] AddTaskRequest req)
    {
        try { await W(patientId).AddNursingTaskAsync(req.Category, req.Priority, req.Description, req.DueDateTime, req.SourceId, req.SourceType); return Created("", null); }
        catch (Exception ex) { _logger.LogError(ex, "Error adding task"); return StatusCode(500, "Failed to add task."); }
    }

    public record CompleteTaskRequest { public string NurseId { get; init; } = string.Empty; public string NurseName { get; init; } = string.Empty; public string? Notes { get; init; } }
    public record DeferTaskRequest { public string? Reason { get; init; } }
    public record AddTaskRequest { public NursingTaskCategory Category { get; init; } public NursingTaskPriority Priority { get; init; } public string Description { get; init; } = string.Empty; public DateTime DueDateTime { get; init; } public string? SourceId { get; init; } public string? SourceType { get; init; } }
}
