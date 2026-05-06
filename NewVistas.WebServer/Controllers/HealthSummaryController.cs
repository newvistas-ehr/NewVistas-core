// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Health Summary API — configurable patient health summary reports.
/// VistA HEALTH SUMMARY TYPE file (#142).
/// MUMPS routines: GMTS.m (BUILD/PRINT), GMTSS.m (SETUP), GMTSUP.m (UTILITY).
/// </summary>
[Authorize]
[ApiController]
[Produces("application/json")]
public class HealthSummaryController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<HealthSummaryController> _logger;

    public HealthSummaryController(IGrainFactory grainFactory, ILogger<HealthSummaryController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private IHealthSummaryTypeGrain GetTypeGrain(string typeId)
        => _grainFactory.GetGrain<IHealthSummaryTypeGrain>($"HS-TYPE:{typeId}");

    private IHealthSummaryTypeIndexGrain GetTypeIndex()
        => _grainFactory.GetGrain<IHealthSummaryTypeIndexGrain>("HS-TYPE-IDX");

    // ─── Summary Type Management ───────────────────────────────────────────────

    /// <summary>List all health summary type templates.</summary>
    [HttpGet("api/healthsummary/types")]
    [ProducesResponseType(typeof(List<HealthSummaryTypeIndexEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<HealthSummaryTypeIndexEntry>>> GetTypes(
        [FromQuery] bool activeOnly = false)
    {
        try
        {
            List<HealthSummaryTypeIndexEntry> entries = activeOnly
                ? await GetTypeIndex().GetActiveAsync()
                : await GetTypeIndex().GetAllAsync();
            return Ok(entries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving health summary types");
            return StatusCode(500, "An error occurred while retrieving health summary types");
        }
    }

    /// <summary>Get a specific health summary type template by ID.</summary>
    [HttpGet("api/healthsummary/types/{typeId}")]
    [ProducesResponseType(typeof(HealthSummaryTypeState), StatusCodes.Status200OK)]
    public async Task<ActionResult<HealthSummaryTypeState>> GetType(string typeId)
    {
        try
        {
            HealthSummaryTypeState state = await GetTypeGrain(typeId).GetAsync();
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving health summary type {TypeId}", typeId);
            return StatusCode(500, "An error occurred while retrieving the health summary type");
        }
    }

    /// <summary>Create a new health summary type template.</summary>
    [HttpPost("api/healthsummary/types")]
    [ProducesResponseType(typeof(CreateHealthSummaryTypeResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateType([FromBody] CreateHealthSummaryTypeRequest request)
    {
        try
        {
            string typeId = Guid.NewGuid().ToString();
            await GetTypeGrain(typeId).CreateAsync(
                typeId,
                request.Name,
                request.Description,
                request.CreatedById,
                request.CreatedByName);

            await GetTypeIndex().UpsertEntryAsync(new HealthSummaryTypeIndexEntry
            {
                TypeId = typeId,
                Name = request.Name,
                Description = request.Description,
                Status = HealthSummaryTypeStatus.Active,
                ComponentCount = 0,
                CreatedDate = DateTime.UtcNow
            });

            _logger.LogInformation(
                "Created health summary type {TypeId} '{Name}' by {Creator}",
                typeId, request.Name, request.CreatedByName);

            return Created($"api/healthsummary/types/{typeId}", new CreateHealthSummaryTypeResponse(typeId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating health summary type '{Name}'", request.Name);
            return StatusCode(500, "An error occurred while creating the health summary type");
        }
    }

    /// <summary>Update the name and description of a health summary type template.</summary>
    [HttpPut("api/healthsummary/types/{typeId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateType(string typeId, [FromBody] UpdateHealthSummaryTypeRequest request)
    {
        try
        {
            await GetTypeGrain(typeId).UpdateAsync(request.Name, request.Description);

            // Refresh index entry
            HealthSummaryTypeState state = await GetTypeGrain(typeId).GetAsync();
            await GetTypeIndex().UpsertEntryAsync(new HealthSummaryTypeIndexEntry
            {
                TypeId = typeId,
                Name = state.Name,
                Description = state.Description,
                Status = state.Status,
                ComponentCount = state.Components.Count,
                CreatedDate = state.CreatedDate
            });

            _logger.LogInformation("Updated health summary type {TypeId}", typeId);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating health summary type {TypeId}", typeId);
            return StatusCode(500, "An error occurred while updating the health summary type");
        }
    }

    /// <summary>Add or update a component in a health summary type template.</summary>
    [HttpPost("api/healthsummary/types/{typeId}/components")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> AddOrUpdateComponent(
        string typeId,
        [FromBody] UpsertHealthSummaryComponentRequest request)
    {
        try
        {
            HealthSummaryComponentConfig config = new()
            {
                ComponentType = request.ComponentType,
                IsEnabled = request.IsEnabled,
                DisplayOrder = request.DisplayOrder,
                MaxOccurrences = request.MaxOccurrences,
                DaysBack = request.DaysBack,
                SectionHeader = request.SectionHeader
            };

            await GetTypeGrain(typeId).AddOrUpdateComponentAsync(config);

            // Refresh index entry component count
            HealthSummaryTypeState state = await GetTypeGrain(typeId).GetAsync();
            await GetTypeIndex().UpsertEntryAsync(new HealthSummaryTypeIndexEntry
            {
                TypeId = typeId,
                Name = state.Name,
                Description = state.Description,
                Status = state.Status,
                ComponentCount = state.Components.Count,
                CreatedDate = state.CreatedDate
            });

            _logger.LogInformation(
                "Upserted component {ComponentType} in health summary type {TypeId}",
                request.ComponentType, typeId);

            return Created($"api/healthsummary/types/{typeId}/components", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error upserting component in health summary type {TypeId}", typeId);
            return StatusCode(500, "An error occurred while updating the component");
        }
    }

    /// <summary>Remove a component from a health summary type template.</summary>
    [HttpDelete("api/healthsummary/types/{typeId}/components/{componentType}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveComponent(string typeId, HealthSummaryComponentType componentType)
    {
        try
        {
            await GetTypeGrain(typeId).RemoveComponentAsync(componentType);

            HealthSummaryTypeState state = await GetTypeGrain(typeId).GetAsync();
            await GetTypeIndex().UpsertEntryAsync(new HealthSummaryTypeIndexEntry
            {
                TypeId = typeId,
                Name = state.Name,
                Description = state.Description,
                Status = state.Status,
                ComponentCount = state.Components.Count,
                CreatedDate = state.CreatedDate
            });

            _logger.LogInformation(
                "Removed component {ComponentType} from health summary type {TypeId}",
                componentType, typeId);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing component from health summary type {TypeId}", typeId);
            return StatusCode(500, "An error occurred while removing the component");
        }
    }

    /// <summary>Activate or deactivate a health summary type template.</summary>
    [HttpPut("api/healthsummary/types/{typeId}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> SetTypeStatus(string typeId, [FromBody] SetHealthSummaryTypeStatusRequest request)
    {
        try
        {
            await GetTypeGrain(typeId).SetActiveAsync(request.IsActive);

            HealthSummaryTypeState state = await GetTypeGrain(typeId).GetAsync();
            await GetTypeIndex().UpsertEntryAsync(new HealthSummaryTypeIndexEntry
            {
                TypeId = typeId,
                Name = state.Name,
                Description = state.Description,
                Status = state.Status,
                ComponentCount = state.Components.Count,
                CreatedDate = state.CreatedDate
            });

            _logger.LogInformation(
                "Set health summary type {TypeId} active={IsActive}",
                typeId, request.IsActive);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting status on health summary type {TypeId}", typeId);
            return StatusCode(500, "An error occurred while updating the type status");
        }
    }

    // ─── Patient Summary Generation ────────────────────────────────────────────

    /// <summary>Generate a health summary for a patient using the specified template.</summary>
    [HttpPost("api/patient/{patientId}/healthsummary/generate")]
    [ProducesResponseType(typeof(GenerateHealthSummaryResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Generate(string patientId, [FromBody] GenerateHealthSummaryRequest request)
    {
        try
        {
            string reportId = await GetWorkflow(patientId).GenerateHealthSummaryAsync(
                request.TypeId,
                request.RequestedById,
                request.RequestedByName);

            _logger.LogInformation(
                "Generated health summary {ReportId} (type={TypeId}) for patient {PatientId}",
                reportId, request.TypeId, patientId);

            return Created(
                $"api/patient/{patientId}/healthsummary/{reportId}",
                new GenerateHealthSummaryResponse(reportId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating health summary for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while generating the health summary");
        }
    }

    /// <summary>Get a previously generated health summary report.</summary>
    [HttpGet("api/patient/{patientId}/healthsummary/{reportId}")]
    [ProducesResponseType(typeof(HealthSummaryState), StatusCodes.Status200OK)]
    public async Task<ActionResult<HealthSummaryState>> GetReport(string patientId, string reportId)
    {
        try
        {
            HealthSummaryState report = await GetWorkflow(patientId).GetHealthSummaryAsync(reportId);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving health summary {ReportId} for patient {PatientId}", reportId, patientId);
            return StatusCode(500, "An error occurred while retrieving the health summary");
        }
    }

    /// <summary>List all generated health summaries for a patient (newest first).</summary>
    [HttpGet("api/patient/{patientId}/healthsummary")]
    [ProducesResponseType(typeof(List<HealthSummaryIndexEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<HealthSummaryIndexEntry>>> GetList(
        string patientId,
        [FromQuery] string? typeId = null)
    {
        try
        {
            List<HealthSummaryIndexEntry> list = typeId is not null
                ? await GetWorkflow(patientId).GetHealthSummaryByTypeAsync(typeId)
                : await GetWorkflow(patientId).GetHealthSummaryListAsync();
            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving health summary list for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving the health summary list");
        }
    }
}

// ─── Request / Response DTOs ──────────────────────────────────────────────────

public record CreateHealthSummaryTypeRequest(
    string Name,
    string? Description,
    string CreatedById,
    string CreatedByName);

public record CreateHealthSummaryTypeResponse(string TypeId);

public record UpdateHealthSummaryTypeRequest(
    string Name,
    string? Description);

public record UpsertHealthSummaryComponentRequest(
    HealthSummaryComponentType ComponentType,
    bool IsEnabled,
    int DisplayOrder,
    int MaxOccurrences,
    int DaysBack,
    string? SectionHeader);

public record SetHealthSummaryTypeStatusRequest(bool IsActive);

public record GenerateHealthSummaryRequest(
    string TypeId,
    string RequestedById,
    string RequestedByName);

public record GenerateHealthSummaryResponse(string ReportId);
