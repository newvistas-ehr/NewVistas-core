// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Provider Dashboard API — provider-centric views for "My Patients" and "Today's Schedule".
/// Calls provider grains directly (not via workflow grain) since these are provider-scoped queries.
/// </summary>
[Authorize]
[ApiController]
[Route("api/providers")]
[Produces("application/json")]
public class ProviderDashboardController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<ProviderDashboardController> _logger;

    public ProviderDashboardController(IGrainFactory grainFactory, ILogger<ProviderDashboardController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IProviderPatientIndexGrain GetPatientIndex(string providerId)
        => _grainFactory.GetGrain<IProviderPatientIndexGrain>($"PROV-PAT-IDX:{providerId}");

    private IProviderScheduleIndexGrain GetScheduleIndex(string providerId)
        => _grainFactory.GetGrain<IProviderScheduleIndexGrain>($"PROV-SCHED:{providerId}");

    /// <summary>
    /// Get the provider's patient list. Supports optional name search and role filter.
    /// </summary>
    [HttpGet("{providerId}/patients")]
    [ProducesResponseType(typeof(List<ProviderPatientEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ProviderPatientEntry>>> GetMyPatients(
        string providerId,
        [FromQuery] string? search = null,
        [FromQuery] string? role = null)
    {
        try
        {
            List<ProviderPatientEntry> patients;

            if (!string.IsNullOrEmpty(search))
                patients = await GetPatientIndex(providerId).SearchPatientsAsync(search);
            else if (!string.IsNullOrEmpty(role))
                patients = await GetPatientIndex(providerId).GetPatientsByRoleAsync(role);
            else
                patients = await GetPatientIndex(providerId).GetActivePatientsAsync();

            return Ok(patients);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving patients for provider {ProviderId}", providerId);
            return StatusCode(500, "An error occurred while retrieving patient list");
        }
    }

    /// <summary>
    /// Get the provider's schedule for today.
    /// </summary>
    [HttpGet("{providerId}/schedule/today")]
    [ProducesResponseType(typeof(List<ProviderScheduleEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ProviderScheduleEntry>>> GetTodaySchedule(string providerId)
    {
        try
        {
            List<ProviderScheduleEntry> entries = await GetScheduleIndex(providerId).GetTodayAsync();
            return Ok(entries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving today's schedule for provider {ProviderId}", providerId);
            return StatusCode(500, "An error occurred while retrieving today's schedule");
        }
    }

    /// <summary>
    /// Get the provider's schedule for a specific date.
    /// </summary>
    [HttpGet("{providerId}/schedule")]
    [ProducesResponseType(typeof(List<ProviderScheduleEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ProviderScheduleEntry>>> GetScheduleByDate(
        string providerId,
        [FromQuery] DateTime? date = null)
    {
        try
        {
            List<ProviderScheduleEntry> entries;

            if (date.HasValue)
                entries = await GetScheduleIndex(providerId).GetByDateAsync(date.Value);
            else
                entries = await GetScheduleIndex(providerId).GetAllAsync();

            return Ok(entries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving schedule for provider {ProviderId}", providerId);
            return StatusCode(500, "An error occurred while retrieving schedule");
        }
    }

    /// <summary>
    /// Get the provider's upcoming schedule (next N days, default 7).
    /// </summary>
    [HttpGet("{providerId}/schedule/upcoming")]
    [ProducesResponseType(typeof(List<ProviderScheduleEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ProviderScheduleEntry>>> GetUpcomingSchedule(
        string providerId,
        [FromQuery] int days = 7)
    {
        try
        {
            List<ProviderScheduleEntry> entries = await GetScheduleIndex(providerId).GetUpcomingAsync(days);
            return Ok(entries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving upcoming schedule for provider {ProviderId}", providerId);
            return StatusCode(500, "An error occurred while retrieving upcoming schedule");
        }
    }

    // ─── Provider Availability (SD File #44.005) ���───────────────────────

    private IProviderAvailabilityGrain GetAvailability(string providerId)
        => _grainFactory.GetGrain<IProviderAvailabilityGrain>($"PROV-AVAIL:{providerId}");

    /// <summary>Get the full availability state for a provider.</summary>
    [HttpGet("{providerId}/availability")]
    public async Task<ActionResult<ProviderAvailabilityState>> GetAvailability(string providerId, int _unused = 0)
    {
        try
        {
            ProviderAvailabilityState state = await GetAvailability(providerId).GetAvailabilityAsync();
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving availability for provider {ProviderId}", providerId);
            return StatusCode(500, "An error occurred while retrieving availability");
        }
    }

    /// <summary>Update provider scheduling status (ACTIVE, ON_LEAVE, UNAVAILABLE).</summary>
    [HttpPut("{providerId}/availability/status")]
    public async Task<ActionResult> UpdateStatus(string providerId, [FromBody] UpdateStatusRequest request)
    {
        try
        {
            await GetAvailability(providerId).UpdateProviderStatusAsync(request.Status, request.Reason, request.ModifiedBy);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating status for provider {ProviderId}", providerId);
            return StatusCode(500, "An error occurred while updating provider status");
        }
    }

    /// <summary>Add a recurring weekly availability pattern.</summary>
    [HttpPost("{providerId}/availability/patterns")]
    public async Task<ActionResult> AddWeeklyPattern(string providerId, [FromBody] WeeklyAvailabilityPattern pattern)
    {
        try
        {
            await GetAvailability(providerId).AddWeeklyPatternAsync(pattern);
            return Created($"api/providers/{providerId}/availability/patterns/{pattern.PatternId}", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding weekly pattern for provider {ProviderId}", providerId);
            return StatusCode(500, "An error occurred while adding weekly pattern");
        }
    }

    /// <summary>Update an existing weekly pattern.</summary>
    [HttpPut("{providerId}/availability/patterns/{patternId}")]
    public async Task<ActionResult> UpdateWeeklyPattern(string providerId, string patternId, [FromBody] WeeklyAvailabilityPattern pattern)
    {
        try
        {
            await GetAvailability(providerId).UpdateWeeklyPatternAsync(patternId, pattern);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { Error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating weekly pattern for provider {ProviderId}", providerId);
            return StatusCode(500, "An error occurred while updating weekly pattern");
        }
    }

    /// <summary>Remove a weekly pattern.</summary>
    [HttpDelete("{providerId}/availability/patterns/{patternId}")]
    public async Task<ActionResult> RemoveWeeklyPattern(string providerId, string patternId)
    {
        try
        {
            await GetAvailability(providerId).RemoveWeeklyPatternAsync(patternId);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing weekly pattern for provider {ProviderId}", providerId);
            return StatusCode(500, "An error occurred while removing weekly pattern");
        }
    }

    /// <summary>Add a time block (vacation, sick leave, lunch, admin, etc.).</summary>
    [HttpPost("{providerId}/availability/blocks")]
    public async Task<ActionResult> AddTimeBlock(string providerId, [FromBody] ProviderTimeBlock block)
    {
        try
        {
            string blockId = await GetAvailability(providerId).AddTimeBlockAsync(block);
            return Created($"api/providers/{providerId}/availability/blocks/{blockId}", new { BlockId = blockId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding time block for provider {ProviderId}", providerId);
            return StatusCode(500, "An error occurred while adding time block");
        }
    }

    /// <summary>Remove a time block.</summary>
    [HttpDelete("{providerId}/availability/blocks/{blockId}")]
    public async Task<ActionResult> RemoveTimeBlock(string providerId, string blockId)
    {
        try
        {
            await GetAvailability(providerId).RemoveTimeBlockAsync(blockId);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing time block for provider {ProviderId}", providerId);
            return StatusCode(500, "An error occurred while removing time block");
        }
    }

    /// <summary>Get effective availability for a provider at a clinic on a date.</summary>
    [HttpGet("{providerId}/availability/effective")]
    public async Task<ActionResult<List<AvailabilityWindow>>> GetEffectiveAvailability(
        string providerId, [FromQuery] string clinicId, [FromQuery] DateTime date)
    {
        try
        {
            List<AvailabilityWindow> windows = await GetAvailability(providerId)
                .GetEffectiveAvailabilityAsync(clinicId, date);
            return Ok(windows);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving effective availability for provider {ProviderId}", providerId);
            return StatusCode(500, "An error occurred while retrieving effective availability");
        }
    }

    /// <summary>Set scheduling tier configuration for a provider at a specific clinic.</summary>
    [HttpPut("{providerId}/availability/tiers/{clinicId}")]
    public async Task<ActionResult> SetSchedulingTiers(
        string providerId, string clinicId, [FromBody] ClinicSchedulingTierConfig config)
    {
        try
        {
            await GetAvailability(providerId).SetClinicSchedulingTiersAsync(clinicId, config);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting scheduling tiers for provider {ProviderId}", providerId);
            return StatusCode(500, "An error occurred while setting scheduling tiers");
        }
    }

    public record UpdateStatusRequest(string Status, string? Reason, string? ModifiedBy);
}
